namespace FH.LanguageComboTool.Core.Models;

public sealed record ApplyResult(
    bool Success,
    string Message,
    string? BackupPath,
    bool RolledBack,
    bool SteamLanguageSet,
    string? SteamLanguageWarning);

public sealed record RestoreResult(bool Success, string Message);

public sealed record ConfigStatus(
    string State,
    string? GameId,
    string? VoiceLanguage,
    string? TextLanguage,
    string? LastApplied);
