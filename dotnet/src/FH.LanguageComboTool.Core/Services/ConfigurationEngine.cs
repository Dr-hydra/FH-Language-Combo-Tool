using FH.LanguageComboTool.Core.Models;

namespace FH.LanguageComboTool.Core.Services;

public sealed class ConfigurationEngine(
    BackupManager backupManager,
    StatusService statusService,
    RestoreEngine restoreEngine,
    ApplyEngine applyEngine)
{
    public ApplyResult ExecuteApply(
        GameProfile profile,
        string voiceLanguage,
        string textLanguage,
        bool forceRefresh = false)
    {
        var backupRoot = backupManager.GetBackupRoot();
        return ExecuteApply(
            profile,
            voiceLanguage,
            textLanguage,
            backupRoot,
            userPreferredLangRoot: null,
            forceRefresh);
    }

    public ApplyResult ExecuteApply(
        GameProfile profile,
        string voiceLanguage,
        string textLanguage,
        string backupRoot,
        string? userPreferredLangRoot = null,
        bool forceRefresh = false)
    {
        var gameId = GameDetector.ToWireId(profile.GameId);
        var voice = voiceLanguage.ToUpperInvariant();
        var text = textLanguage.ToUpperInvariant();

        if (string.Equals(voice, text, StringComparison.OrdinalIgnoreCase))
            return Failure("语音语言与文字语言必须不同。");

        ConfigStatus status;
        try
        {
            status = statusService.GetStatus(gameId, profile.ResourcePath, backupRoot);
        }
        catch (Exception ex)
        {
            return Failure($"读取当前配置状态失败：{ex.Message}");
        }

        var latest = backupManager.FindLatestApplicableBackup(
            backupRoot,
            gameId,
            profile.ResourcePath);

        if (status.State is "external_swap" or "external_duplicate")
        {
            return Failure(
                "检测到由外部方式修改的语言包，但没有可用于恢复的原始备份。请先通过 Steam 验证游戏文件，恢复原始语言包后再应用配置。");
        }

        if (status.State == "applied" &&
            !forceRefresh &&
            string.Equals(status.VoiceLanguage, voice, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(status.TextLanguage, text, StringComparison.OrdinalIgnoreCase))
        {
            return new(
                true,
                "所选语言组合已经生效，无需重复应用。",
                latest?.Path,
                false,
                false,
                null);
        }

        if (status.State is "applied" or "outdated" or "modified")
        {
            if (latest is null)
                return Failure("检测到现有语言组合，但找不到可用的原始备份。");

            var restoreResult = restoreEngine.ExecuteRestore(latest.Path, userPreferredLangRoot);
            if (!restoreResult.Success)
                return Failure($"应用新组合前恢复原始文件失败：{restoreResult.Message}");
        }

        try
        {
            var plan = LanguageMapper.GenerateApplyPlan(
                gameId,
                voice,
                text,
                profile.ResourcePath,
                backupRoot,
                profile.ManifestPath);

            return applyEngine.ExecuteApply(plan, profile, backupRoot, userPreferredLangRoot);
        }
        catch (Exception ex)
        {
            return Failure($"生成语言组合应用计划失败：{ex.Message}");
        }
    }

    private static ApplyResult Failure(string message) =>
        new(false, message, null, false, false, null);
}
