using System.IO;
namespace MultyAudioOutput.Platform;

internal static class Log
{
    private static readonly object Gate = new();
    private static readonly string FilePath = Path.Combine(AppPaths.DataDir, "log.txt");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string context, Exception ex) => Write("ERROR", $"{context}: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 1_000_000) info.Delete();
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // logging must never crash the app
        }
    }
}

internal static class AppPaths
{
    public static string DataDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultyAudioOutput");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
