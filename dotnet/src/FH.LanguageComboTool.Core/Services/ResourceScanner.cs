using System.Security.Cryptography;
using FH.LanguageComboTool.Core.Models;

namespace FH.LanguageComboTool.Core.Services;

public static class ResourceScanner
{
    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static IReadOnlyList<LanguagePack> ScanStringTables(string resourcePath)
    {
        if (!Directory.Exists(resourcePath))
            throw new DirectoryNotFoundException($"找不到语言资源目录：{resourcePath}");

        return Directory.EnumerateFiles(resourcePath, "*.zip")
            .Select(ToLanguagePack)
            .OrderBy(pack => pack.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static LanguagePack ToLanguagePack(string path)
    {
        var info = new FileInfo(path);
        var code = Path.GetFileNameWithoutExtension(path);
        var readable = CanOpen(path, FileAccess.Read);
        var writable = CanOpen(path, FileAccess.Write);

        return new LanguagePack(
            code,
            LanguageMapper.GetDisplayName(code),
            Path.GetFileName(path),
            path,
            info.Length,
            readable ? ComputeSha256(path) : "",
            info.LastWriteTimeUtc == DateTime.MinValue ? null : new DateTimeOffset(info.LastWriteTimeUtc),
            readable,
            writable);
    }

    private static bool CanOpen(string path, FileAccess access)
    {
        try
        {
            using var _ = File.Open(path, FileMode.Open, access, FileShare.ReadWrite);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
