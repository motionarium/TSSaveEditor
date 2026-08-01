using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.Win32;
using Ets2SaveEditor.Core;
using System.Text.RegularExpressions;

namespace Ets2SaveEditor.App
{
    public partial class MainWindow : Window
    {
        private List<GameProfile> _allGameProfiles = new List<GameProfile>();
        private List<GameProfile> _profiles = new List<GameProfile>();
        private List<SaveGame> _saves = new List<SaveGame>();
        private GameProfile _selectedProfile;
        private SaveGame _selectedSave;
        private List<CityItem> _allCitiesList = new List<CityItem>();
        private List<GarageItem> _allGaragesList = new List<GarageItem>();
        private string _loadedTheme = "cyan";
        private string _loadedLanguage = "ru";
        private string _committedTheme = "cyan";
        private string _committedLanguage = "ru";
        private string _committedCustomPath = "";
        private string _committedDecryptorPath = "";
        private bool _needsFirstRun;
        private bool _settingsWritable;
        private bool _syncingSettingsUi;
        private bool _syncingXpLevel;
        private bool _syncingFleetSelection;
        private bool _syncingSaveUi;
        private bool? _noSaveOverlayVisible;
        private bool? _saveBarVisible;
        private bool? _settingsDirtyBarVisible;
        private int _themeAnimToken;
        private DispatcherTimer _themeAnimTimer;
        private readonly HashSet<Border> _hidingOverlays = new HashSet<Border>();
        private const double SaveBarExpandedHeight = 68;
        private const double SettingsDirtyBarHeight = 56;
        private string _loadedSaveText;
        private SaveEditSnapshot _committedSaveEdits;
        private List<FleetUnit> _fleetTrucks = new List<FleetUnit>();
        private List<FleetUnit> _fleetTrailers = new List<FleetUnit>();
        private UpdateInfo? _pendingUpdate;
        private bool _updateBusy;
        private enum UpdateButtonMode { Check, Download, OpenRelease }
        private UpdateButtonMode _updateButtonMode = UpdateButtonMode.Check;
        private MapModDetectionResult _mapDetect;
        private string _mapArchiveOverride;
        private ModMapScanResult _modScanCache;
        private bool _modScanBusy;
        private int _modScanToken;
        private string _modsFolderOverride;
        private int _modsCatalogToken;
        private ModCatalogSnapshot _modsSnapshot;

        private sealed class ModListRow
        {
            public string Label { get; set; }
            public string Title { get; set; }
            public string Subtitle { get; set; }
        }

        private sealed class SaveEditSnapshot
        {
            public string Money;
            public string Xp;
            public int Adr;
            public int SkillDist;
            public int SkillFragile;
            public int SkillUrgent;
            public int SkillEco;
            public int SkillValuable;
            public bool UnlockMapCities;
            public bool UnlockMapRoads;
            public bool RepairCabin;
            public bool RepairChassis;
            public bool RepairEngine;
            public bool RepairTransmission;
            public bool RepairWheels;
            public bool RepairFuel;
            public bool RepairTrailerBody;
            public bool RepairTrailerChassis;
            public bool RepairTrailerWheels;
            public Dictionary<string, bool> Cities = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> Garages = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        static MainWindow()
        {
            try
            {
                string dllPath = Path.Combine(AppContext.BaseDirectory, "zlib.dll");
                if (File.Exists(dllPath))
                {
                    System.Runtime.InteropServices.NativeLibrary.Load(dllPath);
                }
            }
            catch { }
        }

        public MainWindow()
        {
            // Decide first-run BEFORE InitializeComponent — theme ComboBox
            // SelectionChanged would otherwise create settings.ini too early.
            string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
            _needsFirstRun = !File.Exists(settingsFile);

            InitializeComponent();

            if (AboutVersionBadge != null)
                AboutVersionBadge.Text = AppInfo.VersionLabel;

            InitPlayerLevelCombo();

            LoadSettings();
            Loc.SetLanguage(_loadedLanguage);
            ApplyTheme(_loadedTheme, animate: false);
            ApplyLanguage();
            CaptureCommittedSettings();

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            WindowChromeHelper.ApplyDarkTitleBar(this);
            UpdateWindowIconFromAccent();
        }

        private void InitPlayerLevelCombo()
        {
            if (ComboPlayerLevel == null) return;
            _syncingXpLevel = true;
            try
            {
                ComboPlayerLevel.Items.Clear();
                for (int i = 0; i <= XpLevel.MaxUsefulLevel; i++)
                    ComboPlayerLevel.Items.Add(i);
                ComboPlayerLevel.SelectedItem = 0;
            }
            finally { _syncingXpLevel = false; }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;
            PlaySplashAnimation(() =>
            {
                if (_needsFirstRun)
                    ShowFirstRun();
                else
                    FinishStartupScan();
            });
        }

        private void FinishStartupScan()
        {
            _settingsWritable = true;
            RefreshProfiles();
            UpdateMapListEmptyStates();
            UpdateSelectorsEnabled();
        }

        private void LoadSettings()
        {
            try
            {
                string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
                if (!File.Exists(settingsFile))
                    return;

                var lines = File.ReadAllLines(settingsFile);
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;
                    string key = parts[0].Trim();
                    string val = parts[1].Trim();
                    if (key == "CustomPath") TxtCustomPath.Text = val;
                    if (key == "DecryptorPath") TxtDecryptorPath.Text = val;
                    if (key == "ThemeColor") _loadedTheme = val;
                    if (key == "Language") _loadedLanguage = val;
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            // Block writes during InitializeComponent / splash / first-run UI tweaks
            if (!_settingsWritable) return;
            WriteSettingsIni(_committedTheme, _committedLanguage, _committedCustomPath, _committedDecryptorPath);
        }

        private void WriteSettingsIni(string theme, string language, string customPath, string decryptorPath)
        {
            try
            {
                string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
                var lines = new List<string>
                {
                    $"CustomPath={customPath ?? ""}",
                    $"DecryptorPath={decryptorPath ?? ""}",
                    $"ThemeColor={theme ?? "cyan"}",
                    $"Language={language ?? "ru"}"
                };
                File.WriteAllLines(settingsFile, lines);
                _needsFirstRun = false;
            }
            catch { }
        }

        private void CaptureCommittedSettings()
        {
            _committedTheme = _loadedTheme ?? "cyan";
            _committedLanguage = _loadedLanguage ?? "ru";
            _committedCustomPath = TxtCustomPath?.Text ?? "";
            _committedDecryptorPath = TxtDecryptorPath?.Text ?? "";
            UpdateSettingsDirtyBar(false);
        }

        private string GetDraftTheme()
        {
            return _loadedTheme ?? _committedTheme ?? "cyan";
        }

        private string GetDraftLanguage()
        {
            return _loadedLanguage ?? _committedLanguage ?? "ru";
        }

        private bool HasPendingSettingsChanges()
        {
            string path = TxtCustomPath?.Text ?? "";
            string decrypt = TxtDecryptorPath?.Text ?? "";
            return !string.Equals(path, _committedCustomPath ?? "", StringComparison.Ordinal)
                || !string.Equals(decrypt, _committedDecryptorPath ?? "", StringComparison.Ordinal)
                || !string.Equals(GetDraftTheme(), _committedTheme ?? "cyan", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetDraftLanguage(), _committedLanguage ?? "ru", StringComparison.OrdinalIgnoreCase);
        }

        private void SettingsField_Changed(object sender, TextChangedEventArgs e)
        {
            if (_syncingSettingsUi) return;
            UpdateSettingsDirtyBar(HasPendingSettingsChanges());
        }

        private void SettingsTheme_Click(object sender, MouseButtonEventArgs e)
        {
            if (_syncingSettingsUi) return;
            if (sender is not FrameworkElement fe || fe.Tag == null) return;

            string theme = fe.Tag.ToString();
            if (string.IsNullOrEmpty(theme)) return;

            _loadedTheme = theme;
            // Apply after the mouse-up routing finishes — swapping DynamicResource
            // brushes mid-event can tear down the clicked visual.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyTheme(theme);
                UpdateSettingsThemeSwatches(theme);
                UpdateSettingsLangCards();
                UpdateSettingsDirtyBar(HasPendingSettingsChanges());
            }), DispatcherPriority.Input);
        }

        private void SettingsLang_Click(object sender, MouseButtonEventArgs e)
        {
            if (_syncingSettingsUi) return;
            if (sender is not FrameworkElement fe || fe.Tag == null) return;

            string lang = fe.Tag.ToString();
            if (string.IsNullOrEmpty(lang)) return;

            _loadedLanguage = lang;
            Loc.SetLanguage(_loadedLanguage);
            ApplyLanguage();
            UpdateSettingsLangCards();
            UpdateSettingsDirtyBar(HasPendingSettingsChanges());
        }

        private void UpdateSettingsThemeSwatches(string theme)
        {
            void Mark(Border b, string tag)
            {
                if (b == null) return;
                bool on = string.Equals(theme, tag, StringComparison.OrdinalIgnoreCase);
                b.BorderBrush = on ? Brushes.White : Brushes.Transparent;
            }
            Mark(SettingsSwatchCyan, "cyan");
            Mark(SettingsSwatchPurple, "purple");
            Mark(SettingsSwatchGreen, "green");
            Mark(SettingsSwatchBlue, "blue");
            Mark(SettingsSwatchOrange, "orange");
            Mark(SettingsSwatchRed, "red");
        }

        private void UpdateSettingsLangCards()
        {
            Brush accent = Brushes.Cyan;
            if (TryFindResource("AccentColor") is Color accentColor)
                accent = new SolidColorBrush(accentColor);
            else if (TryFindResource("AccentBrush") is SolidColorBrush scb)
                accent = new SolidColorBrush(scb.Color);

            var idle = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D323E"));
            bool ru = string.Equals(_loadedLanguage, "ru", StringComparison.OrdinalIgnoreCase);
            if (SettingsCardLangRu != null) SettingsCardLangRu.BorderBrush = ru ? accent : idle;
            if (SettingsCardLangEn != null) SettingsCardLangEn.BorderBrush = !ru ? accent : idle;
        }

        private void SyncSettingsUiFromCommitted()
        {
            _syncingSettingsUi = true;
            try
            {
                if (TxtCustomPath != null)
                    TxtCustomPath.Text = _committedCustomPath ?? "";
                if (TxtDecryptorPath != null)
                    TxtDecryptorPath.Text = _committedDecryptorPath ?? "";

                _loadedTheme = _committedTheme ?? "cyan";
                _loadedLanguage = _committedLanguage ?? "ru";
                ApplyTheme(_loadedTheme, animate: false);
                Loc.SetLanguage(_loadedLanguage);
                ApplyLanguage();
                UpdateSettingsThemeSwatches(_loadedTheme);
                UpdateSettingsLangCards();
            }
            finally
            {
                _syncingSettingsUi = false;
            }

            UpdateSettingsDirtyBar(false);
        }

