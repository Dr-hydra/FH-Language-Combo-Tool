using FH.LanguageComboTool.Core.Models;

namespace FH.LanguageComboTool.Core.Services;

public sealed class RestoreEngine(BackupManager backupManager)
{
    public RestoreResult ExecuteRestore(string backupPath, string? userPreferredLangRoot = null)
    {
        BackupManifest manifest;
        try
        {
            manifest = backupManager.ReadManifest(backupPath);
        }
        catch (Exception ex)
        {
            return new(false, $"读取备份清单失败：{ex.Message}");
        }

        GameId gameId;
        try
        {
            gameId = GameDetector.ParseWireId(manifest.Game);
        }
        catch (Exception ex)
        {
            return new(false, $"备份清单中的游戏标识无效：{ex.Message}");
        }

        if (ProcessService.IsGameRunning(gameId))
            return new(false, "游戏正在运行，请关闭游戏后再恢复备份。");

        var fileEntry = manifest.Files.FirstOrDefault();
        if (fileEntry is null)
            return new(false, "备份清单不包含文件记录。");

        string originalFile;
        string targetPath;
        try
        {
            originalFile = BackupManager.ResolveContainedPath(Path.Combine(backupPath, "original"), fileEntry.Path);
            targetPath = BackupManager.ResolveContainedPath(manifest.ResourceDirectory, manifest.TargetFile);
        }
        catch (Exception ex)
        {
            return new(false, $"备份路径校验失败：{ex.Message}");
        }

        if (!File.Exists(originalFile))
            return new(false, $"备份原始文件不存在：{originalFile}");

        var tempPath = targetPath + $".fhlt.{Guid.NewGuid():N}.tmp";

        try
        {
            var backupHash = ResourceScanner.ComputeSha256(originalFile);
            if (!string.Equals(backupHash, fileEntry.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                return new(false, $"备份文件完整性校验失败：预期 {fileEntry.OriginalSha256}，实际 {backupHash}");

            File.Copy(originalFile, tempPath, overwrite: true);
            var tempHash = ResourceScanner.ComputeSha256(tempPath);
            if (!string.Equals(tempHash, fileEntry.OriginalSha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(tempPath);
                return new(false, $"临时恢复文件校验失败：预期 {fileEntry.OriginalSha256}，实际 {tempHash}");
            }

            File.Move(tempPath, targetPath, overwrite: true);
            var finalHash = ResourceScanner.ComputeSha256(targetPath);
            if (!string.Equals(finalHash, fileEntry.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                return new(false, $"恢复后文件校验失败：预期 {fileEntry.OriginalSha256}，实际 {finalHash}");
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            return new(false, $"恢复备份失败：{ex.Message}");
        }

        TryRestoreSteamLanguage(manifest, gameId, userPreferredLangRoot);
        return new(true, $"已将 {manifest.TargetFile} 恢复为原始状态。");
    }

    private static void TryRestoreSteamLanguage(
        BackupManifest manifest,
        GameId gameId,
        string? userPreferredLangRoot)
    {
        try
        {
            if (manifest.ManifestPath is not null &&
                manifest.OriginalSteamLanguage is not null &&
                File.Exists(manifest.ManifestPath))
            {
                if (manifest.OriginalSteamLanguage.Length == 0)
                    SteamLanguageService.RemoveManifestLanguage(manifest.ManifestPath);
                else
                    SteamLanguageService.SetManifestLanguage(manifest.ManifestPath, manifest.OriginalSteamLanguage);
            }

            if (manifest.OriginalUserPreferredLang is not null)
            {
                if (manifest.OriginalUserPreferredLang.Length == 0)
                    SteamLanguageService.RemoveUserPreferredLang(gameId, userPreferredLangRoot);
                else
                    SteamLanguageService.SetUserPreferredLang(gameId, manifest.OriginalUserPreferredLang, userPreferredLangRoot);
            }
        }
        catch
        {
            // Restore of language metadata is best-effort; file restore has already succeeded.
        }
    }
}
