using System.Collections.Concurrent;
using System.Text.Json;

namespace ContentStudio.Services;

/// <summary>
/// Fans server-side events out to every open browser tab over Server-Sent Events:
/// validation results, file reloads, disk conflicts and save confirmations.
/// </summary>
public sealed class SseHub
{
    private sealed class Subscriber
    {
        public required StreamWriter Writer { get; init; }
        public required SemaphoreSlim WriteLock { get; init; }
    }

    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();

    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task HandleConnection(HttpContext context)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-store";
        var writer = new StreamWriter(context.Response.Body);
        var subscriber = new Subscriber { Writer = writer, WriteLock = new SemaphoreSlim(1, 1) };
        var key = Guid.NewGuid();
        _subscribers[key] = subscriber;

        try
        {
            await writer.WriteAsync("event: connected\ndata: {}\n\n");
            await writer.FlushAsync();
            // Keep the connection alive until the tab closes.
            while (!context.RequestAborted.IsCancellationRequested)
            {
                await Task.Delay(15_000, context.RequestAborted);
                await SendTo(subscriber, "ping", new { });
            }
        }
        catch (OperationCanceledException)
        {
            // Normal disconnect.
        }
        finally
        {
            _subscribers.TryRemove(key, out _);
        }
    }

    public void Broadcast(string eventName, object payload)
    {
        foreach (var subscriber in _subscribers.Values)
            _ = SendTo(subscriber, eventName, payload);
    }

    private static async Task SendTo(Subscriber subscriber, string eventName, object payload)
    {
        var json = JsonSerializer.Serialize(payload, PayloadOptions);
        await subscriber.WriteLock.WaitAsync();
        try
        {
            await subscriber.Writer.WriteAsync($"event: {eventName}\ndata: {json}\n\n");
            await subscriber.Writer.FlushAsync();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // The tab went away; the connection handler will clean up.
        }
        finally
        {
            subscriber.WriteLock.Release();
        }
    }
}
