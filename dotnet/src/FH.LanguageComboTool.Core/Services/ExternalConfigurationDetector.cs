using System.IO.Compression;
using System.Text;
using FH.LanguageComboTool.Core.Models;

namespace FH.LanguageComboTool.Core.Services;

internal static class ExternalConfigurationDetector
{
    private const int MaxCharactersToInspect = 4_000_000;

    public static ConfigStatus? Detect(string gameId, string resourcePath)
    {
        var packs = ResourceScanner.ScanStringTables(resourcePath)
            .Where(pack => pack.Readable && !string.IsNullOrWhiteSpace(pack.Sha256))
            .ToList();

        var duplicate = packs
            .GroupBy(pack => pack.Sha256, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            var codes = duplicate
                .Select(pack => pack.Code.ToUpperInvariant())
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new("external_duplicate", gameId, codes[0], codes[1], null);
        }

        var japanesePath = FindPackPath(packs, "JP");
        var simplifiedChinesePath = FindPackPath(packs, "CHS");
        if (japanesePath is null || simplifiedChinesePath is null)
            return null;

        ScriptProfile jpContent;
        ScriptProfile chsContent;
        try
        {
            jpContent = AnalyzeScripts(japanesePath);
            chsContent = AnalyzeScripts(simplifiedChinesePath);
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }

        if (jpContent.IsChinese && chsContent.IsJapanese)
            return new("external_swap", gameId, "JP", "CHS", null);

        return null;
    }

    private static string? FindPackPath(IEnumerable<LanguagePack> packs, string code) =>
        packs.FirstOrDefault(pack =>
            string.Equals(pack.Code, code, StringComparison.OrdinalIgnoreCase))?.Path;

    private static ScriptProfile AnalyzeScripts(string path)
    {
        long kana = 0;
        long cjk = 0;
        var inspected = 0;

        using var archive = ZipFile.OpenRead(path);
        var buffer = new char[8192];
        foreach (var entry in archive.Entries)
        {
            if (inspected >= MaxCharactersToInspect)
                break;

            using var stream = entry.Open();
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: buffer.Length,
                leaveOpen: false);

            int read;
            while (inspected < MaxCharactersToInspect &&
                   (read = reader.Read(buffer, 0, Math.Min(buffer.Length, MaxCharactersToInspect - inspected))) > 0)
            {
                inspected += read;
                for (var i = 0; i < read; i++)
                {
                    var value = buffer[i];
                    if (IsKana(value))
                        kana++;
                    else if (IsCjk(value))
                        cjk++;
                }
            }
        }

        return new ScriptProfile(kana, cjk);
    }

    private static bool IsKana(char value) =>
        value is >= '\u3040' and <= '\u30ff' or >= '\uff66' and <= '\uff9d';

    private static bool IsCjk(char value) =>
        value is >= '\u3400' and <= '\u4dbf' or >= '\u4e00' and <= '\u9fff';

    private sealed record ScriptProfile(long Kana, long Cjk)
    {
        public bool IsJapanese => Kana >= 1_000 && Kana > Cjk;
        public bool IsChinese => Cjk >= 1_000 && Kana * 50 < Cjk;
    }
}
