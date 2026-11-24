using System.IO;

namespace ClaudeMd.Services;

/// <summary>Where the app keeps its files: next to the exe (published single file).</summary>
public static class AppPaths
{
    public static string Base { get; } = ResolveBase();
    public static string StorageDir => Path.Combine(Base, "Storage");
    public static string SettingDir => Path.Combine(Base, "setting files");
    public static string PathJson => Path.Combine(SettingDir, "path.json");

    private static string ResolveBase()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe) && !Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(exe)!;
        return AppContext.BaseDirectory.TrimEnd('\\', '/');
    }
}
