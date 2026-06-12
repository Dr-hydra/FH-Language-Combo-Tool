using System.Text.Json;
using FH.LanguageComboTool.Core.Models;

namespace FH.LanguageComboTool.Core.Services;

public sealed class BackupManager
{
    public const string ToolVersion = "2.1.1";

    public string GetBackupRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            throw new InvalidOperationException("无法获取 LOCALAPPDATA 目录。");

        var backupRoot = Path.Combine(localAppData, "FHLanguageComboTool", "backups");
        Directory.CreateDirectory(backupRoot);
        return backupRoot;
    }

    public string CreateBackup(
        string backupRoot,
        string gameId,
        string channel,
        string gameRoot,
        string resourceDirectory,
        string voiceLanguage,
        string textLanguage,
        string targetFilePath,
        string sourceFileName,
        string? acfManifestPath,
        string? originalSteamLanguage,
        string? originalUserPreferredLang,
        string? appliedSha256)
    {
        var now = DateTimeOffset.Now;
        var dirName = $"{now:yyyyMMdd_HHmmss_fff}_{voiceLanguage}_voice_{textLanguage}_text_{Guid.NewGuid():N}";
        var backupDir = Path.Combine(backupRoot, gameId, dirName);
        var originalDir = Path.Combine(backupDir, "original");
        Directory.CreateDirectory(originalDir);

        try
        {
            var targetFileName = Path.GetFileName(targetFilePath);
            var dest = Path.Combine(originalDir, targetFileName);
            var originalHash = ResourceScanner.ComputeSha256(targetFilePath);
            File.Copy(targetFilePath, dest, overwrite: true);

            var copiedHash = ResourceScanner.ComputeSha256(dest);
            if (!string.Equals(originalHash, copiedHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"备份复制后的 SHA-256 不一致：源文件={originalHash}，备份文件={copiedHash}");

            var manifest = new BackupManifest
            {
                ToolVersion = ToolVersion,
                Game = gameId,
                Channel = channel,
                GameRoot = gameRoot,
                ResourceDirectory = resourceDirectory,
                VoiceLanguage = voiceLanguage,
                TextLanguage = textLanguage,
                TargetFile = targetFileName,
                SourceFile = sourceFileName,
                CreatedAt = now.ToString("O"),
                ManifestPath = acfManifestPath,
                OriginalSteamLanguage = originalSteamLanguage,
                OriginalUserPreferredLang = originalUserPreferredLang,
                AppliedSha256 = appliedSha256,
                Files = [new BackupFileEntry { Path = targetFileName, OriginalSha256 = originalHash }]
            };

            WriteManifest(backupDir, manifest);
            return backupDir;
        }
        catch
        {
            TryDeleteDirectory(backupDir);
            throw;
        }
    }

    public IReadOnlyList<BackupInfo> ListBackups(string gameId) =>
        ListBackups(GetBackupRoot(), gameId);

    public IReadOnlyList<BackupInfo> ListBackups(string backupRoot, string gameId)
    {
        var gameBackupDir = Path.Combine(backupRoot, gameId);
        if (!Directory.Exists(gameBackupDir))
            return [];

        return Directory.EnumerateDirectories(gameBackupDir)
            .Select(TryReadBackupInfo)
            .Where(info => info is not null)
            .Cast<BackupInfo>()
            .OrderByDescending(info => info.CreatedAt, StringComparer.Ordinal)
            .ToList();
    }

    public BackupInfo? FindLatestApplicableBackup(
        string backupRoot,
        string gameId,
        string resourceDirectory)
    {
        foreach (var backup in ListBackups(backupRoot, gameId).Where(item => item.Valid))
        {
            try
            {
                var manifest = ReadManifest(backup.Path);
                if (string.Equals(manifest.Game, gameId, StringComparison.OrdinalIgnoreCase) &&
                    PathsEqual(manifest.ResourceDirectory, resourceDirectory))
                    return backup;
            }
            catch
            {
                // Skip unreadable or incompatible backup metadata.
            }
        }

        return null;
    }

    public BackupManifest ReadManifest(string backupPath)
    {
        var manifestPath = Path.Combine(backupPath, "manifest.json");
        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize(json, BackupJsonContext.Default.BackupManifest)
            ?? throw new InvalidDataException("备份清单为空。");
    }

    private void WriteManifest(string backupPath, BackupManifest manifest)
    {
        var json = JsonSerializer.Serialize(manifest, BackupJsonContext.Default.BackupManifest);
        File.WriteAllText(Path.Combine(backupPath, "manifest.json"), json);
    }

    private BackupInfo? TryReadBackupInfo(string backupPath)
    {
        try
        {
            var manifest = ReadManifest(backupPath);
            var valid = manifest.Files.Count > 0 && manifest.Files.All(file =>
            {
                try
                {
                    var originalPath = ResolveContainedPath(Path.Combine(backupPath, "original"), file.Path);
                    return File.Exists(originalPath) &&
                           string.Equals(
                               ResourceScanner.ComputeSha256(originalPath),
                               file.OriginalSha256,
                               StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            });

            return new BackupInfo(
                Path.GetFileName(backupPath),
                manifest.Game,
                manifest.VoiceLanguage,
                manifest.TextLanguage,
                manifest.CreatedAt,
                backupPath,
                valid);
        }
        catch
        {
            return null;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); }
        catch { }
    }

    internal static string ResolveContainedPath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new InvalidDataException("备份清单包含无效路径。");

        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        var rootPrefix = fullRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("备份清单路径超出允许目录。");

        return fullPath;
    }

    internal static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
