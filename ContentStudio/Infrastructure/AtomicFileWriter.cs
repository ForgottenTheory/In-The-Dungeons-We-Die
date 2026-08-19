using System.Text;

namespace ContentStudio.Infrastructure;

/// <summary>
/// Writes files so a crash mid-write can never leave half a JSON file behind: write to a
/// sibling temp file, flush, then move it over the target in one filesystem operation.
/// </summary>
public static class AtomicFileWriter
{
    public static void Write(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporaryPath = targetPath + ".contentstudio-tmp";
        // UTF-8 without BOM, matching how the authored files are stored.
        File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // OneDrive/editors can hold transient locks; a short retry beats a spurious failure.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(temporaryPath, targetPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(80);
            }
        }
    }
}
