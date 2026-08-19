using System.Diagnostics;
using System.Text.Encodings.Web;
using ContentStudio.Api;
using ContentStudio.Services;

// Content Studio — standalone developer tool for In The Dungeons We Die.
// Serves a local UI over the game's authored JSON. The game never references this project.

var port = 5590;
var openBrowser = true;
for (var index = 0; index < args.Length; index++)
{
    if (args[index] == "--port" && index + 1 < args.Length && int.TryParse(args[index + 1], out var parsedPort))
        port = parsedPort;
    if (args[index] == "--no-browser")
        openBrowser = false;
}

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

var app = builder.Build();

using var studioState = new StudioState();
if (!studioState.TryOpenProjectFromSettingsOrDiscovery())
    Console.WriteLine("No game project found yet — pick one in the browser window.");

// The UI is bundled static files; no build step, no CDN. Serve fresh on every reload so
// iterating on the tool never fights browser caching.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-store",
});

app.MapContentStudioApi(studioState);

app.Lifetime.ApplicationStarted.Register(() =>
{
    var url = $"http://127.0.0.1:{port}";
    Console.WriteLine($"Content Studio running at {url}");
    if (studioState.Workspace.IsLoaded)
        Console.WriteLine($"Project: {studioState.Workspace.ProjectRoot}");
    if (openBrowser)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Console.WriteLine($"Could not open a browser automatically — open {url} yourself.");
        }
    }
});

app.Run();
