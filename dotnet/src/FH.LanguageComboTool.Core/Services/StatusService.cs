using FH.LanguageComboTool.Core.Models;

namespace FH.LanguageComboTool.Core.Services;

public sealed class StatusService(BackupManager backupManager)
{
    public ConfigStatus GetStatus(string gameId, string resourcePath)
    {
        var backups = backupManager.ListBackups(gameId);
        return GetStatusFromBackups(gameId, resourcePath, backups);
    }

    public ConfigStatus GetStatus(string gameId, string resourcePath, string backupRoot)
    {
        var backups = backupManager.ListBackups(backupRoot, gameId);
        return GetStatusFromBackups(gameId, resourcePath, backups);
    }

    private ConfigStatus GetStatusFromBackups(string gameId, string resourcePath, IReadOnlyList<BackupInfo> backups)
    {
        var latest = FindLatestApplicableBackup(backups, gameId, resourcePath);
        if (latest is null)
            return ExternalConfigurationDetector.Detect(gameId, resourcePath)
                ?? new("none", null, null, null, null);

        var manifest = backupManager.ReadManifest(latest.Path);
        string targetPath;
        string sourcePath;
        try
        {
            targetPath = BackupManager.ResolveContainedPath(resourcePath, manifest.TargetFile);
            sourcePath = BackupManager.ResolveContainedPath(resourcePath, manifest.SourceFile);
        }
        catch
        {
            return new("modified", manifest.Game, manifest.VoiceLanguage, manifest.TextLanguage, manifest.CreatedAt);
        }

        if (!File.Exists(targetPath))
            return new("modified", manifest.Game, manifest.VoiceLanguage, manifest.TextLanguage, manifest.CreatedAt);

        var currentHash = ResourceScanner.ComputeSha256(targetPath);
        var sourceHash = File.Exists(sourcePath) ? ResourceScanner.ComputeSha256(sourcePath) : null;
        var originalHash = manifest.Files.FirstOrDefault()?.OriginalSha256;

        return new(
            ClassifyState(currentHash, sourceHash, originalHash, manifest.AppliedSha256),
            manifest.Game,
            manifest.VoiceLanguage,
            manifest.TextLanguage,
            manifest.CreatedAt);
    }

    public static string ClassifyState(
        string current,
        string? source,
        string? original,
        string? applied)
    {
        if (source == current)
            return "applied";
        if (original == current)
            return "reverted";
        if (applied == current)
            return "outdated";
        return "modified";
    }

    private BackupInfo? FindLatestApplicableBackup(
        IReadOnlyList<BackupInfo> backups,
        string gameId,
        string resourcePath)
    {
        foreach (var backup in backups.Where(item => item.Valid))
        {
            try
            {
                var manifest = backupManager.ReadManifest(backup.Path);
                if (string.Equals(manifest.Game, gameId, StringComparison.OrdinalIgnoreCase) &&
                    BackupManager.PathsEqual(manifest.ResourceDirectory, resourcePath))
                    return backup;
            }
            catch
            {
                // Ignore incompatible backup metadata and inspect the next entry.
            }
        }

        return null;
    }
}
