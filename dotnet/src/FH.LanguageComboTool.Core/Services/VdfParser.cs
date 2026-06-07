namespace FH.LanguageComboTool.Core.Services;

public static class VdfParser
{
    public static string? ExtractValue(string content, string key)
    {
        foreach (var token in Tokenize(content))
        {
            if (token is KeyValueToken kv && string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        return null;
    }

    public static IReadOnlyList<string> ExtractLibraryPaths(string content)
    {
        var paths = new List<string>();
        var depth = 0;
        var insideEntry = false;

        foreach (var token in Tokenize(content))
        {
            switch (token)
            {
                case BraceOpenToken:
                    depth++;
                    if (depth == 2)
                        insideEntry = true;
                    break;
                case BraceCloseToken:
                    if (depth == 2)
                        insideEntry = false;
                    depth = Math.Max(0, depth - 1);
                    break;
                case KeyValueToken kv when insideEntry && depth == 2 && kv.Key == "path":
                    paths.Add(kv.Value);
                    break;
            }
        }

        return paths;
    }

    private static IEnumerable<VdfToken> Tokenize(string content)
    {
        var i = 0;
        while (i < content.Length)
        {
            SkipWhitespaceAndComments(content, ref i);
            if (i >= content.Length)
                yield break;

            if (content[i] == '{')
            {
                i++;
                yield return new BraceOpenToken();
            }
            else if (content[i] == '}')
            {
                i++;
                yield return new BraceCloseToken();
            }
            else if (content[i] == '"')
            {
                var key = ReadQuotedString(content, ref i);
                SkipWhitespaceAndComments(content, ref i);
                if (i < content.Length && content[i] == '"')
                    yield return new KeyValueToken(key, ReadQuotedString(content, ref i));
            }
            else
            {
                i++;
            }
        }
    }

    private static void SkipWhitespaceAndComments(string content, ref int i)
    {
        while (i < content.Length)
        {
            if (char.IsWhiteSpace(content[i]))
            {
                i++;
            }
            else if (content[i] == '/' && i + 1 < content.Length && content[i + 1] == '/')
            {
                while (i < content.Length && content[i] != '\n')
                    i++;
            }
            else
            {
                return;
            }
        }
    }

    private static string ReadQuotedString(string content, ref int i)
    {
        if (content[i] != '"')
            throw new FormatException("VDF 字符串缺少起始引号。");

        i++;
        var start = i;
        while (i < content.Length && content[i] != '"')
        {
            if (content[i] == '\\' && i + 1 < content.Length)
                i++;
            i++;
        }

        var value = content[start..i].Replace(@"\\", @"\");
        if (i < content.Length)
            i++;
        return value;
    }

    private abstract record VdfToken;
    private sealed record KeyValueToken(string Key, string Value) : VdfToken;
    private sealed record BraceOpenToken : VdfToken;
    private sealed record BraceCloseToken : VdfToken;
}
