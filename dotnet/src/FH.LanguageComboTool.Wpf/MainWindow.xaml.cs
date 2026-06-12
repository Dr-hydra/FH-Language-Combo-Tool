using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using FH.LanguageComboTool.Core.Models;
using FH.LanguageComboTool.Core.Services;
using Microsoft.Win32;

namespace FH.LanguageComboTool.Wpf;

public partial class MainWindow : Window
{
    private readonly BackupManager _backupManager = new();
    private readonly GameDetector _gameDetector = new();
    private readonly AppSettingsService _settingsService = new();
    private readonly LocalizationService _localization = new();
    private readonly ApplyEngine _applyEngine;
    private readonly RestoreEngine _restoreEngine;
    private readonly ConfigurationEngine _configurationEngine;
    private readonly ReapplyEngine _reapplyEngine;
    private readonly StatusService _statusService;
    private readonly WindowResizer _resizer;

    private readonly List<GameOption> _games = [];
    private readonly HashSet<string> _shownXboxLanguagePackNotices =
        new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<LanguagePack> _languagePacks = [];
    private GameProfile? _selectedGame;
    private BackupOption? _selectedBackup;
    private ConfigStatus? _currentStatus;
    private AppSettings _settings = new();
    private bool _updatingLanguageSelectors;

    public MainWindow()
    {
        InitializeComponent();
        _applyEngine = new ApplyEngine(_backupManager);
        _restoreEngine = new RestoreEngine(_backupManager);
        _statusService = new StatusService(_backupManager);
        _configurationEngine = new ConfigurationEngine(
            _backupManager,
            _statusService,
            _restoreEngine,
            _applyEngine);
        _reapplyEngine = new ReapplyEngine(_backupManager, _configurationEngine);
        _resizer = new WindowResizer(this);
        _resizer.AddBottom(ResizerB);
        _resizer.AddLeft(ResizerL);
        _resizer.AddBottomLeft(ResizerLB);
        _resizer.AddTopLeft(ResizerLT);
        _resizer.AddRight(ResizerR);
        _resizer.AddBottomRight(ResizerRB);
        _resizer.AddTopRight(ResizerRT);
        _resizer.AddTop(ResizerT);
        Loaded += (_, _) => InitializeWindow();
    }

    private void InitializeWindow()
    {
        _settings = _settingsService.Load();
        _localization.SetLanguage(_settings.UiLanguage);
        UiLanguageList.ItemsSource = _localization.SupportedLanguages;
        SettingsLanguageCombo.ItemsSource = _localization.SupportedLanguages;
        SyncLanguageSelectors();
        ApplyLocalization();

        if (string.IsNullOrWhiteSpace(_settings.UiLanguage))
        {
            ShowLanguageSelection();
            return;
        }

        if (!_settings.DisclaimerAccepted)
        {
            ShowDisclaimer();
            return;
        }

        FirstRunOverlay.Visibility = Visibility.Collapsed;
        MainContent.IsEnabled = true;
        DetectGames();
    }

