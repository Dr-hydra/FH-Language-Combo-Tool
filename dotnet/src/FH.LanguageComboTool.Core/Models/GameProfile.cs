namespace FH.LanguageComboTool.Core.Models;

public sealed record GameProfile(
    GameId GameId,
    string DisplayName,
    string Channel,
    string SteamAppId,
    string RootPath,
    string ResourcePath,
    string ExecutableName,
    string? ManifestPath);
