using FH.LanguageComboTool.Core.Models;

namespace FH.LanguageComboTool.Core.Services;

public sealed class ApplyEngine(BackupManager backupManager)
{
    public ApplyResult ExecuteApply(ApplyPlan plan, GameProfile profile)
    {
        var backupRoot = backupManager.GetBackupRoot();
        return ExecuteApply(plan, profile, backupRoot);
    }

    public ApplyResult ExecuteApply(
        ApplyPlan plan,
        GameProfile profile,
        string backupRoot,
        string? userPreferredLangRoot = null)
    {
        if (ProcessService.IsGameRunning(profile.GameId))
            return new(false, "游戏正在运行，请关闭游戏后再应用语言配置。", null, false, false, null);

        var copyOp = plan.Operations.FirstOrDefault(op => op.Type == "copy_replace");
        var backupOp = plan.Operations.FirstOrDefault(op => op.Type == "backup");
        if (copyOp is null || backupOp is null)
            return new(false, "应用计划缺少必要操作。", null, false, false, null);

        if (!File.Exists(copyOp.From))
            return new(false, $"文字语言包不存在：{copyOp.From}", null, false, false, null);

        if (!File.Exists(backupOp.From))
            return new(false, $"语音语言包不存在：{backupOp.From}", null, false, false, null);

        string sourceHash;
        try
        {
            sourceHash = ResourceScanner.ComputeSha256(copyOp.From);
        }
        catch (Exception ex)
        {
            return new(false, $"计算文字语言包 SHA-256 失败：{ex.Message}", null, false, false, null);
        }

        var originalSteamLanguage = plan.ManifestPath is null
            ? null
            : SteamLanguageService.ReadManifestLanguage(plan.ManifestPath) ?? "";

        var gameId = GameDetector.ParseWireId(plan.GameId);
        var originalUserPreferredLang = SteamLanguageService.GetUserPreferredLangPath(gameId, userPreferredLangRoot) is null
            ? null
            : SteamLanguageService.ReadUserPreferredLang(gameId, userPreferredLangRoot) ?? "";

        string backupPath;
        try
        {
            backupPath = backupManager.CreateBackup(
                backupRoot,
                plan.GameId,
                profile.Channel,
                profile.RootPath,
                profile.ResourcePath,
                plan.VoiceLanguage,
                plan.TextLanguage,
                backupOp.From,
                plan.SourceFile,
                plan.ManifestPath,
                originalSteamLanguage,
                originalUserPreferredLang,
                sourceHash);
        }
        catch (Exception ex)
        {
            return new(false, $"创建备份失败：{ex.Message}", null, false, false, null);
        }

        var tempPath = backupOp.From + $".fhlt.{Guid.NewGuid():N}.tmp";
        try
        {
            File.Copy(copyOp.From, tempPath, overwrite: true);
            var tempHash = ResourceScanner.ComputeSha256(tempPath);
            if (!string.Equals(sourceHash, tempHash, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(tempPath);
                return new(false, $"临时文件校验失败：源文件={sourceHash}，临时文件={tempHash}", backupPath, false, false, null);
            }

            File.Move(tempPath, backupOp.From, overwrite: true);
            var finalHash = ResourceScanner.ComputeSha256(backupOp.From);
            if (!string.Equals(sourceHash, finalHash, StringComparison.OrdinalIgnoreCase))
            {
                var rolledBack = RollbackFromBackup(backupPath, backupOp.From);
                return new(false, $"最终文件校验失败：预期 {sourceHash}，实际 {finalHash}", backupPath, rolledBack, false, null);
            }
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            var rolledBack = RollbackFromBackup(backupPath, backupOp.From);
            return new(false, $"应用语言包失败：{ex.Message}", backupPath, rolledBack, false, null);
        }

        var steamLanguageSet = false;
        string? steamWarning = null;
        if (plan.ManifestPath is not null && plan.SteamLanguage is not null)
        {
            try
            {
                SteamLanguageService.SetManifestLanguage(plan.ManifestPath, plan.SteamLanguage);
                steamLanguageSet = true;
            }
            catch (Exception ex)
            {
                steamWarning = $"文件替换成功，但无法更新 Steam 语言：{ex.Message}";
            }
        }

        try
        {
            SteamLanguageService.SetUserPreferredLang(gameId, plan.VoiceLanguage, userPreferredLangRoot);
        }
        catch
        {
            // Non-critical; appmanifest language is still the primary Steam setting.
        }

        return new(
            true,
            $"已将 {plan.TextLanguage} 文字应用到 {plan.VoiceLanguage} 语音包。",
            backupPath,
            false,
            steamLanguageSet,
            steamWarning);
    }

    private bool RollbackFromBackup(string backupPath, string targetPath)
    {
        try
        {
            var manifest = backupManager.ReadManifest(backupPath);
            var file = manifest.Files.First();
            File.Copy(Path.Combine(backupPath, "original", file.Path), targetPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