    private void ApplyLocalization()
    {
        Title = _localization.T("AppName");
        WindowTitleText.Text = _localization.T("AppName");
        NavHome.Text = _localization.T("NavHome");
        NavBackups.Text = _localization.T("NavBackups");
        NavSettings.Text = _localization.T("NavSettings");
        NavAbout.Text = _localization.T("NavAbout");
        GameCard.Title = _localization.T("Game");
        InstalledGamesLabel.Text = _localization.T("InstalledGames");
        DetectButton.Text = _localization.T("Detect");
        ManualDirectoryCard.Title = _localization.T("ManualDirectory");
        ManualPathTextBox.HintText = _localization.T("ManualPathHint");
        BrowseButton.Text = _localization.T("Browse");
        ValidateManualButton.Text = _localization.T("Validate");
        BackupsCard.Title = _localization.T("Backups");
        BackupsDescriptionText.Text = _localization.T("BackupsDescription");
        RestoreButton.Text = _localization.T("RestoreSelected");
        LanguagePanel.Title = _localization.T("LanguageTitle");
        LanguageIntroText.Text = _localization.T("LanguageIntro");
        ContinueLanguageButton.Text = _localization.T("Continue");
        DisclaimerPanel.Title = _localization.T("DisclaimerTitle");
        DisclaimerIntroText.Text = _localization.T("DisclaimerIntro");
        DisclaimerCheck1.Text = _localization.T("Disclaimer1");
        DisclaimerCheck2.Text = _localization.T("Disclaimer2");
        DisclaimerCheck3.Text = _localization.T("Disclaimer3");
        DisclaimerCheck4.Text = _localization.T("Disclaimer4");
        AcceptDisclaimerButton.Text = _localization.T("Accept");
        SettingsCard.Title = _localization.T("Settings");
        InterfaceLanguageLabel.Text = _localization.T("InterfaceLanguage");
        SettingsDescriptionText.Text = _localization.T("SettingsDescription");
        ResetFirstRunButton.Text = _localization.T("ResetFirstRun");
        LanguageComboCard.Title = _localization.T("LanguageCombo");
        LanguageComboIntroText.Text = _localization.T("LanguageComboIntro");
        LanguageComboDescriptionText.Text = _localization.T("LanguageComboDescription");
        VoiceLanguageLabel.Text = _localization.T("VoiceLanguage");
        TextLanguageLabel.Text = _localization.T("TextLanguage");
        StatusCard.Title = _localization.T("StatusAndPreview");
        CurrentStatusLabel.Text = _localization.T("CurrentStatus");
        ExpectedOperationLabel.Text = _localization.T("ExpectedOperation");
        ActionsCard.Title = _localization.T("Actions");
        ApplyButton.Text = _localization.T("ApplyConfiguration");
        RefreshStatusButton.Text = _localization.T("RefreshStatus");
        ReapplyButton.Text = _localization.T("Reapply");
        AboutCard.Title = _localization.T("AboutTitle");
        AboutProductNameText.Text = _localization.T("AboutProductName");
        AboutDescriptionText.Text = _localization.T("AboutDescription");
        VersionLabelText.Text = _localization.T("Version");
        ProjectLinkLabelText.Text = _localization.T("ProjectAddress");
        VersionText.Text = GetApplicationVersion();

        if (_selectedGame is null)
        {
            GamePathText.Text = _localization.T("NoGameSelected");
            StatusText.Text = _localization.T("SelectGameToLoadStatus");
        }
        else
        {
            PopulateLanguageSelectors();
            RefreshStatus();
            RefreshBackups();
        }

        FooterText.Text = _localization.T("Ready");
        UpdatePreview();
    }

    private void ShowLanguageSelection()
    {
        MainContent.IsEnabled = false;
        FirstRunOverlay.Visibility = Visibility.Visible;
        LanguagePanel.Visibility = Visibility.Visible;
        DisclaimerPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowDisclaimer()
    {
        MainContent.IsEnabled = false;
        FirstRunOverlay.Visibility = Visibility.Visible;
        LanguagePanel.Visibility = Visibility.Collapsed;
        DisclaimerPanel.Visibility = Visibility.Visible;
        AcceptDisclaimerButton.IsEnabled = AreDisclaimerChecksAccepted();
    }

    private void Detect_Click(object sender, MouseButtonEventArgs e) => DetectGames();

    private void UiLanguageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ContinueLanguageButton.IsEnabled = UiLanguageList.SelectedItem is UiLanguage;
    }

    private void ContinueLanguage_Click(object sender, MouseButtonEventArgs e)
    {
        if (UiLanguageList.SelectedItem is not UiLanguage language)
            return;

        _settings.UiLanguage = language.Code;
        _settingsService.Save(_settings);
        _localization.SetLanguage(language.Code);
        SyncLanguageSelectors();
        ApplyLocalization();
        ShowDisclaimer();
    }

    private void SettingsLanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingLanguageSelectors ||
            SettingsLanguageCombo.SelectedItem is not UiLanguage language ||
            string.Equals(language.Code, _localization.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
            return;

        _settings.UiLanguage = language.Code;
        _settingsService.Save(_settings);
        _localization.SetLanguage(language.Code);
        SyncLanguageSelectors();
        ApplyLocalization();
    }

    private void DisclaimerCheck_Changed(object sender, bool user)
    {
        AcceptDisclaimerButton.IsEnabled = AreDisclaimerChecksAccepted();
    }

    private void AcceptDisclaimer_Click(object sender, MouseButtonEventArgs e)
    {
        if (!AreDisclaimerChecksAccepted())
            return;

        _settings.DisclaimerAccepted = true;
        _settingsService.Save(_settings);
        FirstRunOverlay.Visibility = Visibility.Collapsed;
        MainContent.IsEnabled = true;
        DetectGames();
    }

    private bool AreDisclaimerChecksAccepted() =>
        DisclaimerCheck1.Checked &&
        DisclaimerCheck2.Checked &&
        DisclaimerCheck3.Checked &&
        DisclaimerCheck4.Checked;

