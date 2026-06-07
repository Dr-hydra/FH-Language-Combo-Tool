namespace FH.LanguageComboTool.Core.Models;

public sealed record LanguagePack(
    string Code,
    string DisplayName,
    string FileName,
    string Path,
    long Size,
    string Sha256,
    DateTimeOffset? ModifiedAt,
    bool Readable,
    bool Writable);
