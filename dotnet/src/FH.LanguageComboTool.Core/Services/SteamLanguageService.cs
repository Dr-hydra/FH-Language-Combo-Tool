using FH.LanguageComboTool.Core.Models;

namespace FH.LanguageComboTool.Core.Services;

public static class SteamLanguageService
{
    public static string? CodeToSteamLanguage(string code) => code.ToUpperInvariant() switch
    {
        "EN" or "GB" => "english",
        "JP" => "japanese",
        "CHS" => "schinese",
        "CHT" => "tchinese",
        "FR" => "french",
        "DE" => "german",
        "ES" => "spanish",
        "MX" => "latam",
        "IT" => "italian",
        "PT" => "portuguese",
        "BR" => "brazilian",
        "KO" => "korean",
        "RU" => "russian",
        "PL" => "polish",
        "NL" => "dutch",
        "TR" => "turkish",
        "DK" => "danish",
        "SV" => "swedish",
        "NO" => "norwegian",
        "FI" => "finnish",
        "CZ" => "czech",
        "HU" => "hungarian",
        "EL" => "greek",
        _ => null
    };

    public static string? ReadManifestLanguage(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            return null;

        return File.ReadLines(manifestPath)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("\"language\"", StringComparison.OrdinalIgnoreCase))
            .Select(ExtractSecondQuotedValue)
            .FirstOrDefault(value => value is not null);
    }

    public static string SetManifestLanguage(string manifestPath, string newLanguage)
    {
        var lines = File.ReadAllLines(manifestPath).ToList();
        string? oldLanguage = null;
        var updated = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (!trimmed.StartsWith("\"language\"", StringComparison.OrdinalIgnoreCase))
                continue;

            oldLanguage ??= ExtractSecondQuotedValue(trimmed);
            var indent = lines[i][..(lines[i].Length - lines[i].TrimStart().Length)];
            lines[i] = $"{indent}\"language\"\t\t\"{newLanguage}\"";
            updated = true;
        }

        if (!updated)
            InsertManifestLanguage(lines, newLanguage);

        WriteManifestAtomically(manifestPath, lines);
        return oldLanguage ?? "";
    }

    public static bool RemoveManifestLanguage(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            return false;

        var lines = File.ReadAllLines(manifestPath).ToList();
        var removed = lines.RemoveAll(line =>
            line.TrimStart().StartsWith("\"language\"", StringComparison.OrdinalIgnoreCase)) > 0;

        if (removed)
            WriteManifestAtomically(manifestPath, lines);

        return removed;
    }

    public static string? GetUserPreferredLangPath(GameId gameId, string? localAppDataRoot = null)
    {
        var localAppData = localAppDataRoot ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            return null;

        var dirName = gameId == GameId.Fh5 ? "ForzaHorizon5" : "ForzaHorizon6";
        return Path.Combine(localAppData, dirName, "UserPreferredLang");
    }

    public static string? ReadUserPreferredLang(GameId gameId, string? localAppDataRoot = null)
    {
        var path = GetUserPreferredLangPath(gameId, localAppDataRoot);
        return path is not null && File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    public static string? SetUserPreferredLang(GameId gameId, string langCode, string? localAppDataRoot = null)
    {
        var path = GetUserPreferredLangPath(gameId, localAppDataRoot);
        if (path is null)
            return null;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var old = File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        File.WriteAllText(path, langCode);
        return old;
    }

    public static bool RemoveUserPreferredLang(GameId gameId, string? localAppDataRoot = null)
    {
        var path = GetUserPreferredLangPath(gameId, localAppDataRoot);
        if (path is null || !File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    private static void InsertManifestLanguage(List<string> lines, string newLanguage)
    {
        var userConfigLine = lines.FindIndex(line =>
            string.Equals(line.Trim(), "\"UserConfig\"", StringComparison.OrdinalIgnoreCase));

        if (userConfigLine >= 0)
        {
            var openBrace = FindNextLine(lines, userConfigLine + 1, line => line.Trim() == "{");
            if (openBrace >= 0)
            {
                var closeBrace = FindMatchingCloseBrace(lines, openBrace);
                if (closeBrace >= 0)
                {
                    var indent = lines[openBrace][..(lines[openBrace].Length - lines[openBrace].TrimStart().Length)] + "\t";
                    lines.Insert(closeBrace, $"{indent}\"language\"\t\t\"{newLanguage}\"");
                    return;
                }
            }
        }

        var rootClose = lines.FindLastIndex(line => line.Trim() == "}");
        if (rootClose < 0)
            throw new InvalidDataException("Steam appmanifest 结构无效，无法写入语言设置。");

        lines.InsertRange(
            rootClose,
            [
                "\t\"UserConfig\"",
                "\t{",
                $"\t\t\"language\"\t\t\"{newLanguage}\"",
                "\t}"
            ]);
    }

    private static int FindNextLine(List<string> lines, int start, Func<string, bool> predicate)
    {
        for (var i = start; i < lines.Count; i++)
        {
            if (predicate(lines[i]))
                return i;
        }

        return -1;
    }

    private static int FindMatchingCloseBrace(List<string> lines, int openBrace)
    {
        var depth = 0;
        for (var i = openBrace; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed == "{")
                depth++;
            else if (trimmed == "}" && --depth == 0)
                return i;
        }

        return -1;
    }

    private static void WriteManifestAtomically(string manifestPath, IReadOnlyCollection<string> lines)
    {
        var tempPath = manifestPath + $".fhlt.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllLines(tempPath, lines);
            File.Move(tempPath, manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static string? ExtractSecondQuotedValue(string line)
    {
        var values = new List<string>();
        var start = -1;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] != '"')
                continue;

            if (start < 0)
            {
                start = i + 1;
            }
            else
            {
                values.Add(line[start..i]);
                start = -1;
            }
        }

        return values.Count >= 2 ? values[1] : null;
    }
}
