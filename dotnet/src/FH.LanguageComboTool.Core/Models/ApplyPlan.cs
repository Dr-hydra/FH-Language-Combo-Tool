namespace FH.LanguageComboTool.Core.Models;

public sealed record ApplyPlan(
    string GameId,
    string VoiceLanguage,
    string TextLanguage,
    string SourceFile,
    string TargetFile,
    IReadOnlyList<ApplyOperation> Operations,
    string? SteamLanguage,
    string? ManifestPath);

public sealed record ApplyOperation(
    string Type,
    string From,
    string To,
    string Description);