        private void UpdateSettingsDirtyBar(bool dirty)
        {
            if (SettingsDirtyBar == null) return;
            if (_settingsDirtyBarVisible == dirty)
                return;

            bool animate = _settingsDirtyBarVisible.HasValue;
            _settingsDirtyBarVisible = dirty;

            SettingsDirtyBar.BeginAnimation(UIElement.OpacityProperty, null);
            SettingsDirtyBar.BeginAnimation(FrameworkElement.MaxHeightProperty, null);

            if (!animate)
            {
                if (dirty)
                {
                    SettingsDirtyBar.MaxHeight = double.PositiveInfinity;
                    SettingsDirtyBar.Opacity = 1;
                    SettingsDirtyBar.Visibility = Visibility.Visible;
                    SettingsDirtyBar.IsHitTestVisible = true;
                }
                else
                {
                    SettingsDirtyBar.MaxHeight = 0;
                    SettingsDirtyBar.Opacity = 0;
                    SettingsDirtyBar.Visibility = Visibility.Collapsed;
                    SettingsDirtyBar.IsHitTestVisible = false;
                }
                return;
            }

            if (dirty)
            {
                SettingsDirtyBar.Visibility = Visibility.Visible;
                SettingsDirtyBar.IsHitTestVisible = true;
                SettingsDirtyBar.MaxHeight = 0;
                SettingsDirtyBar.Opacity = 0;

                var heightAnim = new DoubleAnimation(0, SettingsDirtyBarHeight, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                heightAnim.Completed += (_, _) =>
                {
                    if (_settingsDirtyBarVisible == true)
                    {
                        SettingsDirtyBar.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                        SettingsDirtyBar.MaxHeight = double.PositiveInfinity;
                    }
                };

                var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                SettingsDirtyBar.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                SettingsDirtyBar.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            }
            else
            {
                SettingsDirtyBar.IsHitTestVisible = false;
                double fromHeight = SettingsDirtyBar.ActualHeight > 1 ? SettingsDirtyBar.ActualHeight : SettingsDirtyBarHeight;

                var heightAnim = new DoubleAnimation(fromHeight, 0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                heightAnim.Completed += (_, _) =>
                {
                    if (_settingsDirtyBarVisible != true)
                    {
                        SettingsDirtyBar.Visibility = Visibility.Collapsed;
                        SettingsDirtyBar.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                        SettingsDirtyBar.MaxHeight = 0;
                        SettingsDirtyBar.Opacity = 0;
                    }
                };

                var fadeAnim = new DoubleAnimation(SettingsDirtyBar.Opacity, 0, TimeSpan.FromMilliseconds(160))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                SettingsDirtyBar.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                SettingsDirtyBar.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            }
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            string newTheme = GetDraftTheme();
            string newLang = GetDraftLanguage();
            string newPath = TxtCustomPath?.Text ?? "";
            string newDecrypt = TxtDecryptorPath?.Text ?? "";
            bool pathChanged = !string.Equals(newPath, _committedCustomPath ?? "", StringComparison.Ordinal);

            _committedTheme = newTheme;
            _committedLanguage = newLang;
            _committedCustomPath = newPath;
            _committedDecryptorPath = newDecrypt;

            _loadedTheme = newTheme;
            _loadedLanguage = newLang;

            ApplyTheme(newTheme);
            Loc.SetLanguage(newLang);
            ApplyLanguage();
            SaveSettings();
            UpdateSettingsDirtyBar(false);

            if (pathChanged)
                RefreshProfiles();
        }

        private void ApplyTheme(string theme, bool animate = true)
        {
            if (string.IsNullOrEmpty(theme)) theme = "cyan";
            _loadedTheme = theme;

            // Colors definition for themes
            string accentColorHex = "#00E5FF";
            string hoverColorHex = "#00CCDF";
            string pressedColorHex = "#009AB0";
            string accentBgColorHex = "#1B2740";
            string accentBgPressedColorHex = "#0D1E30";

            switch (theme.ToLowerInvariant())
            {
                case "purple":
                    accentColorHex = "#D500F9"; // Neon Purple
                    hoverColorHex = "#C500E9";
                    pressedColorHex = "#A500C9";
                    accentBgColorHex = "#2B143D";
                    accentBgPressedColorHex = "#1B0D2B";
                    break;
                case "green":
                    accentColorHex = "#00E676"; // Cyber Green
                    hoverColorHex = "#00D666";
                    pressedColorHex = "#00B656";
                    accentBgColorHex = "#123624";
                    accentBgPressedColorHex = "#0A2416";
                    break;
                case "blue":
                    accentColorHex = "#2979FF"; // Electric Blue
                    hoverColorHex = "#2570EF";
                    pressedColorHex = "#1D5ECF";
                    accentBgColorHex = "#122045";
                    accentBgPressedColorHex = "#0A122B";
                    break;
                case "orange":
                    accentColorHex = "#FF9100"; // Sunset Orange
                    hoverColorHex = "#EF8500";
                    pressedColorHex = "#CF7500";
                    accentBgColorHex = "#362212";
                    accentBgPressedColorHex = "#24160A";
                    break;
                case "red":
                    accentColorHex = "#FF1744"; // Imperial Red
                    hoverColorHex = "#EF1540";
                    pressedColorHex = "#CF1030";
                    accentBgColorHex = "#361218";
                    accentBgPressedColorHex = "#240A0F";
                    break;
            }

            var accentColor = (Color)ColorConverter.ConvertFromString(accentColorHex);
            var hoverColor = (Color)ColorConverter.ConvertFromString(hoverColorHex);
            var pressedColor = (Color)ColorConverter.ConvertFromString(pressedColorHex);
            var accentBgColor = (Color)ColorConverter.ConvertFromString(accentBgColorHex);
            var accentBgPressedColor = (Color)ColorConverter.ConvertFromString(accentBgPressedColorHex);

            var targets = new (string Key, Color To)[]
            {
                ("AccentBrush", accentColor),
                ("AccentHoverBrush", hoverColor),
                ("AccentPressedBrush", pressedColor),
                ("AccentBgBrush", accentBgColor),
                ("AccentBgPressedBrush", accentBgPressedColor),
            };

            // AccentColor jumps immediately — used by lang-card borders, not animated fills.
            Resources["AccentColor"] = accentColor;
            UpdateSettingsThemeSwatches(theme);
            UpdateSettingsLangCards();
            UpdateFirstRunLangCards();

            ApplyThemeBrushesSafe(targets, animate);
        }

        /// <summary>
        /// Safe theme transition: each frame installs a brand-new SolidColorBrush.
        /// Never mutates Color on a brush already published in Resources (WPF may freeze it).
        /// </summary>
        private void ApplyThemeBrushesSafe((string Key, Color To)[] targets, bool animate)
        {
            StopThemeBrushAnimation();
            _themeAnimToken++;
            int token = _themeAnimToken;

            var from = new Color[targets.Length];
            bool anyChange = false;
            for (int i = 0; i < targets.Length; i++)
            {
                from[i] = ReadResourceBrushColor(targets[i].Key, targets[i].To);
                if (from[i] != targets[i].To)
                    anyChange = true;
            }

            if (!animate || !anyChange)
            {
                SetThemeBrushes(targets, null);
                Dispatcher.BeginInvoke(new Action(UpdateWindowIconFromAccent), DispatcherPriority.ApplicationIdle);
                return;
            }

            const int steps = 12;
            int step = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            _themeAnimTimer = timer;

            timer.Tick += (_, __) =>
            {
                try
                {
                    if (token != _themeAnimToken)
                    {
                        timer.Stop();
                        return;
                    }

                    step++;
                    double t = Math.Min(1.0, step / (double)steps);
                    double eased = EaseInOutCubic(t);

                    var frame = new (string Key, Color To)[targets.Length];
                    for (int i = 0; i < targets.Length; i++)
                        frame[i] = (targets[i].Key, LerpColor(from[i], targets[i].To, eased));

                    SetThemeBrushes(frame, null);

                    if (t < 1.0)
                        return;

                    timer.Stop();
                    if (ReferenceEquals(_themeAnimTimer, timer))
                        _themeAnimTimer = null;

                    SetThemeBrushes(targets, null);
                    UpdateWindowIconFromAccent();
                }
                catch
                {
                    timer.Stop();
                    if (ReferenceEquals(_themeAnimTimer, timer))
                        _themeAnimTimer = null;
                    try { SetThemeBrushes(targets, null); } catch { }
                    try { UpdateWindowIconFromAccent(); } catch { }
                }
            };

            timer.Start();
        }

        private void SetThemeBrushes((string Key, Color To)[] brushes, Color? accentColor)
        {
            foreach (var (key, to) in brushes)
                Resources[key] = new SolidColorBrush(to);
            if (accentColor.HasValue)
                Resources["AccentColor"] = accentColor.Value;
        }

        private void StopThemeBrushAnimation()
        {
            if (_themeAnimTimer == null) return;
            _themeAnimTimer.Stop();
            _themeAnimTimer = null;
        }

        private Color ReadResourceBrushColor(string key, Color fallback)
        {
            if (TryFindResource(key) is SolidColorBrush brush)
                return brush.Color;
            return fallback;
        }

        private static double EaseInOutCubic(double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            return t < 0.5
                ? 4 * t * t * t
                : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }

        private static Color LerpColor(Color from, Color to, double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            return Color.FromArgb(
                (byte)Math.Round(from.A + (to.A - from.A) * t),
                (byte)Math.Round(from.R + (to.R - from.R) * t),
                (byte)Math.Round(from.G + (to.G - from.G) * t),
                (byte)Math.Round(from.B + (to.B - from.B) * t));
        }

        private void UpdateWindowIconFromAccent()
        {
            try
            {
                Color accent = Colors.Cyan;
                if (TryFindResource("AccentColor") is Color c)
                    accent = c;
                else if (TryFindResource("AccentBrush") is SolidColorBrush scb)
                    accent = scb.Color;

                Icon = AppIconHelper.Create(accent, 64);
            }
            catch { }
        }

        private void ApplyLanguage()
        {
            Loc.SetLanguage(_loadedLanguage);

            if (NavEconomy != null) { NavEconomy.Content = Loc.T("nav.economy"); NavEconomy.ToolTip = Loc.T("nav.economy.tip"); }
            if (NavMap != null) { NavMap.Content = Loc.T("nav.map"); NavMap.ToolTip = Loc.T("nav.map.tip"); }
            if (NavVehicle != null) { NavVehicle.Content = Loc.T("nav.vehicle"); NavVehicle.ToolTip = Loc.T("nav.vehicle.tip"); }
            if (NavMods != null) { NavMods.Content = Loc.T("nav.mods"); NavMods.ToolTip = Loc.T("nav.mods.tip"); }
            if (TxtBtnSettings != null) TxtBtnSettings.Text = Loc.T("nav.settings");
            if (BtnOpenSettings != null) BtnOpenSettings.ToolTip = Loc.T("nav.settings.tip");
            if (TxtBtnAbout != null) TxtBtnAbout.Text = Loc.T("nav.about");
            if (BtnOpenAbout != null) BtnOpenAbout.ToolTip = Loc.T("nav.about.tip");
            if (TxtSettingsOverlayTitle != null) TxtSettingsOverlayTitle.Text = Loc.T("nav.settings");
            if (TxtAboutOverlayTitle != null) TxtAboutOverlayTitle.Text = Loc.T("nav.about");

            if (LblGameHeader != null) LblGameHeader.Text = Loc.T("hdr.game");
            if (LblSourceHeader != null) LblSourceHeader.Text = Loc.T("hdr.source");
            if (LblProfileHeader != null) LblProfileHeader.Text = Loc.T("hdr.profile");
            if (LblSaveHeader != null) LblSaveHeader.Text = Loc.T("hdr.save");
            if (TxtHeaderSubtitle != null) TxtHeaderSubtitle.Text = Loc.T("hdr.subtitle");
            if (ComboGame != null) ComboGame.ToolTip = Loc.T("hdr.game.tip");
            if (ComboSource != null) ComboSource.ToolTip = Loc.T("hdr.source.tip");
            if (ComboSourceSteam != null) ComboSourceSteam.Content = Loc.T("hdr.source.steam");
            if (ComboSourceLocal != null) ComboSourceLocal.Content = Loc.T("hdr.source.local");
            if (ComboProfile != null) ComboProfile.ToolTip = Loc.T("hdr.profile.tip");
            if (ComboSave != null) ComboSave.ToolTip = Loc.T("hdr.save.tip");
            if (BtnRefresh != null) BtnRefresh.ToolTip = Loc.T("hdr.refresh.tip");
            if (TxtSteamId != null) TxtSteamId.ToolTip = Loc.T("hdr.steamid.tip");
            UpdateSteamIdLabel();

            if (TxtSaveBarTitle != null) TxtSaveBarTitle.Text = Loc.T("savebar.title");
            if (TxtSaveBarHint != null) TxtSaveBarHint.Text = Loc.T("savebar.hint");
            if (TxtSaveBarBtn != null) TxtSaveBarBtn.Text = Loc.T("savebar.btn");
            if (BtnSave != null) BtnSave.ToolTip = Loc.T("savebar.tip");

            if (TxtSettingsLangTitle != null) TxtSettingsLangTitle.Text = Loc.T("settings.lang.title");
            if (TxtSettingsLangHint != null) TxtSettingsLangHint.Text = Loc.T("settings.lang.hint");
            if (TxtSettingsThemeTitle != null) TxtSettingsThemeTitle.Text = Loc.T("settings.theme.title");
            if (TxtSettingsThemeHint != null) TxtSettingsThemeHint.Text = Loc.T("settings.theme.hint");
            if (TxtSettingsDirtyHint != null) TxtSettingsDirtyHint.Text = Loc.T("settings.dirty.hint");
            if (BtnSaveSettings != null)
            {
                BtnSaveSettings.Content = Loc.T("settings.dirty.save");
                BtnSaveSettings.ToolTip = Loc.T("settings.dirty.save.tip");
            }
            if (TxtSettingsCardLangRu != null) TxtSettingsCardLangRu.Text = Loc.T("firstrun.ru");
            if (TxtSettingsCardLangEn != null) TxtSettingsCardLangEn.Text = Loc.T("firstrun.en");
            UpdateSettingsLangCards();

            if (TxtFirstRunWelcome != null) TxtFirstRunWelcome.Text = Loc.T("firstrun.welcome");
            if (TxtFirstRunSubtitle != null) TxtFirstRunSubtitle.Text = Loc.T("firstrun.subtitle");
            if (TxtFirstRunLangLabel != null) TxtFirstRunLangLabel.Text = Loc.T("firstrun.lang");
            if (TxtFirstRunThemeLabel != null) TxtFirstRunThemeLabel.Text = Loc.T("firstrun.theme");
            if (BtnFirstRunContinue != null) BtnFirstRunContinue.Content = Loc.T("firstrun.continue");
            if (TxtCardLangRu != null) TxtCardLangRu.Text = Loc.T("firstrun.ru");
            if (TxtCardLangEn != null) TxtCardLangEn.Text = Loc.T("firstrun.en");
            if (TxtSplashTagline != null) TxtSplashTagline.Text = Loc.T("splash.tagline");
            if (BtnAppDialogOk != null) BtnAppDialogOk.Content = Loc.T("dialog.ok");

            UpdateNoSaveOverlayLanguage(NoSaveOverlayEconomy);
            UpdateNoSaveOverlayLanguage(NoSaveOverlayMap);
            UpdateNoSaveOverlayLanguage(NoSaveOverlayVehicle);
            UpdateNoSaveOverlayLanguage(NoSaveOverlayMods);
            UpdateFirstRunLangCards();

            Loc.ApplyTree(this);
            RefreshQuickMoneyTooltips();
            RefreshUpdateLabelsAfterLanguage();
            UpdatePlayerLevelFromXp();
            foreach (var garage in _allGaragesList)
                garage.NotifyLocalized();
            UpdateMapListEmptyStates();
        }

        private void RefreshUpdateLabelsAfterLanguage()
        {
            if (AboutVersionBadge != null)
                AboutVersionBadge.Text = AppInfo.VersionLabel;

            if (BtnCheckUpdate != null)
                BtnCheckUpdate.ToolTip = Loc.T("about.update.tip");

            string buttonKey = _updateButtonMode switch
            {
                UpdateButtonMode.Download => "about.update.download",
                UpdateButtonMode.OpenRelease => "about.update.open",
                _ => "about.update.btn"
            };
            if (BtnCheckUpdateLabel != null)
                BtnCheckUpdateLabel.Text = Loc.T(buttonKey);

            if (AboutUpdateHint == null || _updateBusy)
                return;

            if (_updateButtonMode == UpdateButtonMode.Download && _pendingUpdate != null)
                AboutUpdateHint.Text = Loc.Tf("about.update.available", "v" + _pendingUpdate.Version);
            else if (_updateButtonMode == UpdateButtonMode.OpenRelease)
                AboutUpdateHint.Text = Loc.T("about.update.noasset");
            else if (_pendingUpdate == null)
                AboutUpdateHint.Text = Loc.T("about.update.hint");
        }

        private void RefreshQuickMoneyTooltips()
        {
            if (PanelQuickMoney == null) return;
            foreach (var child in PanelQuickMoney.Children)
            {
                if (child is Button btn && btn.Tag != null
                    && long.TryParse(btn.Tag.ToString(), out long amount))
                {
                    string formatted = amount.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
                    btn.ToolTip = Loc.Tf("econ.money.set", formatted);
                }
            }
        }

        private void UpdateNoSaveOverlayLanguage(Border overlay)
        {
            if (overlay == null) return;
            Loc.ApplyTree(overlay);
        }

        private static void SetTemplatedText(Control control, string partName, string locKey)
        {
            if (control == null) return;
            control.ApplyTemplate();
            if (control.Template?.FindName(partName, control) is TextBlock tb)
                tb.Text = Loc.T(locKey);
        }

        private void PlaySplashAnimation(Action onComplete)
        {
            if (SplashOverlay == null || SplashContent == null)
            {
                onComplete?.Invoke();
                return;
            }

            SplashOverlay.Visibility = Visibility.Visible;
            SplashOverlay.Opacity = 1;
            SplashContent.Opacity = 0;
            SplashScale.ScaleX = SplashScale.ScaleY = 0.86;
            SplashTranslate.Y = 12;
            if (SplashLine != null) SplashLine.Width = 0;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease };
            var scaleX = new DoubleAnimation(0.86, 1, TimeSpan.FromMilliseconds(520)) { EasingFunction = ease };
            var scaleY = new DoubleAnimation(0.86, 1, TimeSpan.FromMilliseconds(520)) { EasingFunction = ease };
            var slide = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(520)) { EasingFunction = ease };
            var line = new DoubleAnimation(0, 120, TimeSpan.FromMilliseconds(480))
            {
                BeginTime = TimeSpan.FromMilliseconds(220),
                EasingFunction = ease
            };

            SplashContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            SplashScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            SplashScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
            SplashTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
            SplashLine?.BeginAnimation(FrameworkElement.WidthProperty, line);

            var hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1180) };
            hold.Tick += (s, _) =>
            {
                hold.Stop();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(320))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                fadeOut.Completed += (_, __) =>
                {
                    SplashOverlay.Visibility = Visibility.Collapsed;
                    SplashOverlay.IsHitTestVisible = false;
                    onComplete?.Invoke();
                };
                SplashOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };
            hold.Start();
        }

        private void ShowFirstRun()
        {
            if (FirstRunOverlay == null) return;
            UpdateFirstRunLangCards();
            UpdateFirstRunThemeSwatches(_loadedTheme);

            FirstRunOverlay.Visibility = Visibility.Visible;
            FirstRunOverlay.Opacity = 0;
            FirstRunScale.ScaleX = FirstRunScale.ScaleY = 0.94;

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var scale = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            FirstRunOverlay.BeginAnimation(UIElement.OpacityProperty, fade);
            FirstRunScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            FirstRunScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        }

        private void HideFirstRun(Action onDone = null)
        {
            if (FirstRunOverlay == null || FirstRunOverlay.Visibility != Visibility.Visible)
            {
                onDone?.Invoke();
                return;
            }

            var fade = new DoubleAnimation(FirstRunOverlay.Opacity, 0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (_, __) =>
            {
                FirstRunOverlay.Visibility = Visibility.Collapsed;
                FirstRunOverlay.Opacity = 0;
                onDone?.Invoke();
            };
            FirstRunOverlay.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private void FirstRunLang_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag != null)
            {
                _loadedLanguage = fe.Tag.ToString();
                Loc.SetLanguage(_loadedLanguage);
                ApplyLanguage();
                UpdateFirstRunLangCards();
            }
        }

        private void FirstRunTheme_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag != null)
            {
                string theme = fe.Tag.ToString();
                if (string.IsNullOrEmpty(theme)) return;
                _loadedTheme = theme;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyTheme(theme);
                    UpdateFirstRunThemeSwatches(theme);
                    UpdateFirstRunLangCards();
                }), DispatcherPriority.Input);
            }
        }

        private void UpdateFirstRunLangCards()
        {
            // Use a fresh brush from the current accent color — DynamicResource
            // references go stale if we keep an old SolidColorBrush instance.
            Brush accent = Brushes.Cyan;
            if (TryFindResource("AccentColor") is Color accentColor)
                accent = new SolidColorBrush(accentColor);
            else if (TryFindResource("AccentBrush") is SolidColorBrush scb)
                accent = new SolidColorBrush(scb.Color);

            var idle = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D323E"));
            bool ru = Loc.Language == "ru";
            if (CardLangRu != null) CardLangRu.BorderBrush = ru ? accent : idle;
            if (CardLangEn != null) CardLangEn.BorderBrush = !ru ? accent : idle;
        }

        private void UpdateFirstRunThemeSwatches(string theme)
        {
            void Mark(Border b, string tag)
            {
                if (b == null) return;
                bool on = string.Equals(theme, tag, StringComparison.OrdinalIgnoreCase);
                b.BorderBrush = on ? Brushes.White : Brushes.Transparent;
            }
            Mark(SwatchCyan, "cyan");
            Mark(SwatchPurple, "purple");
            Mark(SwatchGreen, "green");
            Mark(SwatchBlue, "blue");
            Mark(SwatchOrange, "orange");
            Mark(SwatchRed, "red");
        }

        private void BtnFirstRunContinue_Click(object sender, RoutedEventArgs e)
        {
            _settingsWritable = true;
            CaptureCommittedSettings();
            SaveSettings();
            HideFirstRun(FinishStartupScan);
        }

        private void MapUiMode_Changed(object sender, RoutedEventArgs e)
        {
            // Removed in 1.2.0 — single map UI.
        }

        private void AnimateIn(FrameworkElement element)
        {
            element.Opacity = 0;
            var translate = new TranslateTransform(0, 10);
            element.RenderTransform = translate;

            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var slideUp = new DoubleAnimation(10, 0, new Duration(TimeSpan.FromMilliseconds(200)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            translate.BeginAnimation(TranslateTransform.YProperty, slideUp);
        }

        private static SolidColorBrush EnsureMutableBrush(Border border, Color fallback)
        {
            if (border.BorderBrush is SolidColorBrush existing && !existing.IsFrozen)
                return existing;

            Color color = fallback;
            if (border.BorderBrush is SolidColorBrush frozen)
                color = frozen.Color;

            var brush = new SolidColorBrush(color);
            border.BorderBrush = brush;
            return brush;
        }

        private void Card_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_selectedSave == null) return;
            if (sender is not Border border) return;

            var brush = EnsureMutableBrush(border, Color.FromRgb(0x25, 0x29, 0x32));
            brush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(Color.FromRgb(0x3A, 0x42, 0x52), TimeSpan.FromMilliseconds(140))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }

        private void Card_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not Border border) return;

            var brush = EnsureMutableBrush(border, Color.FromRgb(0x3A, 0x42, 0x52));
            brush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(Color.FromRgb(0x25, 0x29, 0x32), TimeSpan.FromMilliseconds(160))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }

        private void SetNoSaveOverlayVisible(Border overlay, bool visible, bool animate)
        {
            if (overlay == null) return;

            overlay.BeginAnimation(UIElement.OpacityProperty, null);

            if (!animate)
            {
                overlay.Opacity = visible ? 1 : 0;
                overlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                overlay.IsHitTestVisible = visible;
                return;
            }

            if (visible)
            {
                overlay.Visibility = Visibility.Visible;
                overlay.IsHitTestVisible = true;
                overlay.Opacity = 0;
                var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                overlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            else
            {
                var fadeOut = new DoubleAnimation(overlay.Opacity, 0, new Duration(TimeSpan.FromMilliseconds(160)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                fadeOut.Completed += (_, _) =>
                {
                    if (_selectedSave != null)
                    {
                        overlay.IsHitTestVisible = false;
                        overlay.Visibility = Visibility.Collapsed;
                        overlay.Opacity = 1;
                    }
                };
                overlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }

        private void UpdateNoSaveOverlays(bool animate = true)
        {
            bool show = _selectedSave == null;
            if (_noSaveOverlayVisible == show && animate)
                return;

            bool shouldAnimate = animate && _noSaveOverlayVisible.HasValue;
            _noSaveOverlayVisible = show;

            SetNoSaveOverlayVisible(NoSaveOverlayEconomy, show, shouldAnimate);
            SetNoSaveOverlayVisible(NoSaveOverlayMap, show, shouldAnimate);
            SetNoSaveOverlayVisible(NoSaveOverlayVehicle, show, shouldAnimate);
            SetNoSaveOverlayVisible(NoSaveOverlayMods, show, shouldAnimate);

            if (show)
            {
                UpdateNoSaveOverlayLanguage(NoSaveOverlayEconomy);
                UpdateNoSaveOverlayLanguage(NoSaveOverlayMap);
                UpdateNoSaveOverlayLanguage(NoSaveOverlayVehicle);
                UpdateNoSaveOverlayLanguage(NoSaveOverlayMods);
            }
        }

        private void BtnPickSave_Click(object sender, RoutedEventArgs e)
        {
            HighlightSaveSelector();

            if (ComboSave != null && ComboSave.IsEnabled)
            {
                ComboSave.Focus();
                ComboSave.IsDropDownOpen = true;
                return;
            }

            if (ComboProfile != null && ComboProfile.IsEnabled)
            {
                ComboProfile.Focus();
                ComboProfile.IsDropDownOpen = true;
                return;
            }

            if (ComboGame != null)
            {
                ComboGame.Focus();
                ComboGame.IsDropDownOpen = true;
            }
        }

        private void HighlightSaveSelector()
        {
            if (SaveSelectorHighlight == null || LblSaveHeader == null) return;

            var accent = (Color)FindResource("AccentColor");
            var borderBrush = new SolidColorBrush(Colors.Transparent);
            SaveSelectorHighlight.BorderBrush = borderBrush;
            borderBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(accent, TimeSpan.FromMilliseconds(180))
                {
                    AutoReverse = true,
                    RepeatBehavior = new RepeatBehavior(2),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                });

            LblSaveHeader.Foreground = new SolidColorBrush(accent);
            var labelBrush = LblSaveHeader.Foreground as SolidColorBrush;
            if (labelBrush != null)
            {
                var restore = new ColorAnimation(Color.FromRgb(0x3A, 0x3F, 0x50), TimeSpan.FromMilliseconds(900))
                {
                    BeginTime = TimeSpan.FromMilliseconds(700),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                labelBrush.BeginAnimation(SolidColorBrush.ColorProperty, restore);
            }
        }

        private void UpdateSaveBarState(bool dirty)
        {
            if (SaveBar == null) return;

            if (_saveBarVisible == dirty)
                return;

            bool animate = _saveBarVisible.HasValue;
            _saveBarVisible = dirty;

            SaveBar.BeginAnimation(UIElement.OpacityProperty, null);
            SaveBar.BeginAnimation(FrameworkElement.MaxHeightProperty, null);

            if (!animate)
            {
                if (dirty)
                {
                    SaveBar.MaxHeight = double.PositiveInfinity;
                    SaveBar.Opacity = 1;
                    SaveBar.Visibility = Visibility.Visible;
                    SaveBar.IsHitTestVisible = true;
                }
                else
                {
                    SaveBar.MaxHeight = 0;
                    SaveBar.Opacity = 0;
                    SaveBar.Visibility = Visibility.Collapsed;
                    SaveBar.IsHitTestVisible = false;
                }
                return;
            }

            if (dirty)
            {
                SaveBar.Visibility = Visibility.Visible;
                SaveBar.IsHitTestVisible = true;
                SaveBar.MaxHeight = 0;
                SaveBar.Opacity = 0;

                var heightAnim = new DoubleAnimation(0, SaveBarExpandedHeight, TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                heightAnim.Completed += (_, _) =>
                {
                    if (_saveBarVisible == true)
                    {
                        SaveBar.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                        SaveBar.MaxHeight = double.PositiveInfinity;
                    }
                };

                var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                SaveBar.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                SaveBar.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            }
            else
            {
                SaveBar.IsHitTestVisible = false;
                double fromHeight = SaveBar.ActualHeight > 1 ? SaveBar.ActualHeight : SaveBarExpandedHeight;

                var heightAnim = new DoubleAnimation(fromHeight, 0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                heightAnim.Completed += (_, _) =>
                {
                    if (_saveBarVisible != true)
                    {
                        SaveBar.Visibility = Visibility.Collapsed;
                        SaveBar.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                        SaveBar.MaxHeight = 0;
                        SaveBar.Opacity = 0;
                    }
                };

                var fadeAnim = new DoubleAnimation(SaveBar.Opacity, 0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                SaveBar.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                SaveBar.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            }
        }

        private void RefreshSaveDirtyBar()
        {
            if (_syncingSaveUi) return;
            bool dirty = _selectedSave != null && HasPendingSaveChanges();
            if (BtnSave != null)
                BtnSave.IsEnabled = dirty;
            UpdateSaveBarState(dirty);
        }

        private async void ChkUnlockMapRoads_Changed(object sender, RoutedEventArgs e)
        {
            RefreshSaveDirtyBar();
            if (_syncingSaveUi) return;
            if (ChkUnlockMapRoads?.IsChecked != true) return;
            if (_modScanBusy) return;
            if (_modScanCache != null && _modScanCache.DiscoverableUids.Count > 0) return;
            if (_mapDetect?.Found != true && string.IsNullOrEmpty(_mapArchiveOverride)) return;

            await ScanDetectedMapAsync(includeMapUids: true).ConfigureAwait(true);
        }

        private void SaveField_Changed(object sender, RoutedEventArgs e) => RefreshSaveDirtyBar();
        private void SaveField_Changed(object sender, TextChangedEventArgs e) => RefreshSaveDirtyBar();
        private void SaveField_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => RefreshSaveDirtyBar();

        private SaveEditSnapshot CaptureSaveEditSnapshot()
        {
            var snap = new SaveEditSnapshot
            {
                Money = TxtMoney?.Text?.Trim() ?? "",
                Xp = TxtXp?.Text?.Trim() ?? "",
                Adr = GetAdrValue(),
                SkillDist = (int)(SliderSkillDist?.Value ?? 0),
                SkillFragile = (int)(SliderSkillFragile?.Value ?? 0),
                SkillUrgent = (int)(SliderSkillUrgent?.Value ?? 0),
                SkillEco = (int)(SliderSkillEco?.Value ?? 0),
                SkillValuable = (int)(SliderSkillValuable?.Value ?? 0),
                UnlockMapCities = ChkUnlockMapCities?.IsChecked == true,
                UnlockMapRoads = ChkUnlockMapRoads?.IsChecked == true,
                RepairCabin = ChkRepairCabin?.IsChecked == true,
                RepairChassis = ChkRepairChassis?.IsChecked == true,
                RepairEngine = ChkRepairEngine?.IsChecked == true,
                RepairTransmission = ChkRepairTransmission?.IsChecked == true,
                RepairWheels = ChkRepairWheels?.IsChecked == true,
                RepairFuel = ChkRepairFuel?.IsChecked == true,
                RepairTrailerBody = ChkRepairTrailerBody?.IsChecked == true,
                RepairTrailerChassis = ChkRepairTrailerChassis?.IsChecked == true,
                RepairTrailerWheels = ChkRepairTrailerWheels?.IsChecked == true
            };

            if (_allCitiesList != null)
            {
                foreach (var city in _allCitiesList)
                    snap.Cities[city.Name ?? ""] = city.IsVisited;
            }
            if (_allGaragesList != null)
            {
                foreach (var garage in _allGaragesList)
                    snap.Garages[garage.BlockName ?? ""] = garage.Status;
            }

            return snap;
        }

        private void CaptureCommittedSaveState()
        {
            _committedSaveEdits = CaptureSaveEditSnapshot();
        }

        private void ClearCommittedSaveState()
        {
            _committedSaveEdits = null;
        }

        private bool HasPendingSaveChanges()
        {
            if (_committedSaveEdits == null || _selectedSave == null)
                return false;

            var cur = CaptureSaveEditSnapshot();
            var c = _committedSaveEdits;

            if (!string.Equals(cur.Money, c.Money, StringComparison.Ordinal)) return true;
            if (!string.Equals(cur.Xp, c.Xp, StringComparison.Ordinal)) return true;
            if (cur.Adr != c.Adr) return true;
            if (cur.SkillDist != c.SkillDist) return true;
            if (cur.SkillFragile != c.SkillFragile) return true;
            if (cur.SkillUrgent != c.SkillUrgent) return true;
            if (cur.SkillEco != c.SkillEco) return true;
            if (cur.SkillValuable != c.SkillValuable) return true;
            if (cur.UnlockMapCities != c.UnlockMapCities) return true;
            if (cur.UnlockMapRoads != c.UnlockMapRoads) return true;
            if (cur.RepairCabin != c.RepairCabin) return true;
            if (cur.RepairChassis != c.RepairChassis) return true;
            if (cur.RepairEngine != c.RepairEngine) return true;
            if (cur.RepairTransmission != c.RepairTransmission) return true;
            if (cur.RepairWheels != c.RepairWheels) return true;
            if (cur.RepairFuel != c.RepairFuel) return true;
            if (cur.RepairTrailerBody != c.RepairTrailerBody) return true;
            if (cur.RepairTrailerChassis != c.RepairTrailerChassis) return true;
            if (cur.RepairTrailerWheels != c.RepairTrailerWheels) return true;

            if (cur.Cities.Count != c.Cities.Count) return true;
            foreach (var kv in cur.Cities)
            {
                if (!c.Cities.TryGetValue(kv.Key, out bool visited) || visited != kv.Value)
                    return true;
            }

            if (cur.Garages.Count != c.Garages.Count) return true;
            foreach (var kv in cur.Garages)
            {
                if (!c.Garages.TryGetValue(kv.Key, out int status) || status != kv.Value)
                    return true;
            }

            return false;
        }

        private void UnsubscribeMapDirtyHandlers()
        {
            if (_allCitiesList != null)
            {
                foreach (var city in _allCitiesList)
                    city.PropertyChanged -= MapItem_PropertyChanged;
            }
            if (_allGaragesList != null)
            {
                foreach (var garage in _allGaragesList)
                    garage.PropertyChanged -= MapItem_PropertyChanged;
            }
        }

        private void SubscribeMapDirtyHandlers()
        {
            if (_allCitiesList != null)
            {
                foreach (var city in _allCitiesList)
                    city.PropertyChanged += MapItem_PropertyChanged;
            }
            if (_allGaragesList != null)
            {
                foreach (var garage in _allGaragesList)
                    garage.PropertyChanged += MapItem_PropertyChanged;
            }
        }

        private void MapItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_syncingSaveUi) return;
            if (e.PropertyName == nameof(CityItem.IsVisited) || e.PropertyName == nameof(GarageItem.Status))
                RefreshSaveDirtyBar();
        }

        private void UpdateSteamIdLabel()
        {
            if (TxtSteamId == null) return;

            // Show SteamID64 only for Steam source + profile that has an account id.
            if (IsSteamSourceSelected() && _selectedProfile?.SteamId64 is ulong steamId64)
            {
                TxtSteamId.Text = Loc.Tf("hdr.steamid", steamId64.ToString());
                TxtSteamId.Visibility = Visibility.Visible;
            }
            else
            {
                TxtSteamId.Text = "";
                TxtSteamId.Visibility = Visibility.Collapsed;
            }
        }

        private void TxtSearchCities_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyCitiesFilter();
        }

        private void ApplyCitiesFilter()
        {
            if (ListCities == null) return;
            string query = TxtSearchCities.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(query))
            {
                ListCities.ItemsSource = null;
                ListCities.ItemsSource = _allCitiesList;
            }
            else
            {
                ListCities.ItemsSource = null;
                ListCities.ItemsSource = _allCitiesList.FindAll(c => c.Name.ToLower().Contains(query));
            }
            UpdateMapListEmptyStates();
        }

        private void TxtSearchGarages_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyGaragesFilter();
        }

        private void ApplyGaragesFilter()
        {
            if (ListGarages == null) return;
            string query = TxtSearchGarages.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(query))
            {
                ListGarages.ItemsSource = null;
                ListGarages.ItemsSource = _allGaragesList;
            }
            else
            {
                ListGarages.ItemsSource = null;
                ListGarages.ItemsSource = _allGaragesList.FindAll(g => g.CityName.ToLower().Contains(query));
            }
            UpdateMapListEmptyStates();
        }

        private void UpdateMapListEmptyStates()
        {
            int cityCount = ListCities?.Items.Count ?? 0;
            int garageCount = ListGarages?.Items.Count ?? 0;
            bool hasAnyData = _allCitiesList.Count > 0 || _allGaragesList.Count > 0;
            bool hasSave = _selectedSave != null;

            if (TxtCitiesEmpty != null)
            {
                if (!hasSave)
                {
                    TxtCitiesEmpty.Visibility = Visibility.Collapsed;
                }
                else if (!hasAnyData)
                {
                    TxtCitiesEmpty.Text = Loc.T("map.cities.none");
                    TxtCitiesEmpty.Visibility = Visibility.Visible;
                }
                else if (cityCount == 0)
                {
                    TxtCitiesEmpty.Text = Loc.T("map.cities.nofilter");
                    TxtCitiesEmpty.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtCitiesEmpty.Visibility = Visibility.Collapsed;
                }
            }

            if (TxtGaragesEmpty != null)
            {
                if (!hasSave)
                {
                    TxtGaragesEmpty.Visibility = Visibility.Collapsed;
                }
                else if (!hasAnyData)
                {
                    TxtGaragesEmpty.Text = Loc.T("map.garages.none");
                    TxtGaragesEmpty.Visibility = Visibility.Visible;
                }
                else if (garageCount == 0)
                {
                    TxtGaragesEmpty.Text = Loc.T("map.garages.nofilter");
                    TxtGaragesEmpty.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtGaragesEmpty.Visibility = Visibility.Collapsed;
                }
            }

            if (TxtCitiesCount != null)
                TxtCitiesCount.Text = cityCount.ToString();
            if (TxtGaragesCount != null)
                TxtGaragesCount.Text = garageCount.ToString();
        }

        private void BtnSelectAllCities_Click(object sender, RoutedEventArgs e)
        {
            var visibleCities = ListCities.ItemsSource as IEnumerable<CityItem>;
            if (visibleCities != null)
            {
                foreach (var city in visibleCities)
                    city.IsVisited = true;
            }
            RefreshSaveDirtyBar();
        }

        private void BtnClearAllCities_Click(object sender, RoutedEventArgs e)
        {
            var visibleCities = ListCities.ItemsSource as IEnumerable<CityItem>;
            if (visibleCities != null)
            {
                foreach (var city in visibleCities)
                    city.IsVisited = false;
            }
            RefreshSaveDirtyBar();
        }

        private void BtnSelectAllGaragesLarge_Click(object sender, RoutedEventArgs e)
        {
            var visibleGarages = ListGarages.ItemsSource as IEnumerable<GarageItem>;
            if (visibleGarages != null)
            {
                foreach (var garage in visibleGarages)
                    garage.Status = 3;
            }
            RefreshSaveDirtyBar();
        }

        private void BtnClearAllGarages_Click(object sender, RoutedEventArgs e)
        {
            var visibleGarages = ListGarages.ItemsSource as IEnumerable<GarageItem>;
            if (visibleGarages != null)
            {
                foreach (var garage in visibleGarages)
                    garage.Status = 0;
            }
            RefreshSaveDirtyBar();
        }

        private void SetStatus(string text, string hexColor)
        {
            // Status bar removed — keep call sites as no-ops
        }

        private enum AppDialogKind { Info, Success, Warning, Error }

        private void ShowAppDialog(string title, string message, AppDialogKind kind = AppDialogKind.Info)
        {
            if (AppDialogOverlay == null) return;

            AppDialogTitle.Text = title ?? "";
            AppDialogMessage.Text = (message ?? "").Trim();

            switch (kind)
            {
                case AppDialogKind.Success:
                    AppDialogIconBg.Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x3A, 0x2A));
                    AppDialogIcon.Fill = new SolidColorBrush(Color.FromRgb(0x69, 0xF0, 0xAE));
                    AppDialogIcon.Data = Geometry.Parse("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z");
                    break;
                case AppDialogKind.Warning:
                    AppDialogIconBg.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x32, 0x1B));
                    AppDialogIcon.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
                    AppDialogIcon.Data = Geometry.Parse("M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z");
                    break;
                case AppDialogKind.Error:
                    AppDialogIconBg.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x1B, 0x1B));
                    AppDialogIcon.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x52, 0x52));
                    AppDialogIcon.Data = Geometry.Parse("M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z");
                    break;
                default:
                    var accent = (Brush)FindResource("AccentBrush");
                    var accentBg = (Brush)FindResource("AccentBgBrush");
                    AppDialogIconBg.Background = accentBg;
                    AppDialogIcon.Fill = accent;
                    AppDialogIcon.Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z");
                    break;
            }

            if (BtnAppDialogOk != null)
                BtnAppDialogOk.Content = Loc.T("dialog.ok");

            AppDialogOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            AppDialogScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            AppDialogScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            AppDialogOverlay.Visibility = Visibility.Visible;
            AppDialogOverlay.Opacity = 0;
            AppDialogScale.ScaleX = 0.94;
            AppDialogScale.ScaleY = 0.94;

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var scale = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            AppDialogOverlay.BeginAnimation(UIElement.OpacityProperty, fade);
            AppDialogScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            AppDialogScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
            BtnAppDialogOk.Focus();
        }

        private void BtnAppDialogOk_Click(object sender, RoutedEventArgs e)
        {
            HideAppDialog();
        }

        private void HideAppDialog()
        {
            if (AppDialogOverlay == null || AppDialogOverlay.Visibility != Visibility.Visible)
                return;

            var fade = new DoubleAnimation(AppDialogOverlay.Opacity, 0, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var scale = new DoubleAnimation(1, 0.96, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (_, _) =>
            {
                AppDialogOverlay.Visibility = Visibility.Collapsed;
                AppDialogOverlay.Opacity = 0;
            };
            AppDialogOverlay.BeginAnimation(UIElement.OpacityProperty, fade);
            AppDialogScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            AppDialogScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        }

        private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
        {
            HidePanelOverlay(AboutOverlay, AboutScale);
            SyncSettingsUiFromCommitted();
            ShowPanelOverlay(SettingsOverlay, SettingsScale);
        }

        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            SyncSettingsUiFromCommitted();
            HidePanelOverlay(SettingsOverlay, SettingsScale);
        }

        private void BtnOpenAbout_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsOverlay?.Visibility == Visibility.Visible)
                SyncSettingsUiFromCommitted();
            HidePanelOverlay(SettingsOverlay, SettingsScale);
            ShowPanelOverlay(AboutOverlay, AboutScale);
        }

        private void BtnCloseAbout_Click(object sender, RoutedEventArgs e)
        {
            HidePanelOverlay(AboutOverlay, AboutScale);
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_updateBusy) return;

            if (_updateButtonMode == UpdateButtonMode.OpenRelease)
            {
                string url = _pendingUpdate?.HtmlUrl ?? AppInfo.ReleasesPageUrl;
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    ShowAppDialog(Loc.T("dialog.error"), Loc.Tf("about.update.error", ex.Message), AppDialogKind.Error);
                }
                return;
            }

            if (_updateButtonMode == UpdateButtonMode.Download && _pendingUpdate != null)
            {
                await DownloadAndInstallUpdateAsync(_pendingUpdate);
                return;
            }

            await CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            _updateBusy = true;
            SetUpdateUi(Loc.T("about.update.checking"), enabled: false);

            try
            {
                var result = await UpdateService.CheckLatestAsync().ConfigureAwait(true);
                switch (result.Status)
                {
                    case UpdateCheckStatus.UpToDate:
                        _pendingUpdate = null;
                        _updateButtonMode = UpdateButtonMode.Check;
                        SetUpdateUi(
                            Loc.Tf("about.update.latest", AppInfo.VersionLabel),
                            enabled: true,
                            buttonKey: "about.update.btn");
                        break;

                    case UpdateCheckStatus.UpdateAvailable:
                        _pendingUpdate = result.Latest;
                        if (string.IsNullOrEmpty(result.Latest?.DownloadUrl))
                        {
                            _updateButtonMode = UpdateButtonMode.OpenRelease;
                            SetUpdateUi(
                                Loc.T("about.update.noasset"),
                                enabled: true,
                                buttonKey: "about.update.open");
                        }
                        else
                        {
                            _updateButtonMode = UpdateButtonMode.Download;
                            SetUpdateUi(
                                Loc.Tf("about.update.available", "v" + result.Latest!.Version),
                                enabled: true,
                                buttonKey: "about.update.download");
                        }
                        break;

                    case UpdateCheckStatus.NoRelease:
                        _pendingUpdate = null;
                        _updateButtonMode = UpdateButtonMode.Check;
                        SetUpdateUi(Loc.T("about.update.none"), enabled: true, buttonKey: "about.update.btn");
                        break;

                    default:
                        _pendingUpdate = null;
                        _updateButtonMode = UpdateButtonMode.Check;
                        SetUpdateUi(
                            Loc.Tf("about.update.error", result.ErrorMessage ?? "unknown"),
                            enabled: true,
                            buttonKey: "about.update.btn");
                        break;
                }
            }
            catch (Exception ex)
            {
                _pendingUpdate = null;
                _updateButtonMode = UpdateButtonMode.Check;
                SetUpdateUi(Loc.Tf("about.update.error", ex.Message), enabled: true, buttonKey: "about.update.btn");
            }
            finally
            {
                _updateBusy = false;
            }
        }

        private async Task DownloadAndInstallUpdateAsync(UpdateInfo info)
        {
            if (string.IsNullOrEmpty(info.DownloadUrl)) return;

            _updateBusy = true;
            SetUpdateUi(Loc.Tf("about.update.downloading", 0), enabled: false);

            string tempExe = Path.Combine(
                Path.GetTempPath(),
                "TSSaveEditor_update_" + info.Version + ".exe");

            try
            {
                var progress = new Progress<double>(p =>
                {
                    SetUpdateUi(Loc.Tf("about.update.downloading", p), enabled: false);
                });

                await UpdateService.DownloadAsync(info.DownloadUrl, tempExe, progress).ConfigureAwait(true);
                SetUpdateUi(Loc.T("about.update.installing"), enabled: false);
                UpdateService.ApplyAndRestart(tempExe);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tempExe)) File.Delete(tempExe); } catch { }
                _updateButtonMode = UpdateButtonMode.Download;
                SetUpdateUi(Loc.Tf("about.update.error", ex.Message), enabled: true, buttonKey: "about.update.download");
                ShowAppDialog(Loc.T("dialog.error"), Loc.Tf("about.update.error", ex.Message), AppDialogKind.Error);
            }
            finally
            {
                _updateBusy = false;
            }
        }

        private void SetUpdateUi(string hint, bool enabled, string? buttonKey = null)
        {
            if (AboutUpdateHint != null)
                AboutUpdateHint.Text = hint ?? "";

            if (BtnCheckUpdate != null)
                BtnCheckUpdate.IsEnabled = enabled;

            if (BtnCheckUpdateLabel != null && !string.IsNullOrEmpty(buttonKey))
                BtnCheckUpdateLabel.Text = Loc.T(buttonKey);
        }

        private async void BtnBrowseMapArchive_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = Loc.T("map.detect.browse"),
                Filter = "SCS archives (*.scs)|*.scs|All files (*.*)|*.*",
                Multiselect = false,
                CheckFileExists = true
            };

            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string etsMod = Path.Combine(docs, "Euro Truck Simulator 2", "mod");
            string atsMod = Path.Combine(docs, "American Truck Simulator", "mod");
            if (Directory.Exists(etsMod)) dlg.InitialDirectory = etsMod;
            else if (Directory.Exists(atsMod)) dlg.InitialDirectory = atsMod;

            if (dlg.ShowDialog() != true || string.IsNullOrEmpty(dlg.FileName))
                return;

            _mapArchiveOverride = dlg.FileName;
            _modScanCache = null;
            if (_mapDetect == null)
                _mapDetect = new MapModDetectionResult();
            _mapDetect.ArchivePath = dlg.FileName;
            _mapDetect.DisplayName = Path.GetFileNameWithoutExtension(dlg.FileName);
            await ScanDetectedMapAsync().ConfigureAwait(true);
        }

        private async void BtnScanMods_Click(object sender, RoutedEventArgs e)
        {
            await RefreshModsCatalogAsync(rescan: true).ConfigureAwait(true);
        }

        private void ChkModsCompatibleOnly_Changed(object sender, RoutedEventArgs e)
        {
            BindModsListsFromSnapshot();
        }

        private async void BtnBrowseModsFolder_Click(object sender, RoutedEventArgs e)
        {
            bool preferAts = ComboGame?.SelectedIndex == 1;
            string initial = _modsFolderOverride
                ?? ModCatalog.ResolveDefaultModFolder(preferAts)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var dlg = new OpenFolderDialog
            {
                Title = Loc.T("mods.browse"),
                InitialDirectory = Directory.Exists(initial) ? initial : null
            };
            if (dlg.ShowDialog() != true || string.IsNullOrEmpty(dlg.FolderName))
                return;

            _modsFolderOverride = dlg.FolderName;
            await RefreshModsCatalogAsync(rescan: true).ConfigureAwait(true);
        }

        private async Task RefreshModsCatalogAsync(bool rescan)
        {
            if (ListModsFolder == null || ListModsActive == null) return;

            int token = ++_modsCatalogToken;
            if (TxtModsScanStatus != null)
                TxtModsScanStatus.Text = Loc.T("mods.status.scanning");
            if (BtnScanMods != null) BtnScanMods.IsEnabled = false;
            // Show progress when rescanning or when old cache needs manifest enrich.
            SetModsScanProgressVisible(true);

            bool preferAts = ComboGame?.SelectedIndex == 1;
            string? folder = _modsFolderOverride;
            string? savePath = _selectedSave?.Path;

            ModCatalogSnapshot snap;
            try
            {
                var progress = new Progress<ScanProgressInfo>(info =>
                {
                    if (token != _modsCatalogToken) return;
                    ApplyModsScanProgress(info);
                });

                snap = await Task.Run(() =>
                {
                    string? modFolder = folder;
                    if (string.IsNullOrWhiteSpace(modFolder))
                        modFolder = ModCatalog.ResolveDefaultModFolder(preferAts);
                    return ModCatalog.BuildSnapshot(
                        modFolder,
                        savePath,
                        rescan,
                        progress: progress,
                        preferAts: preferAts);
                }).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                if (token != _modsCatalogToken) return;
                if (TxtModsScanStatus != null)
                    TxtModsScanStatus.Text = ex.Message;
                return;
            }
            finally
            {
                if (token == _modsCatalogToken)
                {
                    if (BtnScanMods != null) BtnScanMods.IsEnabled = true;
                    SetModsScanProgressVisible(false);
                }
            }

            if (token != _modsCatalogToken) return;

            _modsSnapshot = snap;

            if (!string.IsNullOrEmpty(snap.ModFolder))
                _modsFolderOverride ??= snap.ModFolder;

            if (TxtModsFolderPath != null)
                TxtModsFolderPath.Text = snap.ModFolder ?? "—";

            if (TxtModsGameVersion != null)
            {
                TxtModsGameVersion.Text = string.IsNullOrEmpty(snap.GameVersion)
                    ? Loc.T("mods.game.version.unknown")
                    : Loc.Tf("mods.game.version", snap.GameVersion);
            }

            BindModsListsFromSnapshot();
        }

        private void BindModsListsFromSnapshot()
        {
            if (ListModsFolder == null || ListModsActive == null) return;
            var snap = _modsSnapshot;
            if (snap == null)
            {
                ListModsFolder.ItemsSource = new[] { new ModListRow { Title = Loc.T("mods.empty.folder"), Subtitle = "", Label = Loc.T("mods.empty.folder") } };
                ListModsActive.ItemsSource = new[] { new ModListRow { Title = Loc.T("mods.empty.active"), Subtitle = "", Label = Loc.T("mods.empty.active") } };
                return;
            }

            bool onlyCompat = ChkModsCompatibleOnly?.IsChecked != false;
            string? gameVer = snap.GameVersion;

            var activeIds = new HashSet<string>(
                snap.ActiveMods.Select(m => m.Id),
                StringComparer.OrdinalIgnoreCase);
            var folderIds = new HashSet<string>(
                snap.FolderMods.Select(m => m.Id),
                StringComparer.OrdinalIgnoreCase);

            IEnumerable<ModFolderEntry> folderQuery = snap.FolderMods;
            if (onlyCompat && !string.IsNullOrEmpty(gameVer))
                folderQuery = folderQuery.Where(m => ModVersioning.IsCompatibleWith(m, gameVer));

            var folderList = folderQuery.ToList();
            var folderRows = folderList.Select(m =>
            {
                string badge = activeIds.Contains(m.Id) ? Loc.T("mods.badge.active") : "";
                string ver = FormatModVersions(m.ManifestParsed, m.CompatibleVersions);
                string sub = $"{m.SizeLabel}  ·  {ver}";
                if (!string.IsNullOrEmpty(badge))
                    sub += $"  ·  {badge}";
                return new ModListRow
                {
                    Title = ModVersioning.SanitizeText(m.FileName),
                    Subtitle = sub,
                    Label = $"{m.FileName}  ·  {sub}"
                };
            }).ToList();

            if (folderRows.Count == 0)
                folderRows.Add(new ModListRow { Title = Loc.T("mods.empty.folder"), Subtitle = "", Label = Loc.T("mods.empty.folder") });

            var activeRows = snap.ActiveMods.Select(m =>
            {
                string name = string.IsNullOrWhiteSpace(m.DisplayName) ? m.Id : m.DisplayName;
                name = ModVersioning.SanitizeText(name);
                bool inFolder = folderIds.Contains(m.Id);
                var bits = new List<string>();

                if (IsWorkshopModId(m.Id))
                    bits.Add(Loc.T("mods.source.workshop"));
                else if (!string.IsNullOrWhiteSpace(m.Id)
                         && !string.Equals(m.Id, name, StringComparison.OrdinalIgnoreCase))
                    bits.Add(ModVersioning.SanitizeText(m.Id));

                if (inFolder)
                    bits.Add(FormatModVersions(m.ManifestParsed, m.CompatibleVersions));
                else
                    bits.Add(Loc.T("mods.badge.missing"));

                if (m.CompatibleWithGame == false)
                    bits.Add(Loc.T("mods.badge.incompatible"));

                string sub = string.Join(" · ", bits.Where(s => !string.IsNullOrWhiteSpace(s)));
                return new ModListRow
                {
                    Title = name,
                    Subtitle = sub,
                    Label = string.IsNullOrEmpty(sub) ? name : $"{name} · {sub}"
                };
            }).ToList();

            if (activeRows.Count == 0)
                activeRows.Add(new ModListRow { Title = Loc.T("mods.empty.active"), Subtitle = "", Label = Loc.T("mods.empty.active") });

            ListModsFolder.ItemsSource = folderRows;
            ListModsActive.ItemsSource = activeRows;

            if (TxtModsFolderCount != null)
                TxtModsFolderCount.Text = folderList.Count.ToString();
            if (TxtModsActiveCount != null)
                TxtModsActiveCount.Text = snap.ActiveMods.Count.ToString();

            if (TxtModsScanStatus != null)
            {
                if (snap.ScannedAtUtc.HasValue)
                {
                    string when = snap.ScannedAtUtc.Value.ToLocalTime().ToString("g");
                    if (onlyCompat && !string.IsNullOrEmpty(gameVer))
                        TxtModsScanStatus.Text = Loc.Tf("mods.status.filtered", when, folderList.Count, snap.FolderMods.Count);
                    else
                        TxtModsScanStatus.Text = Loc.Tf("mods.status.ready", when, snap.FolderMods.Count);
                }
                else
                    TxtModsScanStatus.Text = Loc.T("mods.status.none");

                if (snap.Warnings.Count > 0)
                    TxtModsScanStatus.Text += " · " + snap.Warnings[0];
            }
        }

        private static string FormatModVersions(bool manifestParsed, IList<string>? versions)
        {
            if (!manifestParsed)
                return Loc.T("mods.ver.unknown");
            if (versions == null || versions.Count == 0)
                return Loc.T("mods.ver.all");
            return string.Join(", ", versions);
        }

        private static bool IsWorkshopModId(string? id) =>
            !string.IsNullOrEmpty(id)
            && id.StartsWith("mod_workshop_package.", StringComparison.OrdinalIgnoreCase);

        private void SetModsScanProgressVisible(bool visible)
        {
            if (PanelModsScanProgress != null)
                PanelModsScanProgress.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (!visible && BarModsScan != null)
            {
                BarModsScan.IsIndeterminate = false;
                BarModsScan.Value = 0;
            }
        }

        private void ApplyModsScanProgress(ScanProgressInfo info)
        {
            if (PanelModsScanProgress != null && PanelModsScanProgress.Visibility != Visibility.Visible)
                PanelModsScanProgress.Visibility = Visibility.Visible;

            if (TxtModsScanProgress != null)
            {
                TxtModsScanProgress.Text = info.Total > 0
                    ? Loc.Tf("mods.progress", info.Message ?? "", info.Current, info.Total)
                    : (info.Message ?? "");
            }

            if (BarModsScan != null)
            {
                if (info.Total <= 0)
                {
                    BarModsScan.IsIndeterminate = true;
                }
                else
                {
                    BarModsScan.IsIndeterminate = false;
                    BarModsScan.Maximum = 100;
                    BarModsScan.Value = info.Fraction * 100;
                }
            }

            if (TxtModsScanPercent != null)
            {
                TxtModsScanPercent.Text = info.Total > 0
                    ? Loc.Tf("mods.progress.pct", (int)Math.Round(info.Fraction * 100))
                    : "";
            }
        }

        private void RefreshMapDetectSummary()
        {
            if (TxtMapDetectSummary == null) return;

            if (_modScanBusy)
            {
                TxtMapDetectSummary.Text = Loc.Tf("map.detect.scanning",
                    ModVersioning.SanitizeText(_mapDetect?.DisplayName ?? _mapDetect?.MapPath ?? "…"));
                return;
            }

            if (_modScanCache != null && (_mapDetect?.Found == true || !string.IsNullOrEmpty(_mapArchiveOverride)))
            {
                string label = ModVersioning.SanitizeText(
                    _mapDetect?.DisplayName
                    ?? Path.GetFileNameWithoutExtension(_mapArchiveOverride ?? _mapDetect?.ArchivePath)
                    ?? "map");
                string file = Path.GetFileName(_mapArchiveOverride ?? _mapDetect?.ArchivePath ?? "");
                TxtMapDetectSummary.Text = Loc.Tf(
                    "map.detect.ready",
                    label,
                    string.IsNullOrEmpty(file) ? $"{_modScanCache.ScannedArchives.Count} scs" : file,
                    _modScanCache.Cities.Count,
                    _modScanCache.DiscoverableUids.Count);
                if (BtnBrowseMapArchive != null)
                    BtnBrowseMapArchive.Visibility = Visibility.Collapsed;
                return;
            }

            if (_mapDetect != null && !string.IsNullOrEmpty(_mapDetect.MapPath) && !_mapDetect.Found)
            {
                TxtMapDetectSummary.Text = Loc.Tf("map.detect.missing", _mapDetect.MapPath);
                if (BtnBrowseMapArchive != null)
                    BtnBrowseMapArchive.Visibility = Visibility.Visible;
                return;
            }

            TxtMapDetectSummary.Text = Loc.T("map.detect.idle");
            if (BtnBrowseMapArchive != null)
                BtnBrowseMapArchive.Visibility = Visibility.Collapsed;
        }

        private async Task DetectAndScanMapAsync()
        {
            if (_selectedSave == null) return;

            _modScanCache = null;
            if (!string.IsNullOrEmpty(_mapArchiveOverride))
            {
                var det = await Task.Run(() => MapModDetector.Detect(_selectedSave.Path)).ConfigureAwait(true);
                _mapDetect = det ?? new MapModDetectionResult();
                _mapDetect.ArchivePath = _mapArchiveOverride;
                _mapDetect.DisplayName = Path.GetFileNameWithoutExtension(_mapArchiveOverride);
            }
            else
            {
                _mapDetect = await Task.Run(() => MapModDetector.Detect(_selectedSave.Path)).ConfigureAwait(true);
            }

            RefreshMapDetectSummary();

            if (_mapDetect?.Found == true)
                await ScanDetectedMapAsync().ConfigureAwait(true);
            else
                RefreshMapDetectSummary();
        }

        private async Task ScanDetectedMapAsync(bool includeMapUids = false)
        {
            if (_mapDetect == null && string.IsNullOrEmpty(_mapArchiveOverride)) return;
            if (_modScanBusy) return;

            var paths = _mapDetect != null
                ? MapModDetector.ResolveScanArchives(_mapDetect, _mapArchiveOverride)
                : new List<string>();
            if (!string.IsNullOrEmpty(_mapArchiveOverride)
                && !paths.Contains(_mapArchiveOverride, StringComparer.OrdinalIgnoreCase))
                paths.Insert(0, _mapArchiveOverride);

            // ZIP map packs often split map/def/material as sibling .scs in one folder.
            // Only pull siblings for city defs when browsing — never for full map UID load of every .scs.
            if (!string.IsNullOrEmpty(_mapArchiveOverride) && !includeMapUids)
            {
                string? dir = Path.GetDirectoryName(_mapArchiveOverride);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    foreach (var sib in Directory.EnumerateFiles(dir, "*.scs"))
                    {
                        if (!paths.Contains(sib, StringComparer.OrdinalIgnoreCase))
                            paths.Add(sib);
                    }
                }
            }

            if (paths.Count == 0 && !string.IsNullOrEmpty(_mapArchiveOverride))
                paths.Add(_mapArchiveOverride);

            if (paths.Count == 0) return;

            // Fog unlock: only the map-owner archive (not every active mod .scs).
            if (includeMapUids)
            {
                string owner = _mapArchiveOverride ?? _mapDetect?.ArchivePath;
                if (!string.IsNullOrEmpty(owner))
                    paths = new List<string> { owner };
                else if (paths.Count > 1)
                    paths = new List<string> { paths[0] };
            }

            int token = ++_modScanToken;
            _modScanBusy = true;
            SetMapScanProgressVisible(true);
            RefreshMapDetectSummary();

            string preferred = _mapDetect?.MapPath;
            try
            {
                var progress = new Progress<ScanProgressInfo>(info =>
                {
                    if (token != _modScanToken) return;
                    ApplyMapScanProgress(info);
                });

                // Keep previous UIDs if this is cities-only refresh after a fog scan.
                var prevUids = (!includeMapUids && _modScanCache != null)
                    ? _modScanCache.DiscoverableUids.ToList()
                    : null;

                ModMapScanResult result = await Task.Run(() =>
                        ModMapUnlocker.Scan(paths, progress, preferred, includeMapUids))
                    .ConfigureAwait(true);

                if (token != _modScanToken) return;

                if (prevUids != null && result.DiscoverableUids.Count == 0 && prevUids.Count > 0)
                    result.DiscoverableUids.AddRange(prevUids);

                _modScanCache = result;
                if (_mapDetect != null && string.IsNullOrEmpty(_mapDetect.ArchivePath) && paths.Count > 0)
                    _mapDetect.ArchivePath = paths[0];
                MergeMapCitiesIntoList(result);
                RefreshMapDetectSummary();

                // Only popup on fog scan (heavy); city-only noise is shown in summary.
                if (includeMapUids && result.Warnings.Count > 0)
                {
                    string warn = string.Join("\n", result.Warnings.Take(6));
                    ShowAppDialog(Loc.T("dialog.warning"), Loc.Tf("map.detect.failed", warn), AppDialogKind.Warning);
                }
            }
            catch (Exception ex)
            {
                if (token != _modScanToken) return;
                if (includeMapUids)
                    _modScanCache = null;
                RefreshMapDetectSummary();
                ShowAppDialog(Loc.T("dialog.error"), Loc.Tf("map.detect.failed", ex.Message), AppDialogKind.Error);
            }
            finally
            {
                if (token == _modScanToken)
                {
                    _modScanBusy = false;
                    SetMapScanProgressVisible(false);
                    RefreshMapDetectSummary();
                }
            }
        }

        private void SetMapScanProgressVisible(bool visible)
        {
            if (PanelMapScanProgress != null)
                PanelMapScanProgress.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (!visible && BarMapScan != null)
            {
                BarMapScan.IsIndeterminate = false;
                BarMapScan.Value = 0;
            }
        }

        private void ApplyMapScanProgress(ScanProgressInfo info)
        {
            if (PanelMapScanProgress != null && PanelMapScanProgress.Visibility != Visibility.Visible)
                PanelMapScanProgress.Visibility = Visibility.Visible;

            if (TxtMapDetectSummary != null)
                TxtMapDetectSummary.Text = Loc.Tf("map.detect.scanning", info.Message ?? "");

            if (TxtMapScanProgress != null)
            {
                TxtMapScanProgress.Text = info.Total > 0
                    ? Loc.Tf("map.detect.progress", info.Message ?? "", info.Current, info.Total)
                    : (info.Message ?? "");
            }

            if (BarMapScan != null)
            {
                if (info.Total <= 0)
                {
                    BarMapScan.IsIndeterminate = true;
                }
                else
                {
                    BarMapScan.IsIndeterminate = false;
                    BarMapScan.Maximum = 100;
                    BarMapScan.Value = info.Fraction * 100;
                }
            }

            if (TxtMapScanPercent != null)
            {
                TxtMapScanPercent.Text = info.Total > 0
                    ? Loc.Tf("map.detect.progress.pct", (int)Math.Round(info.Fraction * 100))
                    : "";
            }
        }

        private void MergeMapCitiesIntoList(ModMapScanResult scan)
        {
            if (scan == null || _allCitiesList == null) return;
            bool added = false;
            var existing = new HashSet<string>(_allCitiesList.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var city in scan.Cities)
            {
                if (string.IsNullOrWhiteSpace(city) || existing.Contains(city)) continue;
                _allCitiesList.Add(new CityItem { Name = city, IsVisited = false });
                existing.Add(city);
                added = true;
            }

            if (!added) return;
            _allCitiesList.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            if (ListCities != null)
            {
                ListCities.ItemsSource = null;
                ListCities.ItemsSource = _allCitiesList;
            }
            UpdateMapListEmptyStates();
        }

        private async Task<ModMapScanResult> EnsureModScanAsync()
        {
            if (_modScanCache != null) return _modScanCache;
            if (_mapDetect?.Found != true && string.IsNullOrEmpty(_mapArchiveOverride)) return null;
            await ScanDetectedMapAsync().ConfigureAwait(true);
            return _modScanCache;
        }

        private void PanelOverlayBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!ReferenceEquals(e.Source, sender)) return;
            if (ReferenceEquals(sender, SettingsOverlay))
            {
                SyncSettingsUiFromCommitted();
                HidePanelOverlay(SettingsOverlay, SettingsScale);
            }
            else if (ReferenceEquals(sender, AboutOverlay))
                HidePanelOverlay(AboutOverlay, AboutScale);
        }

        private void PanelOverlayCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void ShowPanelOverlay(Border overlay, ScaleTransform scale)
        {
            if (overlay == null || scale == null) return;

            _hidingOverlays.Remove(overlay);

            overlay.BeginAnimation(UIElement.OpacityProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            overlay.Visibility = Visibility.Visible;
            overlay.IsHitTestVisible = true;
            overlay.Opacity = 0;
            scale.ScaleX = 0.96;
            scale.ScaleY = 0.96;

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var grow = new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            overlay.BeginAnimation(UIElement.OpacityProperty, fade);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }

        private void HidePanelOverlay(Border overlay, ScaleTransform scale, bool animate = true)
        {
            if (overlay == null || overlay.Visibility != Visibility.Visible) return;

            overlay.BeginAnimation(UIElement.OpacityProperty, null);
            if (scale != null)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            }

            if (!animate)
            {
                _hidingOverlays.Remove(overlay);
                overlay.Visibility = Visibility.Collapsed;
                overlay.IsHitTestVisible = false;
                overlay.Opacity = 0;
                if (scale != null) { scale.ScaleX = 0.96; scale.ScaleY = 0.96; }
                return;
            }

            _hidingOverlays.Add(overlay);
            overlay.IsHitTestVisible = false;
            double fromOpacity = overlay.Opacity > 0.01 ? overlay.Opacity : 1;

            var fade = new DoubleAnimation(fromOpacity, 0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (_, _) =>
            {
                if (!_hidingOverlays.Remove(overlay)) return;
                overlay.Visibility = Visibility.Collapsed;
                overlay.Opacity = 0;
                if (scale != null) { scale.ScaleX = 0.96; scale.ScaleY = 0.96; }
            };
            overlay.BeginAnimation(UIElement.OpacityProperty, fade);
            if (scale != null)
            {
                double fromScale = scale.ScaleX > 0.5 ? scale.ScaleX : 1;
                var shrink = new DoubleAnimation(fromScale, 0.94, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
            }
        }

        private void Navigation_Checked(object sender, RoutedEventArgs e)
        {
            if (MainTabControl == null) return;

            var button = sender as RadioButton;
            if (button == null) return;

            int tabIndex = button.Name switch
            {
                "NavEconomy" => 0,
                "NavMap"     => 1,
                "NavVehicle" => 2,
                "NavMods"    => 3,
                _ => 0
            };

            MainTabControl.SelectedIndex = tabIndex;

            if (tabIndex == 3)
                _ = RefreshModsCatalogAsync(rescan: false);

            // Soft fade for the newly shown tab content
            if (MainTabControl.SelectedContent is FrameworkElement page)
            {
                page.Opacity = 0;
                var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(140)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                page.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        private void RefreshProfiles()
        {
            // Guard: controls may not be ready yet during InitializeComponent
            if (TxtCustomPath == null || ComboGame == null) return;

            try
            {
                SetStatus(Loc.T("status.scan"), "#FFD700");

                string customPath = _committedCustomPath;
                var gameType = ComboGame.SelectedIndex == 0 ? GameType.ETS2 : GameType.ATS;

                var allProfiles = PathScanner.ScanProfiles(customPath, gameType);
                _allGameProfiles = allProfiles.FindAll(p => p.Game == gameType);
                ApplyProfileSourceFilter();
            }
            catch (Exception ex)
            {
                ShowAppDialog(Loc.T("dialog.error"), Loc.Tf("dialog.profiles.err", ex.Message), AppDialogKind.Error);
                UpdateSelectorsEnabled();
            }
        }

        private bool IsSteamSourceSelected()
        {
            if (ComboSource?.SelectedItem is ComboBoxItem item && item.Tag != null)
                return string.Equals(item.Tag.ToString(), "steam", StringComparison.OrdinalIgnoreCase);
            return false;
        }

        private void ApplyProfileSourceFilter()
        {
            if (ComboProfile == null) return;

            bool steam = IsSteamSourceSelected();
            _profiles = (_allGameProfiles ?? new List<GameProfile>()).FindAll(p =>
                steam ? p.IsSteamSource : !p.IsSteamSource);

            string keepId = _selectedProfile?.Id;
            ComboProfile.ItemsSource = null;
            ComboProfile.ItemsSource = _profiles;

            if (ComboProfile.Items.Count > 0)
            {
                int idx = 0;
                if (!string.IsNullOrEmpty(keepId))
                {
                    for (int i = 0; i < _profiles.Count; i++)
                    {
                        if (string.Equals(_profiles[i].Id, keepId, StringComparison.OrdinalIgnoreCase))
                        {
                            idx = i;
                            break;
                        }
                    }
                }
                ComboProfile.SelectedIndex = idx;
            }
            else
            {
                ComboProfile.SelectedItem = null;
                if (ComboSave != null) ComboSave.ItemsSource = null;
                SetStatus(Loc.T("status.ready"), "#69F0AE");
            }

            UpdateSelectorsEnabled();
            UpdateSteamIdLabel();
        }

        private void ComboSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            ApplyProfileSourceFilter();
        }

        private void UpdateSelectorsEnabled()
        {
            bool hasProfiles = _profiles != null && _profiles.Count > 0;
            bool hasProfile = _selectedProfile != null;
            bool hasSave = _selectedSave != null;

            if (ComboProfile != null)
                ComboProfile.IsEnabled = hasProfiles;
            if (ComboSave != null)
                ComboSave.IsEnabled = hasProfiles && hasProfile;

            if (BtnSave != null) BtnSave.IsEnabled = hasSave && HasPendingSaveChanges();
            if (ChkUnlockMapCities != null) ChkUnlockMapCities.IsEnabled = hasSave;
            if (ChkUnlockMapRoads != null) ChkUnlockMapRoads.IsEnabled = hasSave;
            if (BtnBrowseMapArchive != null) BtnBrowseMapArchive.IsEnabled = hasSave;

            // Scroll viewers stay enabled: IsEnabled=false greys text through the overlay.
            // No-save overlay already blocks input.

            UpdateNoSaveOverlays();
            UpdateSaveBarState(hasSave && HasPendingSaveChanges());
            UpdateMapListEmptyStates();
        }

        private void RefreshSaves()
        {
            if (_selectedProfile == null)
            {
                ComboSave.ItemsSource = null;
                _saves = new List<SaveGame>();
                UpdateSelectorsEnabled();
                return;
            }

            SetStatus(Loc.T("status.saves"), "#FFD700");

            _saves = PathScanner.ScanSaves(_selectedProfile);
            ComboSave.ItemsSource = null;
            ComboSave.ItemsSource = _saves;

            if (ComboSave.Items.Count > 0)
            {
                ComboSave.SelectedIndex = 0;
            }
            else
            {
                ComboSave.SelectedItem = null;
                SetStatus(Loc.T("status.ready"), "#69F0AE");
            }

            UpdateSelectorsEnabled();
        }

        private void LoadSaveData()
        {
            if (_selectedSave == null) return;

            // Clear the wear and fuel displays so stale data doesn't persist if parsing fails
            ClearWearDisplays();

            _syncingSaveUi = true;
            try
            {
                SetStatus(Loc.T("status.loading"), "#FFD700");

                string gameSii = Path.Combine(_selectedSave.Path, "game.sii");
                string decryptedText = SaveEngine.DecryptFile(gameSii, _committedDecryptorPath);

                if (string.IsNullOrEmpty(decryptedText))
                {
                    ClearFleetLists();
                    _loadedSaveText = null;
                    ClearCommittedSaveState();
                    SetStatus(Loc.T("status.decrypt.err"), "#FF5252");
                    return;
                }

                // Economy: XP / skills / ADR from economy; money from bank (new) or economy (legacy)
                string econBlock = ExtractBlock(decryptedText, "economy");
                if (econBlock != null)
                {
                    string moneyBlock = null;
                    var bankRef = Regex.Match(econBlock, @"(?m)^\s*bank:\s*(\S+)");
                    if (bankRef.Success)
                    {
                        string bankId = bankRef.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(bankId) &&
                            !bankId.Equals("null", StringComparison.OrdinalIgnoreCase))
                            moneyBlock = ExtractBlock(decryptedText, "bank", bankId);
                    }
                    // Legacy: money_account still inside economy
                    if (moneyBlock == null)
                        moneyBlock = econBlock;

                    var moneyMatch = Regex.Match(moneyBlock, @"money_account:\s*(&H[0-9a-fA-F]+|-?\d+)");
                    if (moneyMatch.Success)
                    {
                        string rawMoney = moneyMatch.Groups[1].Value;
                        if (rawMoney.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
                        {
                            if (long.TryParse(rawMoney.Substring(2), System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out long hexMoney))
                                TxtMoney.Text = hexMoney.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else if (long.TryParse(rawMoney, System.Globalization.NumberStyles.Integer,
                                     System.Globalization.CultureInfo.InvariantCulture, out long moneyVal))
                        {
                            TxtMoney.Text = moneyVal.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                    }

                    var xpMatch = Regex.Match(econBlock, @"experience_points:\s*(-?\d+)");
                    if (xpMatch.Success)
                    {
                        TxtXp.Text = xpMatch.Groups[1].Value;
                        UpdatePlayerLevelFromXp();
                    }

                    var adrMatch = Regex.Match(econBlock, @"adr:\s*(\d+)");
                    if (adrMatch.Success && int.TryParse(adrMatch.Groups[1].Value, out int adrVal))
                        SetAdrCheckboxes(adrVal);
                    else
                        ClearAdrCheckboxes();

                    // Progressive Skills
                    SetSkillSlider(SliderSkillDist,     Regex.Match(econBlock, @"long_dist:\s*(\d+)"));
                    SetSkillSlider(SliderSkillFragile,   Regex.Match(econBlock, @"fragile:\s*(\d+)"));
                    SetSkillSlider(SliderSkillUrgent,    Regex.Match(econBlock, @"urgent:\s*(\d+)"));
                    SetSkillSlider(SliderSkillEco,       Regex.Match(econBlock, @"mechanical:\s*(\d+)"));
                    // SCS stores High Value cargo skill as "heavy" (not "valuable")
                    SetSkillSlider(SliderSkillValuable,  Regex.Match(econBlock, @"heavy:\s*(\d+)"));
                }

                // Vehicle: find player truck/trailer IDs then parse their wear/fuel
                string assignedTruckId = null;
                string assignedTrailerId = null;

                var playerM = Regex.Match(decryptedText, @"\bassigned_truck:\s*(\S+)");
                if (playerM.Success && playerM.Groups[1].Value.Trim() != "null")
                    assignedTruckId = playerM.Groups[1].Value.Trim();

                var playerMT = Regex.Match(decryptedText, @"\bassigned_trailer:\s*(\S+)");
                if (playerMT.Success && playerMT.Groups[1].Value.Trim() != "null")
                    assignedTrailerId = playerMT.Groups[1].Value.Trim();

                // 1.60+ support: check assigned_vehicles block
                if (assignedTruckId == null)
                {
                    var playerAV = Regex.Match(decryptedText, @"\bassigned_vehicles:\s*(\S+)");
                    if (playerAV.Success && playerAV.Groups[1].Value.Trim() != "null")
                    {
                        string assignedVehiclesId = playerAV.Groups[1].Value.Trim();
                        string pvb = ExtractBlock(decryptedText, "player_vehicles", assignedVehiclesId);
                        if (pvb != null)
                        {
                            var vehM = Regex.Match(pvb, @"\bvehicle:\s*(\S+)");
                            if (vehM.Success && vehM.Groups[1].Value.Trim() != "null")
                                assignedTruckId = vehM.Groups[1].Value.Trim();

                            var trlM = Regex.Match(pvb, @"\btrailer:\s*(\S+)");
                            if (trlM.Success && trlM.Groups[1].Value.Trim() != "null")
                                assignedTrailerId = trlM.Groups[1].Value.Trim();
                        }
                    }
                }

                // Fallback for Quick Jobs (agency truck/trailer)
                if (assignedTruckId == null)
                {
                    var companyM = Regex.Match(decryptedText, @"\bcompany_truck:\s*(\S+)");
                    if (companyM.Success && companyM.Groups[1].Value.Trim() != "null")
                        assignedTruckId = companyM.Groups[1].Value.Trim();
                }
                if (assignedTrailerId == null)
                {
                    var companyMT = Regex.Match(decryptedText, @"\bcompany_trailer:\s*(\S+)");
                    if (companyMT.Success && companyMT.Groups[1].Value.Trim() != "null")
                        assignedTrailerId = companyMT.Groups[1].Value.Trim();
                }

                if (assignedTruckId != null)
                {
                    // Find the vehicle block for this truck id using robust ExtractBlock
                    string tb = ExtractBlock(decryptedText, "vehicle", assignedTruckId);
                    if (tb != null)
                    {
                        SetWearDisplay(LblWearCabin,        IcoWearCabin,        TxtWearCabin,        ParseWearField(tb, "cabin_wear"));
                        SetWearDisplay(LblWearChassis,      IcoWearChassis,      TxtWearChassis,      ParseWearField(tb, "chassis_wear"));
                        SetWearDisplay(LblWearEngine,       IcoWearEngine,       TxtWearEngine,       ParseWearField(tb, "engine_wear"));
                        SetWearDisplay(LblWearTransmission, IcoWearTransmission, TxtWearTransmission, ParseWearField(tb, "transmission_wear"));
                        
                        // wheels_wear[0] is representative
                        var ww = Regex.Match(tb, @"wheels_wear\[0\]:\s*(&[0-9a-fA-F]+|[\d.]+)");
                        SetWearDisplay(LblWearWheels, IcoWearWheels, TxtWearWheels, ww.Success ? ParseFloatValue(ww.Groups[1].Value) : 0);

                        // fuel_relative is hex IEEE-754: &3f800000 = 1.0
                        var fuelM = Regex.Match(tb, @"fuel_relative:\s*(&[0-9a-fA-F]+|[\d.]+)");
                        if (fuelM.Success)
                        {
                            double fuelRel = ParseFloatValue(fuelM.Groups[1].Value);
                            int fuelPct = (int)Math.Round(fuelRel * 100);
                            TxtFuel.Text = $"{fuelPct}%";
                            Color fuelCol = fuelPct >= 50
                                ? (Color)ColorConverter.ConvertFromString("#69F0AE")
                                : fuelPct >= 20
                                    ? (Color)ColorConverter.ConvertFromString("#FFD700")
                                    : (Color)ColorConverter.ConvertFromString("#FF5252");
                            var fuelBrush = new SolidColorBrush(fuelCol);
                            TxtFuel.Foreground  = fuelBrush;
                            LblFuel.Foreground  = fuelBrush;
                            IcoFuel.Fill        = fuelBrush;
                        }
                        else
                        {
                            TxtFuel.Text = "–";
                            var dimBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444C5C"));
                            TxtFuel.Foreground = dimBrush;
                            LblFuel.Foreground = dimBrush;
                            IcoFuel.Fill = dimBrush;
                        }
                    }
                }

                if (assignedTrailerId != null)
                {
                    // Find the trailer block for this trailer id using robust ExtractBlock
                    string tb = ExtractBlock(decryptedText, "trailer", assignedTrailerId);
                    if (tb != null)
                    {
                        double bodyWear = ParseWearField(tb, "trailer_body_wear");
                        if (bodyWear <= 0)
                            bodyWear = ParseWearField(tb, "body_wear");
                        SetWearDisplay(LblWearTrailerBody,    IcoWearTrailerBody,    TxtWearTrailerBody,    bodyWear);
                        SetWearDisplay(LblWearTrailerChassis, IcoWearTrailerChassis, TxtWearTrailerChassis, ParseWearField(tb, "chassis_wear"));
                        
                        var tww = Regex.Match(tb, @"wheels_wear\[0\]:\s*(&[0-9a-fA-F]+|[\d.]+)");
                        SetWearDisplay(LblWearTrailerWheels, IcoWearTrailerWheels, TxtWearTrailerWheels, tww.Success ? ParseFloatValue(tww.Groups[1].Value) : 0);
                    }
                }

                _loadedSaveText = decryptedText;
                FleetParser.Parse(decryptedText, out _fleetTrucks, out _fleetTrailers);
                BindFleetLists(assignedTruckId, assignedTrailerId);

                // --- Parse Cities and Garages (Advanced Map UI) ---
                var garages = new List<GarageItem>();
                var cities = new List<CityItem>();
                var visitedCities = new HashSet<string>();

                // Parse visited cities from player block (visited_cities array in economy block)
                string econBlockForCities = ExtractBlock(decryptedText, "economy");
                if (econBlockForCities != null)
                {
                    var countMatch = Regex.Match(econBlockForCities, @"visited_cities:\s*(\d+)");
                    if (countMatch.Success && int.TryParse(countMatch.Groups[1].Value, out int cityCount))
                    {
                        var cityMatches = Regex.Matches(econBlockForCities, @"visited_cities\[\d+\]:\s*([a-zA-Z0-9_\-]+)");
                        foreach (Match cm in cityMatches)
                        {
                            visitedCities.Add(cm.Groups[1].Value);
                        }
                    }
                }

                // Parse all garages (brace-aware block extract)
                var garageHeaderMatches = Regex.Matches(decryptedText, @"\bgarage\s*:\s*(garage\.[a-zA-Z0-9_\-]+)\s*\{");
                foreach (Match gm in garageHeaderMatches)
                {
                    string blockName = gm.Groups[1].Value;
                    string cityName = blockName.StartsWith("garage.", StringComparison.Ordinal)
                        ? blockName.Substring("garage.".Length)
                        : blockName;
                    string body = ExtractBlock(decryptedText, "garage", blockName) ?? "";

                    int status = 0;
                    var statusMatch = Regex.Match(body, @"\bstatus:\s*(\d+)");
                    if (statusMatch.Success)
                    {
                        int.TryParse(statusMatch.Groups[1].Value, out status);
                    }

                    garages.Add(new GarageItem
                    {
                        CityName = cityName,
                        BlockName = blockName,
                        Status = status
                    });

                    cities.Add(new CityItem
                    {
                        Name = cityName,
                        IsVisited = visitedCities.Contains(cityName)
                    });
                }

                // Sort by name for user convenience
                garages.Sort((a, b) => string.Compare(a.CityName, b.CityName, StringComparison.OrdinalIgnoreCase));
                cities.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

                UnsubscribeMapDirtyHandlers();
                _allCitiesList = cities;
                _allGaragesList = garages;
                SubscribeMapDirtyHandlers();
                _mapArchiveOverride = null;

                // Reset search boxes
                if (TxtSearchCities != null) TxtSearchCities.Text = "";
                if (TxtSearchGarages != null) TxtSearchGarages.Text = "";

                // Update ListBox items sources
                if (ListCities != null)
                {
                    ListCities.ItemsSource = null;
                    ListCities.ItemsSource = _allCitiesList;
                }
                if (ListGarages != null)
                {
                    ListGarages.ItemsSource = null;
                    ListGarages.ItemsSource = _allGaragesList;
                }

                if (ChkUnlockMapCities != null) ChkUnlockMapCities.IsChecked = false;
                if (ChkUnlockMapRoads != null) ChkUnlockMapRoads.IsChecked = false;
                ClearRepairCheckboxes();

                UpdateMapListEmptyStates();

                CaptureCommittedSaveState();
                _ = DetectAndScanMapAsync();
                _ = RefreshModsCatalogAsync(rescan: false);
                SetStatus(Loc.T("status.loaded"), "#69F0AE");
            }
            catch (Exception ex)
            {
                ClearCommittedSaveState();
                SetStatus(Loc.T("status.parse.err"), "#FF5252");
                ShowAppDialog(Loc.T("dialog.error"), Loc.Tf("dialog.read.err", ex.Message), AppDialogKind.Error);
            }
            finally
            {
                _syncingSaveUi = false;
                RefreshSaveDirtyBar();
            }
        }

        private string ExtractBlock(string text, string blockClass, string blockName = null)
        {
            string pattern = blockName != null
                ? @"\b" + Regex.Escape(blockClass) + @"\s*:\s*" + Regex.Escape(blockName) + @"\s*\{"
                : @"\b" + Regex.Escape(blockClass) + @"\s*:\s*[^{\r\n]+\{";

            var match = Regex.Match(text, pattern);
            if (!match.Success) return null;

            int startIndex = match.Index + match.Length;
            int braceCount = 1;
            int currentIndex = startIndex;

            while (braceCount > 0 && currentIndex < text.Length)
            {
                char c = text[currentIndex];
                if (c == '{') braceCount++;
                else if (c == '}') braceCount--;

                if (braceCount == 0)
                {
                    return text.Substring(startIndex, currentIndex - startIndex);
                }
                currentIndex++;
            }
            return null;
        }

        private void SetAdrCheckboxes(int value)
        {
            ChkAdr0.IsChecked = (value & 1) != 0;
            ChkAdr1.IsChecked = (value & 2) != 0;
            ChkAdr2.IsChecked = (value & 4) != 0;
            ChkAdr3.IsChecked = (value & 8) != 0;
            ChkAdr4.IsChecked = (value & 16) != 0;
            ChkAdr5.IsChecked = (value & 32) != 0;
        }

        private void ClearAdrCheckboxes()
        {
            ChkAdr0.IsChecked = false;
            ChkAdr1.IsChecked = false;
            ChkAdr2.IsChecked = false;
            ChkAdr3.IsChecked = false;
            ChkAdr4.IsChecked = false;
            ChkAdr5.IsChecked = false;
        }

        private void ClearWearDisplays()
        {
            Color dimColor = (Color)ColorConverter.ConvertFromString("#444C5C");
            var dimBrush = new SolidColorBrush(dimColor);

            void ResetRow(TextBlock lbl, System.Windows.Shapes.Path ico, TextBlock val)
            {
                lbl.Foreground = dimBrush; val.Foreground = dimBrush;
                if (ico != null) ico.Fill = dimBrush;
                val.Text = "–";
            }

            ResetRow(LblWearCabin,         IcoWearCabin,         TxtWearCabin);
            ResetRow(LblWearChassis,       IcoWearChassis,       TxtWearChassis);
            ResetRow(LblWearEngine,        IcoWearEngine,        TxtWearEngine);
            ResetRow(LblWearTransmission,  IcoWearTransmission,  TxtWearTransmission);
            ResetRow(LblWearWheels,        IcoWearWheels,        TxtWearWheels);
            ResetRow(LblWearTrailerBody,   IcoWearTrailerBody,   TxtWearTrailerBody);
            ResetRow(LblWearTrailerChassis,IcoWearTrailerChassis,TxtWearTrailerChassis);
            ResetRow(LblWearTrailerWheels, IcoWearTrailerWheels, TxtWearTrailerWheels);

            TxtFuel.Text = "–";
            TxtFuel.Foreground  = dimBrush;
            LblFuel.Foreground  = dimBrush;
            IcoFuel.Fill        = dimBrush;
        }

        private int GetAdrValue()
        {
            int value = 0;
            if (ChkAdr0.IsChecked == true) value += 1;
            if (ChkAdr1.IsChecked == true) value += 2;
            if (ChkAdr2.IsChecked == true) value += 4;
            if (ChkAdr3.IsChecked == true) value += 8;
            if (ChkAdr4.IsChecked == true) value += 16;
            if (ChkAdr5.IsChecked == true) value += 32;
            return value;
        }

        /// <summary>Sets a wear TextBlock+label+icon to show a % value with color coding (green=0%, yellow=low, red=high).</summary>
        private void SetWearDisplay(TextBlock label, System.Windows.Shapes.Path icon, TextBlock value, double wearValue)
        {
            int pct = (int)Math.Round(wearValue * 100);
            value.Text = $"{pct}%";
            Color col;
            if (pct == 0)
                col = (Color)ColorConverter.ConvertFromString("#69F0AE"); // green – no wear
            else if (pct < 30)
                col = (Color)ColorConverter.ConvertFromString("#FFD700"); // yellow – moderate
            else
                col = (Color)ColorConverter.ConvertFromString("#FF5252"); // red – heavy
            var brush = new SolidColorBrush(col);
            value.Foreground  = brush;
            label.Foreground  = brush;
            if (icon != null) icon.Fill = brush;
        }

        /// <summary>Parses a wear field (hex float or plain decimal) from a block of text.</summary>
        private static double ParseWearField(string block, string fieldName)
        {
            var m = Regex.Match(block, fieldName + @":\s*(&[0-9a-fA-F]+|[\d.]+)");
            if (m.Success)
            {
                return ParseFloatValue(m.Groups[1].Value);
            }
            return 0;
        }

        /// <summary>Parses IEEE-754 hex float (e.g. &amp;3f800000 = 1.0f or &amp;H3f800000) or plain decimal.</summary>
        private static double ParseFloatValue(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return 0;
            raw = raw.Trim();
            if (raw.StartsWith("&", StringComparison.OrdinalIgnoreCase))
            {
                string hexStr = raw.Substring(1);
                if (hexStr.StartsWith("H", StringComparison.OrdinalIgnoreCase))
                {
                    hexStr = hexStr.Substring(1);
                }
                if (uint.TryParse(hexStr, System.Globalization.NumberStyles.HexNumber, null, out uint bits))
                {
                    return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
                }
            }
            if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double d))
            {
                return d;
            }
            return 0;
        }


        private void SetSkillSlider(Slider slider, Match match)
        {
            if (match.Success && int.TryParse(match.Groups[1].Value, out int val))
                slider.Value = Math.Min(6, Math.Max(0, val));
            else
                slider.Value = 0;
        }

        private void ComboGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshProfiles();
        }

        private void ComboProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedProfile = ComboProfile.SelectedItem as GameProfile;
            UpdateSteamIdLabel();
            RefreshSaves();
        }

        private void ComboSave_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedSave = ComboSave.SelectedItem as SaveGame;
            if (_selectedSave == null)
            {
                if (TxtWearCabin != null)
                    ClearWearDisplays();
                ClearFleetLists();
                _loadedSaveText = null;
                UnsubscribeMapDirtyHandlers();
                _allCitiesList = new List<CityItem>();
                _allGaragesList = new List<GarageItem>();
                if (ListCities != null) ListCities.ItemsSource = null;
                if (ListGarages != null) ListGarages.ItemsSource = null;
                _syncingSaveUi = true;
                try
                {
                    if (TxtMoney != null) TxtMoney.Text = "";
                    if (TxtXp != null) TxtXp.Text = "";
                    UpdatePlayerLevelFromXp();
                    if (ChkAdr0 != null)
                        ClearAdrCheckboxes();
                    if (ChkUnlockMapCities != null) ChkUnlockMapCities.IsChecked = false;
                    if (ChkUnlockMapRoads != null) ChkUnlockMapRoads.IsChecked = false;
                    ClearRepairCheckboxes();
                }
                finally { _syncingSaveUi = false; }
                _mapDetect = null;
                _mapArchiveOverride = null;
                _modScanCache = null;
                RefreshMapDetectSummary();
                ClearCommittedSaveState();
                UpdateMapListEmptyStates();
                UpdateSelectorsEnabled();
                return;
            }
            LoadSaveData();
            UpdateSelectorsEnabled();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshProfiles();
        }

        private void QuickMoney_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null && btn.Tag != null)
            {
                TxtMoney.Text = btn.Tag.ToString();
            }
        }

        private void QuickLevel_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag == null || TxtXp == null) return;
            if (!int.TryParse(btn.Tag.ToString(), out int level)) return;
            TxtXp.Text = XpLevel.ToXp(level).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private void BtnMaxSkills_Click(object sender, RoutedEventArgs e)
        {
            ChkAdr0.IsChecked = true;
            ChkAdr1.IsChecked = true;
            ChkAdr2.IsChecked = true;
            ChkAdr3.IsChecked = true;
            ChkAdr4.IsChecked = true;
            ChkAdr5.IsChecked = true;

            SliderSkillDist.Value     = 6;
            SliderSkillFragile.Value  = 6;
            SliderSkillUrgent.Value   = 6;
            SliderSkillEco.Value      = 6;
            SliderSkillValuable.Value = 6;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            await PerformSaveModificationAsync();
        }

        private bool _syncingRepairChecks;

        private void ChkRepairTruckAll_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncingRepairChecks) return;
            _syncingRepairChecks = true;
            try
            {
                bool on = ChkRepairTruckAll?.IsChecked == true;
                if (ChkRepairCabin != null) ChkRepairCabin.IsChecked = on;
                if (ChkRepairChassis != null) ChkRepairChassis.IsChecked = on;
                if (ChkRepairEngine != null) ChkRepairEngine.IsChecked = on;
                if (ChkRepairTransmission != null) ChkRepairTransmission.IsChecked = on;
                if (ChkRepairWheels != null) ChkRepairWheels.IsChecked = on;
                if (ChkRepairFuel != null) ChkRepairFuel.IsChecked = on;
            }
            finally { _syncingRepairChecks = false; }
            RefreshSaveDirtyBar();
        }

        private void ChkRepairTruckPart_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncingRepairChecks) return;
            SyncTruckAllFromParts();
            RefreshSaveDirtyBar();
        }

        private void SyncTruckAllFromParts()
        {
            if (ChkRepairTruckAll == null) return;
            bool allOn =
                ChkRepairCabin?.IsChecked == true &&
                ChkRepairChassis?.IsChecked == true &&
                ChkRepairEngine?.IsChecked == true &&
                ChkRepairTransmission?.IsChecked == true &&
                ChkRepairWheels?.IsChecked == true &&
                ChkRepairFuel?.IsChecked == true;

            _syncingRepairChecks = true;
            try { ChkRepairTruckAll.IsChecked = allOn; }
            finally { _syncingRepairChecks = false; }
        }

        private void ChkRepairTrailerAll_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncingRepairChecks) return;
            _syncingRepairChecks = true;
            try
            {
                bool on = ChkRepairTrailerAll?.IsChecked == true;
                if (ChkRepairTrailerBody != null) ChkRepairTrailerBody.IsChecked = on;
                if (ChkRepairTrailerChassis != null) ChkRepairTrailerChassis.IsChecked = on;
                if (ChkRepairTrailerWheels != null) ChkRepairTrailerWheels.IsChecked = on;
            }
            finally { _syncingRepairChecks = false; }
            RefreshSaveDirtyBar();
        }

        private void ChkRepairTrailerPart_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncingRepairChecks) return;
            SyncTrailerAllFromParts();
            RefreshSaveDirtyBar();
        }

        private void SyncTrailerAllFromParts()
        {
            if (ChkRepairTrailerAll == null) return;
            bool allOn =
                ChkRepairTrailerBody?.IsChecked == true &&
                ChkRepairTrailerChassis?.IsChecked == true &&
                ChkRepairTrailerWheels?.IsChecked == true;

            _syncingRepairChecks = true;
            try { ChkRepairTrailerAll.IsChecked = allOn; }
            finally { _syncingRepairChecks = false; }
        }

        private RepairOptions BuildRepairOptionsFromUi()
        {
            var opt = new RepairOptions
            {
                TruckCabin = ChkRepairCabin?.IsChecked == true,
                TruckChassis = ChkRepairChassis?.IsChecked == true,
                TruckEngine = ChkRepairEngine?.IsChecked == true,
                TruckTransmission = ChkRepairTransmission?.IsChecked == true,
                TruckWheels = ChkRepairWheels?.IsChecked == true,
                TruckFuel = ChkRepairFuel?.IsChecked == true,
                TrailerBody = ChkRepairTrailerBody?.IsChecked == true,
                TrailerChassis = ChkRepairTrailerChassis?.IsChecked == true,
                TrailerWheels = ChkRepairTrailerWheels?.IsChecked == true
            };
            if (ListTrucks?.SelectedItem is FleetUnit t) opt.TargetTruckId = t.Id;
            if (ListTrailers?.SelectedItem is FleetUnit tr) opt.TargetTrailerId = tr.Id;
            return opt;
        }

        private void TxtXp_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingXpLevel) return;
            UpdatePlayerLevelFromXp();
            RefreshSaveDirtyBar();
        }

        private void ComboPlayerLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingXpLevel || ComboPlayerLevel?.SelectedItem == null) return;
            if (ComboPlayerLevel.SelectedItem is int level)
            {
                _syncingXpLevel = true;
                try
                {
                    if (TxtXp != null)
                        TxtXp.Text = XpLevel.ToXp(level).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                finally { _syncingXpLevel = false; }
            }
        }

        private void UpdatePlayerLevelFromXp()
        {
            if (ComboPlayerLevel == null) return;

            long xp = 0;
            string raw = TxtXp?.Text?.Trim();
            if (!string.IsNullOrEmpty(raw))
            {
                if (!long.TryParse(raw, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out xp))
                    long.TryParse(raw, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.CurrentCulture, out xp);
            }

            int level = XpLevel.FromXp(xp);

            _syncingXpLevel = true;
            try
            {
                if (ComboPlayerLevel.Items.Count > 0)
                {
                    int comboLevel = Math.Min(XpLevel.MaxUsefulLevel, Math.Max(0, level));
                    if (!Equals(ComboPlayerLevel.SelectedItem, comboLevel))
                        ComboPlayerLevel.SelectedItem = comboLevel;
                }
            }
            finally { _syncingXpLevel = false; }
        }

        private void BindFleetLists(string preferredTruckId, string preferredTrailerId)
        {
            _syncingFleetSelection = true;
            try
            {
                if (ListTrucks != null)
                {
                    ListTrucks.ItemsSource = null;
                    ListTrucks.ItemsSource = _fleetTrucks;
                    SelectFleetUnit(ListTrucks, _fleetTrucks, preferredTruckId);
                }
                if (ListTrailers != null)
                {
                    ListTrailers.ItemsSource = null;
                    ListTrailers.ItemsSource = _fleetTrailers;
                    SelectFleetUnit(ListTrailers, _fleetTrailers, preferredTrailerId);
                }
            }
            finally { _syncingFleetSelection = false; }

            if (TxtTrucksCount != null)
                TxtTrucksCount.Text = (_fleetTrucks?.Count ?? 0).ToString();
            if (TxtTrailersCount != null)
                TxtTrailersCount.Text = (_fleetTrailers?.Count ?? 0).ToString();

            if (ListTrucks?.SelectedItem is FleetUnit truck)
                ShowTruckUnit(truck);
            if (ListTrailers?.SelectedItem is FleetUnit trailer)
                ShowTrailerUnit(trailer);
        }

        private static void SelectFleetUnit(ListBox list, List<FleetUnit> units, string preferredId)
        {
            if (list == null || units == null || units.Count == 0)
            {
                if (list != null) list.SelectedItem = null;
                return;
            }

            FleetUnit pick = null;
            if (!string.IsNullOrEmpty(preferredId))
                pick = units.Find(u => string.Equals(u.Id, preferredId, StringComparison.OrdinalIgnoreCase));
            if (pick == null)
                pick = units.Find(u => u.IsAssigned) ?? units[0];
            list.SelectedItem = pick;
        }

        private void ClearFleetLists()
        {
            _fleetTrucks = new List<FleetUnit>();
            _fleetTrailers = new List<FleetUnit>();
            _syncingFleetSelection = true;
            try
            {
                if (ListTrucks != null) ListTrucks.ItemsSource = null;
                if (ListTrailers != null) ListTrailers.ItemsSource = null;
            }
            finally { _syncingFleetSelection = false; }
            if (TxtTrucksCount != null) TxtTrucksCount.Text = "0";
            if (TxtTrailersCount != null) TxtTrailersCount.Text = "0";
        }

        private void ListTrucks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingFleetSelection) return;
            if (ListTrucks?.SelectedItem is FleetUnit unit)
                ShowTruckUnit(unit);
        }

        private void ListTrailers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingFleetSelection) return;
            if (ListTrailers?.SelectedItem is FleetUnit unit)
                ShowTrailerUnit(unit);
        }

        private void ShowTruckUnit(FleetUnit unit)
        {
            if (unit == null) return;
            SetWearDisplay(LblWearCabin,        IcoWearCabin,        TxtWearCabin,        unit.CabinWear);
            SetWearDisplay(LblWearChassis,      IcoWearChassis,      TxtWearChassis,      unit.ChassisWear);
            SetWearDisplay(LblWearEngine,       IcoWearEngine,       TxtWearEngine,       unit.EngineWear);
            SetWearDisplay(LblWearTransmission, IcoWearTransmission, TxtWearTransmission, unit.TransmissionWear);
            SetWearDisplay(LblWearWheels,       IcoWearWheels,       TxtWearWheels,       unit.WheelsWear);

            int fuelPct = (int)Math.Round(unit.FuelRelative * 100);
            if (TxtFuel != null)
            {
                TxtFuel.Text = $"{fuelPct}%";
                Color fuelCol = fuelPct >= 50
                    ? (Color)ColorConverter.ConvertFromString("#69F0AE")
                    : fuelPct >= 20
                        ? (Color)ColorConverter.ConvertFromString("#FFD700")
                        : (Color)ColorConverter.ConvertFromString("#FF5252");
                var fuelBrush = new SolidColorBrush(fuelCol);
                TxtFuel.Foreground = fuelBrush;
                if (LblFuel != null) LblFuel.Foreground = fuelBrush;
                if (IcoFuel != null) IcoFuel.Fill = fuelBrush;
            }
        }

        private void ShowTrailerUnit(FleetUnit unit)
        {
            if (unit == null) return;
            SetWearDisplay(LblWearTrailerBody,    IcoWearTrailerBody,    TxtWearTrailerBody,    unit.BodyWear);
            SetWearDisplay(LblWearTrailerChassis, IcoWearTrailerChassis, TxtWearTrailerChassis, unit.ChassisWear);
            SetWearDisplay(LblWearTrailerWheels,  IcoWearTrailerWheels,  TxtWearTrailerWheels,  unit.WheelsWear);
        }

        private void ClearRepairCheckboxes()
        {
            _syncingRepairChecks = true;
            try
            {
                if (ChkRepairTruckAll != null) ChkRepairTruckAll.IsChecked = false;
                if (ChkRepairCabin != null) ChkRepairCabin.IsChecked = false;
                if (ChkRepairChassis != null) ChkRepairChassis.IsChecked = false;
                if (ChkRepairEngine != null) ChkRepairEngine.IsChecked = false;
                if (ChkRepairTransmission != null) ChkRepairTransmission.IsChecked = false;
                if (ChkRepairWheels != null) ChkRepairWheels.IsChecked = false;
                if (ChkRepairFuel != null) ChkRepairFuel.IsChecked = false;
                if (ChkRepairTrailerAll != null) ChkRepairTrailerAll.IsChecked = false;
                if (ChkRepairTrailerBody != null) ChkRepairTrailerBody.IsChecked = false;
                if (ChkRepairTrailerChassis != null) ChkRepairTrailerChassis.IsChecked = false;
                if (ChkRepairTrailerWheels != null) ChkRepairTrailerWheels.IsChecked = false;
            }
            finally { _syncingRepairChecks = false; }
        }

        private async Task PerformSaveModificationAsync(RepairOptions repair = null)
        {
            if (_selectedSave == null)
            {
                ShowAppDialog(Loc.T("dialog.warning"), Loc.T("dialog.need.save"), AppDialogKind.Warning);
                return;
            }

            repair ??= BuildRepairOptionsFromUi();

            bool unlockMapCities = ChkUnlockMapCities?.IsChecked == true;
            bool unlockMapRoads = ChkUnlockMapRoads?.IsChecked == true;

            if ((unlockMapCities || unlockMapRoads) && _modScanBusy)
            {
                ShowAppDialog(Loc.T("dialog.warning"), Loc.T("map.detect.need.scan"), AppDialogKind.Warning);
                return;
            }

            if ((unlockMapCities || unlockMapRoads)
                && string.IsNullOrEmpty(_mapArchiveOverride)
                && _mapDetect?.Found != true
                && _modScanCache == null)
            {
                ShowAppDialog(Loc.T("dialog.warning"), Loc.T("map.detect.need.archive"), AppDialogKind.Warning);
                return;
            }

            try
            {
                ModMapScanResult modScan = null;
                if (unlockMapCities || unlockMapRoads)
                {
                    SetStatus(Loc.Tf("map.detect.scanning", "…"), "#FFD700");
                    modScan = await EnsureModScanAsync().ConfigureAwait(true);
                    if (modScan == null)
                    {
                        ShowAppDialog(Loc.T("dialog.error"), Loc.Tf("map.detect.failed", "scan returned no data"), AppDialogKind.Error);
                        return;
                    }
                }

                SetStatus(Loc.T("status.writing"), "#FFD700");

                string gameSii = Path.Combine(_selectedSave.Path, "game.sii");
                string decryptedText = SaveEngine.DecryptFile(gameSii, _committedDecryptorPath);

                if (string.IsNullOrEmpty(decryptedText))
                {
                    SetStatus(Loc.T("status.decrypt.err"), "#FF5252");
                    return;
                }

                if (!long.TryParse(TxtMoney.Text?.Trim(),
                        System.Globalization.NumberStyles.Integer | System.Globalization.NumberStyles.AllowThousands,
                        System.Globalization.CultureInfo.InvariantCulture, out long moneyLong)
                    && !long.TryParse(TxtMoney.Text?.Trim(),
                        System.Globalization.NumberStyles.Integer | System.Globalization.NumberStyles.AllowThousands,
                        System.Globalization.CultureInfo.CurrentCulture, out moneyLong))
                {
                    ShowAppDialog(Loc.T("dialog.error"), Loc.T("dialog.bad.money"), AppDialogKind.Error);
                    return;
                }
                decimal money = moneyLong;

                if (!int.TryParse(TxtXp.Text?.Trim(), System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int xp)
                    && !int.TryParse(TxtXp.Text?.Trim(), System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.CurrentCulture, out xp))
                {
                    ShowAppDialog(Loc.T("dialog.error"), Loc.T("dialog.bad.xp"), AppDialogKind.Error);
                    return;
                }

                var skills = new Dictionary<string, int>
                {
                    { "adr",       GetAdrValue() },
                    { "long_dist", (int)SliderSkillDist.Value },
                    { "fragile",   (int)SliderSkillFragile.Value },
                    { "urgent",    (int)SliderSkillUrgent.Value },
                    { "mechanical",(int)SliderSkillEco.Value },
                    { "heavy",     (int)SliderSkillValuable.Value }
                };

                var selectedVisitedCities = new List<string>();
                foreach (var city in _allCitiesList)
                {
                    if (city.IsVisited)
                        selectedVisitedCities.Add(city.Name);
                }

                if (unlockMapCities && modScan != null)
                {
                    foreach (var city in modScan.Cities)
                    {
                        if (!string.IsNullOrWhiteSpace(city)
                            && !selectedVisitedCities.Contains(city, StringComparer.OrdinalIgnoreCase))
                            selectedVisitedCities.Add(city);
                    }
                }

                var selectedGarages = new Dictionary<string, int>();
                foreach (var garage in _allGaragesList)
                    selectedGarages[garage.BlockName] = garage.Status;

                string workingText = decryptedText;
                var modLogParts = new List<string>();
                if (modScan != null && (unlockMapCities || unlockMapRoads))
                {
                    workingText = ExplorationUnlockWriter.Apply(
                        workingText,
                        unlockMapCities ? modScan.Cities : null,
                        unlockMapRoads ? modScan.DiscoverableUids : null,
                        markCompaniesDiscovered: unlockMapCities);

                    if (unlockMapCities)
                        modLogParts.Add(Loc.Tf("map.detect.log.cities", modScan.Cities.Count));
                    if (unlockMapRoads)
                        modLogParts.Add(Loc.Tf("map.detect.log.roads", modScan.DiscoverableUids.Count));
                }

                string modifiedText = SaveParser.ProcessSaveFile(
                    workingText,
                    money,
                    xp,
                    skills,
                    unlockCities: false,
                    buyUpgradeGarages: false,
                    repair,
                    out string log,
                    selectedVisitedCities,
                    selectedGarages
                );

                SaveEngine.WriteSaveFile(gameSii, modifiedText);

                SetStatus(Loc.T("status.saved"), "#69F0AE");

                if (unlockMapCities && ChkUnlockMapCities != null)
                    ChkUnlockMapCities.IsChecked = false;
                if (unlockMapRoads && ChkUnlockMapRoads != null)
                    ChkUnlockMapRoads.IsChecked = false;
                if (repair.Any)
                    ClearRepairCheckboxes();

                if (modLogParts.Count > 0)
                {
                    string modLine = string.Join(" · ", modLogParts);
                    log = string.IsNullOrWhiteSpace(log) ? modLine : modLine + "\n" + log.Trim();
                }

                string title = repair.Any ? Loc.T("dialog.repair.done") : Loc.T("dialog.success");
                string savedMsg = Loc.T("dialog.saved");
                string body = string.IsNullOrWhiteSpace(log)
                    ? savedMsg
                    : log.Trim() + "\n\n" + savedMsg;
                ShowAppDialog(title, body, AppDialogKind.Success);

                LoadSaveData();
            }
            catch (Exception ex)
            {
                SetStatus(Loc.T("status.write.err"), "#FF5252");
                ShowAppDialog(Loc.T("dialog.error"), Loc.Tf("dialog.write.err", ex.Message), AppDialogKind.Error);
            }
        }

        private void BtnBrowseCustomPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = Loc.T("browse.folder")
            };

            if (dialog.ShowDialog() == true)
            {
                TxtCustomPath.Text = dialog.FolderName;
                UpdateSettingsDirtyBar(HasPendingSettingsChanges());
            }
        }

        private void BtnBrowseDecryptor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = Loc.T("browse.exe.filter"),
                Title = Loc.T("browse.decrypt")
            };

            if (dialog.ShowDialog() == true)
            {
                TxtDecryptorPath.Text = dialog.FileName;
                UpdateSettingsDirtyBar(HasPendingSettingsChanges());
            }
        }
    }

    public class CityItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isVisited;
        public string Name { get; set; }

        public bool IsVisited
        {
            get => _isVisited;
            set
            {
                if (_isVisited != value)
                {
                    _isVisited = value;
                    OnPropertyChanged(nameof(IsVisited));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public class GarageItem : System.ComponentModel.INotifyPropertyChanged
    {
        private int _status;
        public string CityName { get; set; }
        public string BlockName { get; set; }

        public int Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusLevelText));
                }
            }
        }

        public string StatusText => Status switch
        {
            3 => Loc.T("garage.size.large"),
            2 => Loc.T("garage.size.medium"),
            1 => Loc.T("garage.size.small"),
            _ => Loc.T("garage.size.none")
        };

        public string StatusLevelText => Loc.Tf("garage.level", Status);

        public void NotifyLocalized()
        {
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusLevelText));
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}