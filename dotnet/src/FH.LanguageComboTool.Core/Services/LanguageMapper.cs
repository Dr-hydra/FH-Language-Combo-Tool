using FH.LanguageComboTool.Core.Models;

namespace FH.LanguageComboTool.Core.Services;

public static class LanguageMapper
{
    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EN"] = "英语",
        ["GB"] = "英语（英国）",
        ["JP"] = "日语",
        ["CHS"] = "简体中文",
        ["CHT"] = "繁体中文",
        ["FR"] = "法语",
        ["DE"] = "德语",
        ["ES"] = "西班牙语",
        ["MX"] = "西班牙语（墨西哥）",
        ["IT"] = "意大利语",
        ["PT"] = "葡萄牙语",
        ["BR"] = "葡萄牙语（巴西）",
        ["KO"] = "韩语",
        ["RU"] = "俄语",
        ["PL"] = "波兰语",
        ["NL"] = "荷兰语",
        ["TR"] = "土耳其语",
        ["DK"] = "丹麦语",
        ["SV"] = "瑞典语",
        ["NO"] = "挪威语",
        ["FI"] = "芬兰语",
        ["CZ"] = "捷克语",
        ["HU"] = "匈牙利语",
        ["EL"] = "希腊语"
    };

    public static readonly ISet<string> VoiceLanguageCodes = new HashSet<string>(
        ["EN", "GB", "JP", "CHS", "CHT", "BR", "DE", "ES", "FR", "IT", "KO", "MX"],
        StringComparer.OrdinalIgnoreCase);

    public static string GetDisplayName(string code) =>
        DisplayNames.TryGetValue(code, out var name) ? name : $"未知语言（{code}）";

    public static string ResolveFileName(string code, string resourcePath)
    {
        if (!Directory.Exists(resourcePath))
            throw new DirectoryNotFoundException($"找不到语言资源目录：{resourcePath}");

        var match = Directory.EnumerateFiles(resourcePath, "*.zip")
            .FirstOrDefault(file => string.Equals(
                Path.GetFileNameWithoutExtension(file),
                code,
                StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new FileNotFoundException($"在 {resourcePath} 中找不到语言代码“{code}”对应的 ZIP 文件。")
            : Path.GetFileName(match);
    }

    public static ApplyPlan GenerateApplyPlan(
        string gameId,
        string voiceLanguage,
        string textLanguage,
        string resourcePath,
        string backupRoot,
        string? manifestPath)
    {
        if (string.Equals(voiceLanguage, textLanguage, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("语音语言与文字语言必须不同。");

        var sourceFile = ResolveFileName(textLanguage, resourcePath);
        var targetFile = ResolveFileName(voiceLanguage, resourcePath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var voiceUpper = voiceLanguage.ToUpperInvariant();
        var textUpper = textLanguage.ToUpperInvariant();
        var backupTarget = Path.Combine(
            backupRoot,
            gameId,
            $"{timestamp}_{voiceUpper}_voice_{textUpper}_text",
            "original",
            targetFile);

        var sourcePath = Path.Combine(resourcePath, sourceFile);
        var targetPath = Path.Combine(resourcePath, targetFile);

        return new ApplyPlan(
            gameId,
            voiceUpper,
            textUpper,
            sourceFile,
            targetFile,
            [
                new("backup", targetPath, backupTarget, $"备份 {targetFile}"),
                new("copy_replace", sourcePath, targetPath, $"复制 {sourceFile} -> {targetFile}")
            ],
            SteamLanguageService.CodeToSteamLanguage(voiceLanguage),
            manifestPath);
    }
}
