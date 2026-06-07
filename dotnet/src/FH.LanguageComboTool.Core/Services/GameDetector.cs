using FH.LanguageComboTool.Core.Models;
using Microsoft.Win32;

namespace FH.LanguageComboTool.Core.Services;

public sealed class GameDetector
{
    public const string Fh5SteamAppId = "1551360";
    public const string Fh6SteamAppId = "2483190";
    public const string Fh5Executable = "ForzaHorizon5.exe";
    public const string Fh6Executable = "forzahorizon6.exe";
    public const string ResourceSubpath = @"media\Stripped\StringTables";

    public IReadOnlyList<GameProfile> DetectSteamGames()
    {
        var steamPath = ReadSteamPath();
        return steamPath is null ? [] : DetectSteamGames(steamPath);
    }

    public IReadOnlyList<GameProfile> DetectSteamGames(string steamPath)
    {
        if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
            return [];

        IReadOnlyList<string> parsedLibraryPaths;
        try
        {
            var libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            parsedLibraryPaths = File.Exists(libraryFile)
                ? VdfParser.ExtractLibraryPaths(File.ReadAllText(libraryFile))
                : [];
        }
        catch
        {
            parsedLibraryPaths = [];
        }

        var libraryPaths = parsedLibraryPaths
            .Prepend(steamPath)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var profiles = new List<GameProfile>();
        foreach (var libraryPath in libraryPaths)
        {
            foreach (var gameId in new[] { GameId.Fh5, GameId.Fh6 })
            {
                var manifestPath = Path.Combine(
                    libraryPath,
                    "steamapps",
                    $"appmanifest_{GetSteamAppId(gameId)}.acf");

                if (!File.Exists(manifestPath))
                    continue;

                var installDir = VdfParser.ExtractValue(File.ReadAllText(manifestPath), "installdir");
                if (string.IsNullOrWhiteSpace(installDir))
                    continue;

                var root = Path.Combine(libraryPath, "steamapps", "common", installDir);
                try
                {
                    var profile = ValidateGameDirectory(root, gameId);
                    profiles.Add(profile with { ManifestPath = manifestPath });
                }
                catch
                {
                    // Ignore invalid Steam manifests and keep scanning other libraries.
                }
            }
        }

        return profiles;
    }

    public GameProfile ValidateGameDirectory(string path, GameId gameId)
    {
        RejectDangerousPath(path);

        var root = Path.GetFullPath(path);
        var executableName = GetExecutableName(gameId);
        var exePath = Path.Combine(root, executableName);
        if (!File.Exists(exePath))
            throw new FileNotFoundException($"在目录中找不到游戏可执行文件“{executableName}”：{root}");

        var resourcePath = Path.Combine(root, ResourceSubpath);
        if (!Directory.Exists(resourcePath))
            throw new DirectoryNotFoundException($"找不到语言资源目录：{resourcePath}");

        var zipCount = Directory.EnumerateFiles(resourcePath, "*.zip").Count();
        if (zipCount < 2)
            throw new InvalidDataException($"语言资源目录至少需要 2 个 ZIP 文件，当前仅找到 {zipCount} 个：{resourcePath}");

        return new GameProfile(
            gameId,
            GetDisplayName(gameId),
            "steam",
            GetSteamAppId(gameId),
            root,
            resourcePath,
            executableName,
            null);
    }

    public static string GetSteamAppId(GameId gameId) => gameId switch
    {
        GameId.Fh5 => Fh5SteamAppId,
        GameId.Fh6 => Fh6SteamAppId,
        _ => throw new ArgumentOutOfRangeException(nameof(gameId), gameId, null)
    };

    public static string GetExecutableName(GameId gameId) => gameId switch
    {
        GameId.Fh5 => Fh5Executable,
        GameId.Fh6 => Fh6Executable,
        _ => throw new ArgumentOutOfRangeException(nameof(gameId), gameId, null)
    };

    public static string ToWireId(GameId gameId) => gameId switch
    {
        GameId.Fh5 => "fh5",
        GameId.Fh6 => "fh6",
        _ => throw new ArgumentOutOfRangeException(nameof(gameId), gameId, null)
    };

    public static GameId ParseWireId(string gameId) => gameId.ToLowerInvariant() switch
    {
        "fh5" => GameId.Fh5,
        "fh6" => GameId.Fh6,
        _ => throw new ArgumentException($"未知游戏标识：{gameId}", nameof(gameId))
    };

    private static string GetDisplayName(GameId gameId) => gameId switch
    {
        GameId.Fh5 => "Forza Horizon 5",
        GameId.Fh6 => "Forza Horizon 6",
        _ => throw new ArgumentOutOfRangeException(nameof(gameId), gameId, null)
    };

    private static string? ReadSteamPath()
    {
        try
        {
            using var steamKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
            var value = steamKey?.GetValue("SteamPath") as string;
            return !string.IsNullOrWhiteSpace(value) && Directory.Exists(value) ? value : null;
        }
        catch
        {
            return null;
        }
    }

    private static void RejectDangerousPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"目录不存在或无法访问：{path}");

        var root = Path.GetPathRoot(fullPath);
        if (string.Equals(
                fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"不能将磁盘根目录作为游戏目录：{fullPath}");

        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windowsDir) &&
            fullPath.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"不能将系统目录作为游戏目录：{fullPath}");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.IsNullOrWhiteSpace(home) &&
            string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), home, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"不能将用户主目录作为游戏目录：{fullPath}");
    }
}