    private void DetectGames()
    {
        RunUi(_localization.T("DetectingGames"), () =>
        {
            _games.Clear();
            foreach (var profile in _gameDetector.DetectGames())
                _games.Add(new GameOption(profile));

            PopulateGameList();

            if (_games.Count > 0)
            {
                SelectGameItem(0);
                FooterText.Text = _localization.Format("DetectedGames", _games.Count);
            }
            else
            {
                FooterText.Text = _localization.T("NoGamesDetected");
                ClearGameDetails();
            }
        });
    }

    private void GameItem_Check(object sender, QING.UIKIT.ModBase.RouteEventArgs e)
    {
        if (sender is not QING.UIKIT.MyListItem { Tag: GameOption option })
            return;

        SelectGame(option.Profile);
    }

    private void SelectGame(GameProfile profile)
    {
        _selectedGame = profile;
        GamePathText.Text = profile.RootPath;
        FooterText.Text = _localization.Format("SelectedGame", profile.DisplayName);

        try
        {
            _languagePacks = ResourceScanner.ScanStringTables(profile.ResourcePath);
            PopulateLanguageSelectors();
            ShowXboxLanguagePackNotice(profile);

            RefreshStatus();
            RefreshBackups();
            UpdatePreview();
        }
        catch (Exception ex)
        {
            StatusText.Text = _localization.Format("ScanFailed", ex.Message);
            FooterText.Text = _localization.T("LanguageScanFailed");
        }
    }

    private void ShowXboxLanguagePackNotice(GameProfile profile)
    {
        if (!profile.Channel.Equals(GameDetector.XboxChannel, StringComparison.OrdinalIgnoreCase) ||
            !_shownXboxLanguagePackNotices.Add(profile.RootPath))
            return;

        QingDialog.Show(
            this,
            _localization.T("XboxLanguagePackNotice"),
            _localization.T("XboxLanguagePackNoticeTitle"),
            kind: QingDialogKind.Information);
    }

