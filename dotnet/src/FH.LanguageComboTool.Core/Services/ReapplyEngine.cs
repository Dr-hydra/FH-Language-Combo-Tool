using FH.LanguageComboTool.Core.Models;

namespace FH.LanguageComboTool.Core.Services;

public sealed class ReapplyEngine(
    BackupManager backupManager,
    ConfigurationEngine configurationEngine)
{
    public ApplyResult ExecuteReapply(GameProfile profile)
    {
        var backupRoot = backupManager.GetBackupRoot();
        return ExecuteReapply(profile, backupRoot);
    }

    public ApplyResult ExecuteReapply(
        GameProfile profile,
        string backupRoot,
        string? userPreferredLangRoot = null)
    {
        var gameId = GameDetector.ToWireId(profile.GameId);
        var latest = backupManager.FindLatestApplicableBackup(
            backupRoot,
            gameId,
            profile.ResourcePath);

        if (latest is null)
            return Failure("没有可用于重新应用的有效备份。");

        BackupManifest manifest;
        try
        {
            manifest = backupManager.ReadManifest(latest.Path);
        }
        catch (Exception ex)
        {
            return Failure($"读取最近备份失败：{ex.Message}");
        }

        if (!string.Equals(manifest.Game, gameId, StringComparison.OrdinalIgnoreCase))
            return Failure("最近备份与当前游戏不匹配。");

        return configurationEngine.ExecuteApply(
            profile,
            manifest.VoiceLanguage,
            manifest.TextLanguage,
            backupRoot,
            userPreferredLangRoot,
            forceRefresh: true);
    }

    private static ApplyResult Failure(string message) =>
        new(false, message, null, false, false, null);
}
