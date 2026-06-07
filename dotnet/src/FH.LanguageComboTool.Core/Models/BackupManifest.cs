using System.Text.Json.Serialization;

namespace FH.LanguageComboTool.Core.Models;

public sealed record BackupManifest
{
    public string ToolVersion { get; init; } = "";
    public string Game { get; init; } = "";
    public string Channel { get; init; } = "";
    public string GameRoot { get; init; } = "";
    public string ResourceDirectory { get; init; } = "";
    public string VoiceLanguage { get; init; } = "";
    public string TextLanguage { get; init; } = "";
    public string TargetFile { get; init; } = "";
    public string SourceFile { get; init; } = "";
    public string CreatedAt { get; init; } = "";
    public List<BackupFileEntry> Files { get; init; } = [];
    public string? ManifestPath { get; init; }
    public string? OriginalSteamLanguage { get; init; }
    public string? OriginalUserPreferredLang { get; init; }
    public string? AppliedSha256 { get; init; }
}

public sealed record BackupFileEntry
{
    public string Path { get; init; } = "";
    public string OriginalSha256 { get; init; } = "";
}

public sealed record BackupInfo(
    string Id,
    string Game,
    string VoiceLanguage,
    string TextLanguage,
    string CreatedAt,
    string Path,
    bool Valid);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(BackupManifest))]
internal sealed partial class BackupJsonContext : JsonSerializerContext;