    private void Browse_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = _localization.T("SelectGameRootDialog")
        };

        if (dialog.ShowDialog(this) == true)
            ManualPathTextBox.Text = dialog.FolderName;
    }

    private void ValidateManual_Click(object sender, MouseButtonEventArgs e)
    {
        var path = ManualPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            QingDialog.Show(
                this,
                _localization.T("SelectGameRoot"),
                _localization.T("ManualDirectoryTitle"),
                kind: QingDialogKind.Warning);
            return;
        }

        RunUi(_localization.T("ValidatingDirectory"), () =>
        {
            var gameId = GetManualGameId();
            var profile = _gameDetector.ValidateGameDirectory(path, gameId);
            var existing = _games.FindIndex(game =>
                game.Profile.GameId == profile.GameId &&
                string.Equals(game.Profile.RootPath, profile.RootPath, StringComparison.OrdinalIgnoreCase));

            if (existing < 0)
                _games.Add(new GameOption(profile));

            PopulateGameList();
            SelectGameItem(existing >= 0 ? existing : _games.Count - 1);
            FooterText.Text = _localization.T("ManualDirectoryValidated");
        });
    }

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void Refresh_Click(object sender, MouseButtonEventArgs e)
    {
        RefreshStatus();
        RefreshBackups();
    }

    private void Apply_Click(object sender, MouseButtonEventArgs e) => ApplySelectedCombo();

    private void Reapply_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedGame is null ||
            _currentStatus?.State is not ("outdated" or "modified") ||
            _currentStatus.VoiceLanguage is null ||
            _currentStatus.TextLanguage is null)
        {
            QingDialog.Show(
                this,
                _localization.T("NoReapply"),
                _localization.T("ReapplyTitle"));
            return;
        }

        if (ProcessService.IsGameRunning(_selectedGame.GameId))
        {
            QingDialog.Show(
                this,
                _localization.T("GameRunningApply"),
                _localization.T("ReapplyTitle"),
                kind: QingDialogKind.Warning);
            return;
        }

        var confirm = QingDialog.Show(
            this,
            _localization.T("ConfirmReapply"),
            _localization.T("ConfirmReapplyTitle"),
            QingDialogButtons.YesNo,
            QingDialogKind.Question);
        if (confirm != QingDialogResult.Yes)
            return;

        RunUi(_localization.T("Reapply"), () =>
        {
            var result = _reapplyEngine.ExecuteReapply(_selectedGame);
            QingDialog.Show(
                this,
                result.Message,
                result.Success ? _localization.T("ReapplyComplete") : _localization.T("ReapplyFailed"),
                kind: result.Success ? QingDialogKind.Information : QingDialogKind.Error);
            FooterText.Text = result.Success
                ? _localization.T("ReapplyComplete")
                : _localization.T("ReapplyFailed");
            RefreshStatus();
            RefreshBackups();
        });
    }

    private void ApplySelectedCombo()
    {
        if (_selectedGame is null ||
            VoiceCombo.SelectedItem is not LanguageOption voice ||
            TextCombo.SelectedItem is not LanguageOption text)
        {
            QingDialog.Show(
                this,
                _localization.T("SelectComboFirst"),
                _localization.T("ApplyTitle"),
                kind: QingDialogKind.Warning);
            return;
        }

        if (string.Equals(voice.Pack.Code, text.Pack.Code, StringComparison.OrdinalIgnoreCase))
        {
            QingDialog.Show(
                this,
                _localization.T("SameLanguage"),
                _localization.T("ApplyTitle"),
                kind: QingDialogKind.Warning);
            return;
        }

        if (ProcessService.IsGameRunning(_selectedGame.GameId))
        {
            QingDialog.Show(
                this,
                _localization.T("GameRunningApply"),
                _localization.T("ApplyTitle"),
                kind: QingDialogKind.Warning);
            return;
        }

        var confirm = QingDialog.Show(
            this,
            _localization.Format("ConfirmApply", text.Pack.DisplayName, voice.Pack.DisplayName),
            _localization.T("ConfirmApplyTitle"),
            QingDialogButtons.YesNo,
            QingDialogKind.Question);
        if (confirm != QingDialogResult.Yes)
            return;

        RunUi(_localization.T("ApplyConfiguration"), () =>
        {
            var result = _configurationEngine.ExecuteApply(
                _selectedGame,
                voice.Pack.Code,
                text.Pack.Code);
            if (!result.Success)
            {
                QingDialog.Show(
                    this,
                    result.Message,
                    _localization.T("ApplyFailedTitle"),
                    kind: QingDialogKind.Error);
                FooterText.Text = _localization.T("ApplyFailed");
                return;
            }

            var message = result.Message;
            if (result.SteamLanguageSet)
                message += "\n" + _localization.T("SteamLanguageUpdated");
            if (!string.IsNullOrWhiteSpace(result.SteamLanguageWarning))
                message += "\n" + result.SteamLanguageWarning;

            QingDialog.Show(
                this,
                message,
                _localization.T("ApplyCompleteTitle"));
            FooterText.Text = _localization.T("ConfigurationApplied");
            RefreshStatus();
            RefreshBackups();
        });
    }

    private void BackupItem_Check(object sender, QING.UIKIT.ModBase.RouteEventArgs e)
    {
        _selectedBackup = sender is QING.UIKIT.MyListItem { Tag: BackupOption option }
            ? option
            : null;
        RestoreButton.IsEnabled = _selectedBackup?.Info.Valid == true;
    }

    private void Restore_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedGame is null ||
            _selectedBackup is not { } backup ||
            !backup.Info.Valid)
            return;

        if (ProcessService.IsGameRunning(_selectedGame.GameId))
        {
            QingDialog.Show(
                this,
                _localization.T("GameRunningRestore"),
                _localization.T("RestoreTitle"),
                kind: QingDialogKind.Warning);
            return;
        }

        var confirm = QingDialog.Show(
            this,
            _localization.Format("ConfirmRestore", backup.Info.CreatedAt),
            _localization.T("ConfirmRestoreTitle"),
            QingDialogButtons.YesNo,
            QingDialogKind.Question);
        if (confirm != QingDialogResult.Yes)
            return;

        RunUi(_localization.T("RestoreTitle"), () =>
        {
            var result = _restoreEngine.ExecuteRestore(backup.Info.Path);
            QingDialog.Show(
                this,
                result.Message,
                result.Success ? _localization.T("RestoreCompleteTitle") : _localization.T("RestoreFailedTitle"),
                kind: result.Success ? QingDialogKind.Information : QingDialogKind.Error);
            FooterText.Text = result.Success ? _localization.T("BackupRestored") : _localization.T("RestoreFailed");
            RefreshStatus();
            RefreshBackups();
        });
    }

    private void ProjectLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void ResetFirstRun_Click(object sender, MouseButtonEventArgs e)
    {
        _settings.UiLanguage = null;
        _settings.DisclaimerAccepted = false;
        _settingsService.Save(_settings);

        DisclaimerCheck1.Checked = false;
        DisclaimerCheck2.Checked = false;
        DisclaimerCheck3.Checked = false;
        DisclaimerCheck4.Checked = false;
        _localization.SetLanguage(null);
        SyncLanguageSelectors();
        ApplyLocalization();
        ShowLanguageSelection();
    }

    private void RefreshStatus()
    {
        if (_selectedGame is null)
            return;

        try
        {
            _currentStatus = _statusService.GetStatus(GameDetector.ToWireId(_selectedGame.GameId), _selectedGame.ResourcePath);
            ReapplyButton.IsEnabled = _currentStatus.State is "outdated" or "modified";
            StatusText.Text = _currentStatus.State switch
            {
                "none" => _localization.T("NotConfigured"),
                "external_swap" => _localization.Format(
                    "ExternalSwapDetail",
                    GetStatusDisplayName(_currentStatus.State),
                    _currentStatus.VoiceLanguage,
                    _currentStatus.TextLanguage),
                "external_duplicate" => _localization.Format(
                    "ExternalDuplicateDetail",
                    GetStatusDisplayName(_currentStatus.State),
                    _currentStatus.VoiceLanguage,
                    _currentStatus.TextLanguage),
                _ => _localization.Format(
                    "StatusDetail",
                    GetStatusDisplayName(_currentStatus.State),
                    _currentStatus.VoiceLanguage,
                    _currentStatus.TextLanguage,
                    _currentStatus.LastApplied)
            };
        }
        catch (Exception ex)
        {
            ReapplyButton.IsEnabled = false;
            StatusText.Text = _localization.Format("StatusUnavailable", ex.Message);
        }
    }

    private void RefreshBackups()
    {
        if (_selectedGame is null)
        {
            BackupList.Children.Clear();
            _selectedBackup = null;
            return;
        }

        var backups = _backupManager
            .ListBackups(GameDetector.ToWireId(_selectedGame.GameId))
            .Select(info => new BackupOption(
                info,
                _localization.Format("BackupTitle", info.VoiceLanguage, info.TextLanguage),
                $"{info.CreatedAt}" + (info.Valid ? "" : $" · {_localization.T("InvalidBackup")}")))
            .ToList();
        BackupList.Children.Clear();
        foreach (var backup in backups)
        {
            var item = new QING.UIKIT.MyListItem
            {
                Title = backup.Title,
                Info = backup.Detail,
                Height = 58,
                Type = QING.UIKIT.MyListItem.CheckType.RadioBox,
                Tag = backup,
                IsEnabled = backup.Info.Valid,
                Margin = new Thickness(0, 0, 0, 3)
            };
            item.Check += BackupItem_Check;
            BackupList.Children.Add(item);
        }

        _selectedBackup = null;
        RestoreButton.IsEnabled = false;
    }

    private void UpdatePreview()
    {
        if (VoiceCombo.SelectedItem is not LanguageOption voice ||
            TextCombo.SelectedItem is not LanguageOption text)
        {
            PreviewText.Text = _localization.T("ChooseTwoDifferentLanguages");
            return;
        }

        PreviewText.Text = string.Equals(voice.Pack.Code, text.Pack.Code, StringComparison.OrdinalIgnoreCase)
            ? _localization.T("SameLanguagePreview")
            : _localization.Format("OperationPreview", text.Pack.FileName, voice.Pack.FileName);
    }

    private void ClearGameDetails()
    {
        _selectedGame = null;
        _languagePacks = [];
        VoiceCombo.ItemsSource = null;
        TextCombo.ItemsSource = null;
        BackupList.Children.Clear();
        _selectedBackup = null;
        GamePathText.Text = _localization.T("NoGameSelected");
        StatusText.Text = _localization.T("SelectGameToLoadStatus");
        PreviewText.Text = _localization.T("ChooseTwoDifferentLanguages");
        ReapplyButton.IsEnabled = false;
    }

    private GameId GetManualGameId()
    {
        if (ManualGameCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            return GameDetector.ParseWireId(tag);
        return GameId.Fh5;
    }

    private void PopulateGameList()
    {
        GameList.Children.Clear();
        foreach (var game in _games)
        {
            var item = new QING.UIKIT.MyListItem
            {
                Title = game.Profile.DisplayName,
                Info = $"{game.ChannelDisplay} · {game.Profile.RootPath}",
                Height = 58,
                Type = QING.UIKIT.MyListItem.CheckType.RadioBox,
                Tag = game,
                Margin = new Thickness(0, 0, 0, 3)
            };
            item.Check += GameItem_Check;
            GameList.Children.Add(item);
        }
    }

    private void PopulateLanguageSelectors()
    {
        var selectedVoice = (VoiceCombo.SelectedItem as LanguageOption)?.Pack.Code;
        var selectedText = (TextCombo.SelectedItem as LanguageOption)?.Pack.Code;
        var voice = _languagePacks
            .Where(pack => LanguageMapper.VoiceLanguageCodes.Contains(pack.Code))
            .Select(pack => new LanguageOption(pack, $"{_localization.LanguageName(pack.Code)} ({pack.Code})"))
            .ToList();
        var text = _languagePacks
            .Select(pack => new LanguageOption(pack, $"{_localization.LanguageName(pack.Code)} ({pack.Code})"))
            .ToList();

        VoiceCombo.ItemsSource = voice;
        TextCombo.ItemsSource = text;
        VoiceCombo.SelectedItem = voice.FirstOrDefault(item =>
            string.Equals(item.Pack.Code, selectedVoice, StringComparison.OrdinalIgnoreCase));
        TextCombo.SelectedItem = text.FirstOrDefault(item =>
            string.Equals(item.Pack.Code, selectedText, StringComparison.OrdinalIgnoreCase));
        if (VoiceCombo.SelectedIndex < 0)
            VoiceCombo.SelectedIndex = voice.Count > 0 ? 0 : -1;
        if (TextCombo.SelectedIndex < 0)
            TextCombo.SelectedIndex = text.Count > 1 ? 1 : 0;
    }

    private void SyncLanguageSelectors()
    {
        _updatingLanguageSelectors = true;
        try
        {
            var language = _localization.SupportedLanguages.First(item =>
                string.Equals(item.Code, _localization.CurrentLanguage, StringComparison.OrdinalIgnoreCase));
            UiLanguageList.SelectedItem = language;
            SettingsLanguageCombo.SelectedItem = language;
        }
        finally
        {
            _updatingLanguageSelectors = false;
        }
    }

    private static string GetApplicationVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return (informational?.Split('+')[0] ?? assembly.GetName().Version?.ToString(3) ?? "2.1.0");
    }

    private void SelectGameItem(int index)
    {
        if (index < 0 || index >= GameList.Children.Count)
            return;

        if (GameList.Children[index] is QING.UIKIT.MyListItem item)
            item.Checked = true;
    }

    private string GetStatusDisplayName(string state) => state switch
    {
        "applied" => _localization.T("StateApplied"),
        "reverted" => _localization.T("StateReverted"),
        "outdated" => _localization.T("StateOutdated"),
        "modified" => _localization.T("StateModified"),
        "external_swap" => _localization.T("StateExternalSwap"),
        "external_duplicate" => _localization.T("StateExternalDuplicate"),
        _ => state
    };

    private void RunUi(string busyText, Action action)
    {
        var previous = FooterText.Text;
        try
        {
            FooterText.Text = busyText;
            Mouse.OverrideCursor = Cursors.Wait;
            action();
        }
        catch (Exception ex)
        {
            FooterText.Text = previous;
            QingDialog.Show(
                this,
                ex.Message,
                _localization.T("AppName"),
                kind: QingDialogKind.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(e.Source, sender))
            return;

        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void TopNav_Check(object sender, bool raiseByMouse)
    {
        if (sender is not QING.UIKIT.MyRadioButton button || !button.Checked)
            return;

        var selected = button.Tag?.ToString() ?? "0";
        HomeView.Visibility = selected == "0" ? Visibility.Visible : Visibility.Collapsed;
        BackupView.Visibility = selected == "1" ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = selected == "2" ? Visibility.Visible : Visibility.Collapsed;
        AboutView.Visibility = selected == "3" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Minimize_Click(object sender, EventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, EventArgs e) => Close();

    private sealed record GameOption(GameProfile Profile)
    {
        public string ChannelDisplay => Profile.Channel.Equals("steam", StringComparison.OrdinalIgnoreCase)
            ? "Steam"
            : Profile.Channel.Equals("xbox", StringComparison.OrdinalIgnoreCase)
                ? "Xbox"
            : Profile.Channel;
    }

    private sealed record LanguageOption(LanguagePack Pack, string Display);

    private sealed record BackupOption(BackupInfo Info, string Title, string Detail);
}
