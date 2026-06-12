using FH.LanguageComboTool.Core.Models;
using FH.LanguageComboTool.Core.Services;
using System.IO.Compression;

namespace FH.LanguageComboTool.Core.Tests;

[TestClass]
public sealed class CoreServiceTests
{
    [TestMethod]
    public void VdfParserExtractsLibraryPathsAndValues()
    {
        const string libraries = """
            "libraryfolders"
            {
                "0"
                {
                    "path" "C:\\Program Files (x86)\\Steam"
                }
                "1"
                {
                    "path" "D:\\SteamLibrary"
                }
            }
            """;

        const string manifest = """
            "AppState"
            {
                "appid" "1551360"
                "installdir" "ForzaHorizon5"
            }
            """;

        CollectionAssert.AreEqual(
            new[] { @"C:\Program Files (x86)\Steam", @"D:\SteamLibrary" },
            VdfParser.ExtractLibraryPaths(libraries).ToArray());
        Assert.AreEqual("ForzaHorizon5", VdfParser.ExtractValue(manifest, "installdir"));
    }

    [TestMethod]
    public void GameDetectorFindsFh5AndFh6AcrossSteamLibraries()
    {
        using var temp = TestDirectory.Create();
        var steamRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "Steam")).FullName;
        var secondLibrary = Directory.CreateDirectory(Path.Combine(temp.Path, "SteamLibrary")).FullName;
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        Directory.CreateDirectory(Path.Combine(secondLibrary, "steamapps"));

        File.WriteAllText(
            Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
            $$"""
            "libraryfolders"
            {
                "0"
                {
                    "path" "{{steamRoot.Replace("\\", "\\\\")}}"
                }
                "1"
                {
                    "path" "{{secondLibrary.Replace("\\", "\\\\")}}"
                }
            }
            """);

        CreateSteamGame(
            steamRoot,
            GameDetector.Fh5SteamAppId,
            "ForzaHorizon5",
            GameDetector.Fh5Executable);
        CreateSteamGame(
            secondLibrary,
            GameDetector.Fh6SteamAppId,
            "ForzaHorizon6",
            GameDetector.Fh6Executable);

        var games = new GameDetector().DetectSteamGames(steamRoot);

        Assert.HasCount(2, games);
        Assert.IsTrue(games.Any(game => game.GameId == GameId.Fh5));
        Assert.IsTrue(games.Any(game => game.GameId == GameId.Fh6));
        Assert.IsTrue(games.All(game => game.ManifestPath is not null));
    }

    [TestMethod]
    public void GameDetectorFindsFh6InXboxGamesDirectory()
    {
        using var temp = TestDirectory.Create();
        var installRoot = CreateXboxGame(temp.Path, useContentDirectory: false);

        var games = new GameDetector().DetectXboxGames([temp.Path]);

        Assert.HasCount(1, games);
        Assert.AreEqual(GameId.Fh6, games[0].GameId);
        Assert.AreEqual(GameDetector.XboxChannel, games[0].Channel);
        Assert.AreEqual("", games[0].SteamAppId);
        Assert.AreEqual(installRoot, games[0].RootPath);
        Assert.IsNull(games[0].ManifestPath);
    }

    [TestMethod]
    public void GameDetectorSupportsXboxContentSubdirectoryAndManualInference()
    {
        using var temp = TestDirectory.Create();
        var contentRoot = CreateXboxGame(temp.Path, useContentDirectory: true);
        var installRoot = Directory.GetParent(contentRoot)!.FullName;

        var profile = new GameDetector().ValidateGameDirectory(installRoot, GameId.Fh6);

        Assert.AreEqual(GameDetector.XboxChannel, profile.Channel);
        Assert.AreEqual(contentRoot, profile.RootPath);
        Assert.AreEqual(
            Path.Combine(contentRoot, GameDetector.ResourceSubpath),
            profile.ResourcePath);
    }

    [TestMethod]
    public void LanguageMapperResolvesFilesCaseInsensitivelyAndBuildsPlan()
    {
        using var temp = TestDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "EN.zip"), "voice");
        File.WriteAllText(Path.Combine(temp.Path, "chs.zip"), "text");

        Assert.AreEqual("EN.zip", LanguageMapper.ResolveFileName("en", temp.Path));
        Assert.AreEqual("chs.zip", LanguageMapper.ResolveFileName("CHS", temp.Path));

        var plan = LanguageMapper.GenerateApplyPlan("fh5", "en", "CHS", temp.Path, temp.Path, null);

        Assert.AreEqual("EN", plan.VoiceLanguage);
        Assert.AreEqual("CHS", plan.TextLanguage);
        Assert.AreEqual("chs.zip", plan.SourceFile);
        Assert.AreEqual("EN.zip", plan.TargetFile);
        Assert.AreEqual("english", plan.SteamLanguage);
        Assert.HasCount(2, plan.Operations);
    }

    [TestMethod]
    public void ResourceScannerComputesSha256AndScansZipPacks()
    {
        using var temp = TestDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "EN.zip"), "hello world");
        File.WriteAllText(Path.Combine(temp.Path, "CHS.zip"), "data");
        File.WriteAllText(Path.Combine(temp.Path, "ignore.txt"), "data");

        var hash = ResourceScanner.ComputeSha256(Path.Combine(temp.Path, "EN.zip"));
        Assert.AreEqual("b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", hash);

        var packs = ResourceScanner.ScanStringTables(temp.Path);
        Assert.HasCount(2, packs);
        Assert.IsTrue(packs.All(pack => pack.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void BackupApplyStatusAndRestoreFlowWorksOnTempFiles()
    {
        using var temp = TestDirectory.Create();
        var resource = Directory.CreateDirectory(Path.Combine(temp.Path, "resources")).FullName;
        var backupRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "backups")).FullName;
        var source = Path.Combine(resource, "CHS.zip");
        var target = Path.Combine(resource, "EN.zip");
        var userData = Directory.CreateDirectory(Path.Combine(temp.Path, "userdata")).FullName;
        var manifestPath = Path.Combine(temp.Path, "appmanifest_1551360.acf");
        File.WriteAllText(source, "chinese-text-v1");
        File.WriteAllText(target, "english-original");
        File.WriteAllText(
            manifestPath,
            """
            "AppState"
            {
                "appid" "1551360"
                "UserConfig"
                {
                }
            }
            """);

        var backupManager = new BackupManager();
        var plan = LanguageMapper.GenerateApplyPlan("fh5", "EN", "CHS", resource, backupRoot, manifestPath);
        var profile = new GameProfile(
            GameId.Fh5,
            "Forza Horizon 5",
            "steam",
            GameDetector.Fh5SteamAppId,
            temp.Path,
            resource,
            GameDetector.Fh5Executable,
            manifestPath);

        var applyResult = new ApplyEngine(backupManager).ExecuteApply(plan, profile, backupRoot, userData);

        Assert.IsTrue(applyResult.Success, applyResult.Message);
        Assert.AreEqual("chinese-text-v1", File.ReadAllText(target));
        Assert.AreEqual("english", SteamLanguageService.ReadManifestLanguage(manifestPath));
        Assert.AreEqual("EN", SteamLanguageService.ReadUserPreferredLang(GameId.Fh5, userData));

        var status = new StatusService(backupManager).GetStatus("fh5", resource, backupRoot);
        Assert.AreEqual("applied", status.State);

        File.WriteAllText(source, "chinese-text-v2");
        status = new StatusService(backupManager).GetStatus("fh5", resource, backupRoot);
        Assert.AreEqual("outdated", status.State);

        var restoreResult = new RestoreEngine(backupManager).ExecuteRestore(applyResult.BackupPath!, userData);

        Assert.IsTrue(restoreResult.Success, restoreResult.Message);
        Assert.AreEqual("english-original", File.ReadAllText(target));
        Assert.IsNull(SteamLanguageService.ReadManifestLanguage(manifestPath));
        Assert.IsNull(SteamLanguageService.ReadUserPreferredLang(GameId.Fh5, userData));
    }

    [TestMethod]
    public void StatusClassificationMatchesLegacyBehavior()
    {
        Assert.AreEqual("applied", StatusService.ClassifyState("text-v2", "text-v2", "orig", "text-v1"));
        Assert.AreEqual("reverted", StatusService.ClassifyState("orig", "text-v2", "orig", "text-v1"));
        Assert.AreEqual("outdated", StatusService.ClassifyState("text-v1", "text-v2", "orig", "text-v1"));
        Assert.AreEqual("modified", StatusService.ClassifyState("other", "text-v2", "orig", "text-v1"));
        Assert.AreEqual("modified", StatusService.ClassifyState("text-v1", "text-v2", "orig", null));
    }

    [TestMethod]
    public void StatusDetectsExternalJapaneseChineseSwapWithoutBackup()
    {
        using var temp = TestDirectory.Create();
        var resource = Directory.CreateDirectory(Path.Combine(temp.Path, "resources")).FullName;
        var backupRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "backups")).FullName;
        CreateTextZip(Path.Combine(resource, "JP.zip"), string.Concat(Enumerable.Repeat("简体中文文本", 500)));
        CreateTextZip(Path.Combine(resource, "CHS.zip"), string.Concat(Enumerable.Repeat("日本語のテキストです", 500)));

        var status = new StatusService(new BackupManager()).GetStatus("fh6", resource, backupRoot);

        Assert.AreEqual("external_swap", status.State);
        Assert.AreEqual("JP", status.VoiceLanguage);
        Assert.AreEqual("CHS", status.TextLanguage);
    }

    [TestMethod]
    public void StatusDetectsExternalDuplicateWithoutBackup()
    {
        using var temp = TestDirectory.Create();
        var resource = Directory.CreateDirectory(Path.Combine(temp.Path, "resources")).FullName;
        var backupRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "backups")).FullName;
        CreateTextZip(Path.Combine(resource, "JP.zip"), "same-content");
        File.Copy(Path.Combine(resource, "JP.zip"), Path.Combine(resource, "CHS.zip"));

        var status = new StatusService(new BackupManager()).GetStatus("fh6", resource, backupRoot);

        Assert.AreEqual("external_duplicate", status.State);
        CollectionAssert.AreEquivalent(
            new[] { "JP", "CHS" },
            new[] { status.VoiceLanguage!, status.TextLanguage! });
    }

    private static void CreateTextZip(string path, string content)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("sample.str");
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    [TestMethod]
    public void ReapplyRestoresTrueOriginalBeforeCreatingFreshBackup()
    {
        using var temp = TestDirectory.Create();
        var resource = Directory.CreateDirectory(Path.Combine(temp.Path, "resources")).FullName;
        var backupRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "backups")).FullName;
        var source = Path.Combine(resource, "CHS.zip");
        var target = Path.Combine(resource, "EN.zip");
        var userData = Directory.CreateDirectory(Path.Combine(temp.Path, "userdata")).FullName;
        File.WriteAllText(source, "chinese-text-v1");
        File.WriteAllText(target, "english-original");

        var backupManager = new BackupManager();
        var applyEngine = new ApplyEngine(backupManager);
        var restoreEngine = new RestoreEngine(backupManager);
        var statusService = new StatusService(backupManager);
        var configurationEngine = new ConfigurationEngine(
            backupManager,
            statusService,
            restoreEngine,
            applyEngine);
        var profile = new GameProfile(
            GameId.Fh5,
            "Forza Horizon 5",
            "steam",
            GameDetector.Fh5SteamAppId,
            temp.Path,
            resource,
            GameDetector.Fh5Executable,
            null);

        var firstPlan = LanguageMapper.GenerateApplyPlan("fh5", "EN", "CHS", resource, backupRoot, null);
        var firstApply = applyEngine.ExecuteApply(firstPlan, profile, backupRoot, userData);
        Assert.IsTrue(firstApply.Success, firstApply.Message);

        File.WriteAllText(source, "chinese-text-v2");
        var reapply = new ReapplyEngine(backupManager, configurationEngine)
            .ExecuteReapply(profile, backupRoot, userData);

        Assert.IsTrue(reapply.Success, reapply.Message);
        Assert.AreEqual("chinese-text-v2", File.ReadAllText(target));
        Assert.HasCount(2, backupManager.ListBackups(backupRoot, "fh5"));

        var restoreFreshBackup = restoreEngine.ExecuteRestore(reapply.BackupPath!, userData);
        Assert.IsTrue(restoreFreshBackup.Success, restoreFreshBackup.Message);
        Assert.AreEqual("english-original", File.ReadAllText(target));
    }

    [TestMethod]
    public void RestoreRejectsManifestPathsOutsideResourceDirectory()
    {
        using var temp = TestDirectory.Create();
        var backupPath = Directory.CreateDirectory(Path.Combine(temp.Path, "backup")).FullName;
        var originalPath = Directory.CreateDirectory(Path.Combine(backupPath, "original")).FullName;
        var resourcePath = Directory.CreateDirectory(Path.Combine(temp.Path, "resources")).FullName;
        var originalFile = Path.Combine(originalPath, "EN.zip");
        File.WriteAllText(originalFile, "original");
        var hash = ResourceScanner.ComputeSha256(originalFile);

        File.WriteAllText(
            Path.Combine(backupPath, "manifest.json"),
            $$"""
              {
                "toolVersion": "1.2.0",
                "game": "fh5",
                "channel": "steam",
                "gameRoot": "{{temp.Path.Replace("\\", "\\\\")}}",
                "resourceDirectory": "{{resourcePath.Replace("\\", "\\\\")}}",
                "voiceLanguage": "EN",
                "textLanguage": "CHS",
                "targetFile": "..\\outside.zip",
                "sourceFile": "CHS.zip",
                "createdAt": "2026-06-06T00:00:00+08:00",
                "files": [
                  {
                    "path": "EN.zip",
                    "originalSha256": "{{hash}}"
                  }
                ]
              }
              """);

        var result = new RestoreEngine(new BackupManager()).ExecuteRestore(backupPath);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Message, "路径");
        Assert.IsFalse(File.Exists(Path.Combine(temp.Path, "outside.zip")));
    }

    [TestMethod]
    public void SteamLanguageSettingsCanBeCreatedAndRemovedWithoutTouchingRealUserData()
    {
        using var temp = TestDirectory.Create();
        var manifestPath = Path.Combine(temp.Path, "appmanifest_1551360.acf");
        File.WriteAllText(
            manifestPath,
            """
            "AppState"
            {
                "appid" "1551360"
                "UserConfig"
                {
                }
            }
            """);

        var oldLanguage = SteamLanguageService.SetManifestLanguage(manifestPath, "schinese");

        Assert.AreEqual("", oldLanguage);
        Assert.AreEqual("schinese", SteamLanguageService.ReadManifestLanguage(manifestPath));
        Assert.IsTrue(SteamLanguageService.RemoveManifestLanguage(manifestPath));
        Assert.IsNull(SteamLanguageService.ReadManifestLanguage(manifestPath));

        Assert.IsNull(SteamLanguageService.ReadUserPreferredLang(GameId.Fh5, temp.Path));
        SteamLanguageService.SetUserPreferredLang(GameId.Fh5, "CHS", temp.Path);
        Assert.AreEqual("CHS", SteamLanguageService.ReadUserPreferredLang(GameId.Fh5, temp.Path));
        Assert.IsTrue(SteamLanguageService.RemoveUserPreferredLang(GameId.Fh5, temp.Path));
        Assert.IsNull(SteamLanguageService.ReadUserPreferredLang(GameId.Fh5, temp.Path));
    }

    [TestMethod]
    public void BackupListMarksTamperedBackupInvalid()
    {
        using var temp = TestDirectory.Create();
        var resource = Directory.CreateDirectory(Path.Combine(temp.Path, "resources")).FullName;
        var backupRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "backups")).FullName;
        var target = Path.Combine(resource, "EN.zip");
        File.WriteAllText(target, "english-original");

        var manager = new BackupManager();
        var backupPath = manager.CreateBackup(
            backupRoot,
            "fh5",
            "steam",
            temp.Path,
            resource,
            "EN",
            "CHS",
            target,
            "CHS.zip",
            null,
            null,
            null,
            null);

        File.WriteAllText(Path.Combine(backupPath, "original", "EN.zip"), "tampered");

        var backups = manager.ListBackups(backupRoot, "fh5");
        Assert.HasCount(1, backups);
        Assert.IsFalse(backups[0].Valid);
    }

    [TestMethod]
    public void ApplyingSameActiveCombinationDoesNotCreateNestedBackup()
    {
        using var temp = TestDirectory.Create();
        var resource = Directory.CreateDirectory(Path.Combine(temp.Path, "resources")).FullName;
        var backupRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "backups")).FullName;
        var userData = Directory.CreateDirectory(Path.Combine(temp.Path, "userdata")).FullName;
        File.WriteAllText(Path.Combine(resource, "CHS.zip"), "chinese-text");
        File.WriteAllText(Path.Combine(resource, "EN.zip"), "english-original");

        var manager = new BackupManager();
        var apply = new ApplyEngine(manager);
        var restore = new RestoreEngine(manager);
        var status = new StatusService(manager);
        var configuration = new ConfigurationEngine(manager, status, restore, apply);
        var profile = CreateProfile(temp.Path, resource);

        var first = configuration.ExecuteApply(profile, "EN", "CHS", backupRoot, userData);
        var second = configuration.ExecuteApply(profile, "EN", "CHS", backupRoot, userData);

        Assert.IsTrue(first.Success, first.Message);
        Assert.IsTrue(second.Success, second.Message);
        Assert.HasCount(1, manager.ListBackups(backupRoot, "fh5"));
        Assert.AreEqual("chinese-text", File.ReadAllText(Path.Combine(resource, "EN.zip")));
    }

    [TestMethod]
    public void SwitchingVoiceTargetRestoresPreviousTargetBeforeApplyingNewCombination()
    {
        using var temp = TestDirectory.Create();
        var resource = Directory.CreateDirectory(Path.Combine(temp.Path, "resources")).FullName;
        var backupRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "backups")).FullName;
        var userData = Directory.CreateDirectory(Path.Combine(temp.Path, "userdata")).FullName;
        File.WriteAllText(Path.Combine(resource, "CHS.zip"), "chinese-text");
        File.WriteAllText(Path.Combine(resource, "EN.zip"), "english-original");
        File.WriteAllText(Path.Combine(resource, "JP.zip"), "japanese-original");

        var manager = new BackupManager();
        var apply = new ApplyEngine(manager);
        var restore = new RestoreEngine(manager);
        var status = new StatusService(manager);
        var configuration = new ConfigurationEngine(manager, status, restore, apply);
        var profile = CreateProfile(temp.Path, resource);

        var first = configuration.ExecuteApply(profile, "EN", "CHS", backupRoot, userData);
        var second = configuration.ExecuteApply(profile, "JP", "CHS", backupRoot, userData);

        Assert.IsTrue(first.Success, first.Message);
        Assert.IsTrue(second.Success, second.Message);
        Assert.AreEqual("english-original", File.ReadAllText(Path.Combine(resource, "EN.zip")));
        Assert.AreEqual("chinese-text", File.ReadAllText(Path.Combine(resource, "JP.zip")));

        var restored = restore.ExecuteRestore(second.BackupPath!, userData);
        Assert.IsTrue(restored.Success, restored.Message);
        Assert.AreEqual("japanese-original", File.ReadAllText(Path.Combine(resource, "JP.zip")));
    }

    [TestMethod]
    public void StatusIgnoresBackupsFromPreviousInstallDirectory()
    {
        using var temp = TestDirectory.Create();
        var oldResource = Directory.CreateDirectory(Path.Combine(temp.Path, "old", "resources")).FullName;
        var currentResource = Directory.CreateDirectory(Path.Combine(temp.Path, "current", "resources")).FullName;
        var backupRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "backups")).FullName;
        File.WriteAllText(Path.Combine(oldResource, "CHS.zip"), "old-text");
        File.WriteAllText(Path.Combine(oldResource, "EN.zip"), "old-original");
        File.WriteAllText(Path.Combine(currentResource, "CHS.zip"), "current-text");
        File.WriteAllText(Path.Combine(currentResource, "EN.zip"), "current-original");

        var manager = new BackupManager();
        manager.CreateBackup(
            backupRoot,
            "fh5",
            "steam",
            Path.GetDirectoryName(oldResource)!,
            oldResource,
            "EN",
            "CHS",
            Path.Combine(oldResource, "EN.zip"),
            "CHS.zip",
            null,
            null,
            null,
            ResourceScanner.ComputeSha256(Path.Combine(oldResource, "CHS.zip")));

        var status = new StatusService(manager).GetStatus("fh5", currentResource, backupRoot);

        Assert.AreEqual("none", status.State);
    }

    private static GameProfile CreateProfile(string root, string resource) =>
        new(
            GameId.Fh5,
            "Forza Horizon 5",
            "steam",
            GameDetector.Fh5SteamAppId,
            root,
            resource,
            GameDetector.Fh5Executable,
            null);

    private static void CreateSteamGame(
        string libraryRoot,
        string appId,
        string installDirectory,
        string executableName)
    {
        var steamApps = Directory.CreateDirectory(Path.Combine(libraryRoot, "steamapps")).FullName;
        File.WriteAllText(
            Path.Combine(steamApps, $"appmanifest_{appId}.acf"),
            $$"""
            "AppState"
            {
                "appid" "{{appId}}"
                "installdir" "{{installDirectory}}"
            }
            """);

        var gameRoot = Directory.CreateDirectory(
            Path.Combine(steamApps, "common", installDirectory)).FullName;
        File.WriteAllText(Path.Combine(gameRoot, executableName), "");
        var resource = Directory.CreateDirectory(
            Path.Combine(gameRoot, GameDetector.ResourceSubpath)).FullName;
        File.WriteAllText(Path.Combine(resource, "EN.zip"), "english");
        File.WriteAllText(Path.Combine(resource, "CHS.zip"), "chinese");
    }

    private static string CreateXboxGame(string driveRoot, bool useContentDirectory)
    {
        var installRoot = Directory.CreateDirectory(Path.Combine(
            driveRoot,
            GameDetector.XboxGamesDirectory,
            GameDetector.Fh6XboxInstallDirectory)).FullName;
        var gameRoot = useContentDirectory
            ? Directory.CreateDirectory(Path.Combine(installRoot, "Content")).FullName
            : installRoot;

        File.WriteAllText(Path.Combine(gameRoot, GameDetector.Fh6Executable), "");
        var resource = Directory.CreateDirectory(
            Path.Combine(gameRoot, GameDetector.ResourceSubpath)).FullName;
        File.WriteAllText(Path.Combine(resource, "EN.zip"), "english");
        File.WriteAllText(Path.Combine(resource, "CHS.zip"), "chinese");
        return gameRoot;
    }

    private sealed class TestDirectory : IDisposable
    {
        private TestDirectory(string path) => Path = path;

        public string Path { get; }

        public static TestDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "fh-language-combo-tool-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TestDirectory(path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { }
        }
    }
}
