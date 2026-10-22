using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace AirspyPanel
{
    sealed partial class PanelApp
    {
        CancellationTokenSource scanCancel;
        string currentPage = "Home";
        string effectiveTheme;
        bool presentationReady, updatingReceivers;
        void SetupPresentation()
        {
            C<Grid>("RootLayout").SizeChanged += (s, e) => UpdateResponsive(e.NewSize.Width);
            C<Button>("OpenSettings").Click += (s, e) => ShowSettings();
            C<Button>("TopSettings").Click += (s, e) => ShowSettings();
            C<Button>("TopVrsDetails").Click += (s, e) => Navigate("Details");
            foreach (string name in new[] { "SettingsReceiver", "SettingsPaths", "SettingsAppearance" }) {
                string page = name;
                C<Button>(name).Click += (s, e) => Navigate(page);
            }
            C<Button>("QuickMap").Click += async (s, e) => await OpenMap();
            C<Button>("VrsDetails").Click += (s, e) => Navigate("Details");
            C<Button>("SettingsLog").Click += (s, e) => Navigate("Log");
            C<Button>("PageBack").Click += (s, e) => Navigate((currentPage == "Log" || (currentPage.StartsWith("Settings") && currentPage != "Settings")) ? "Settings" : "Home");
            C<Button>("CopyDetails").Click += (s, e) => { try { Clipboard.SetText(C<TextBlock>("Diagnostics").Text); } catch (Exception ex) { Error(ex); } };

            C<ComboBox>("ReceiverChoice").SelectionChanged += (s, e) => {
                if (updatingReceivers) return;
                var selected = C<ComboBox>("ReceiverChoice").SelectedItem as VrsReceiver;
                if (selected != null) { settings.VrsReceiverId = selected.Id; settingsRevision++; lastProbe = DateTime.MinValue; }
            };
            foreach (var field in new[] { "MapUrl", "VrsPath", "DecoderPath" })
                C<TextBox>(field).TextChanged += (s, e) => { settingsRevision++; PreviewCommand(); lastProbe = DateTime.MinValue; };
            foreach (var key in new[] { "s176", "s177", "s223" }) C<ComboBox>("ThemeChoice").Items.Add(new LocalizedChoice { Key = key });
            C<ComboBox>("ThemeChoice").SelectedIndex = settings.Theme == "Dark" ? 1 : settings.Theme == "System" ? 2 : 0;
            effectiveTheme = ThemeManager.Effective(settings.Theme);
            C<ComboBox>("ThemeChoice").SelectionChanged += (s, e) => {
                settings.Theme = new[] { "Light", "Dark", "System" }[Math.Max(0, C<ComboBox>("ThemeChoice").SelectedIndex)];
                ThemeManager.Apply(Window, settings.Theme); effectiveTheme = ThemeManager.Effective(settings.Theme);
                logDirty = true; RefreshLogs();
                try { Save(); } catch (Exception ex) { Error(ex); }
            };
            SystemEvents.UserPreferenceChanged += SystemThemeChanged;
            C<Button>("ToggleMode").Click += (s, e) => SetCompact(!settings.CompactMode);
            C<Button>("CompactSettings").Click += (s, e) => {
                ShowSettings();
            };
            C<Button>("CompactStart").Click += (s, e) => Start();
            C<Button>("CompactStop").Click += (s, e) => Stop();
            C<Button>("CompactMap").Click += async (s, e) => await OpenMap();
            C<Button>("CompactVrsDetails").Click += (s, e) => Navigate("Details");
            C<Button>("CompactLink").Click += async (s, e) => await LinkVrs();
            C<Button>("Recommended").Click += (s, e) => {
                if (active) return;
                C<TextBox>("Port").Text = "30005"; C<TextBox>("Filter").Text = "4";
                C<ComboBox>("Gain").SelectedItem = "auto"; C<ComboBox>("Sample").SelectedValue = "auto";
                C<CheckBox>("Packing").IsChecked = false; PreviewCommand();
                Text("SettingsHint", L.Source("s141"));
            };
            C<Button>("Help").Click += (s, e) => {
                C<Expander>("HelpSection").Visibility = C<Expander>("HelpSection").Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                C<Expander>("HelpSection").IsExpanded = true;
                C<Expander>("HelpSection").BringIntoView();
            };
            C<Button>("Scan").Click += async (s, e) => await ScanPaths(null);
            C<Button>("CancelScan").Click += (s, e) => { if (scanCancel != null) scanCancel.Cancel(); };
            C<Button>("ScanFolder").Click += async (s, e) => {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = L.Get("s236"), ShowNewFolderButton = false }) {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) await ScanPaths(dialog.SelectedPath);
                }
            };
            C<ComboBox>("DecoderCandidates").SelectionChanged += (s, e) => {
                if (C<ComboBox>("DecoderCandidates").SelectedItem != null && !active)
                    C<TextBox>("DecoderPath").Text = Convert.ToString(C<ComboBox>("DecoderCandidates").SelectedItem);
            };
            C<ComboBox>("VrsCandidates").SelectionChanged += (s, e) => {
                if (C<ComboBox>("VrsCandidates").SelectedItem != null)
                    C<TextBox>("VrsPath").Text = Convert.ToString(C<ComboBox>("VrsCandidates").SelectedItem);
            };
            foreach (var key in new[] { "s175", "s142", "s233" }) C<ComboBox>("CloseChoice").Items.Add(new LocalizedChoice { Key = key });
            C<ComboBox>("CloseChoice").SelectedIndex = settings.CloseAction == "Tray" ? 1 : settings.CloseAction == "Exit" ? 2 : 0;
            C<ComboBox>("CloseChoice").SelectionChanged += (s, e) => {
                settings.CloseAction = new[] { "Ask", "Tray", "Exit" }[Math.Max(0, C<ComboBox>("CloseChoice").SelectedIndex)];
                try { Save(); } catch (Exception ex) { Error(ex); }
            };
            SetupLogs();
            SetCompact(settings.CompactMode, false);
            presentationReady = true;
        }
        void SystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (closed) return;
            Window.Dispatcher.BeginInvoke(new Action(() => {
                if (!closed && settings.Theme == "System" && effectiveTheme != ThemeManager.Effective("System")) {
                    ThemeManager.Apply(Window, "System"); effectiveTheme = ThemeManager.Effective("System");
                    logDirty = true; RefreshLogs();
                }
            }));
        }
        public void SetCompact(bool compact, bool persist = true)
        {
            if (presentationReady) RememberWindow();
            settings.CompactMode = compact;
            Window.WindowState = WindowState.Normal;
            var area = CurrentWorkArea();
            Window.MaxWidth = Double.PositiveInfinity; Window.MaxHeight = Double.PositiveInfinity;
            Window.MinWidth = Math.Min(compact ? 320 : 820, area.Width);
            Window.MinHeight = Math.Min(620, area.Height);
            Window.Width = Math.Min(compact ? 320 : SafeDimension(settings.FullWidth, 1180, 820), area.Width);
            Window.Height = Math.Min(compact ? 620 : SafeDimension(settings.FullHeight, 820, 620), area.Height);
            Window.ResizeMode = compact ? ResizeMode.CanMinimize : ResizeMode.CanResize;
            if (compact) { Window.MinWidth = Window.MaxWidth = Window.Width; Window.MinHeight = Window.MaxHeight = Window.Height; }
            Window.Left = Math.Max(area.Left, Math.Min(Double.IsNaN(Window.Left) ? area.Left : Window.Left, area.Right - Window.Width));
            Window.Top = Math.Max(area.Top, Math.Min(Double.IsNaN(Window.Top) ? area.Top : Window.Top, area.Bottom - Window.Height));
            Navigate(currentPage);
            C<Border>("FullHeader").Visibility = Visibility.Visible;
            C<TextBlock>("Footer").Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            C<Button>("ToggleMode").Content = L.Get(compact ? "s101" : "s191");
            UpdateResponsive(Window.Width - 28);
            if (!compact && settings.FullMaximized) Window.WindowState = WindowState.Maximized;
            if (persist) try { Save(); } catch (Exception ex) { Error(ex); }
        }
        internal void RememberWindow()
        {
            if (!settings.CompactMode && Window.WindowState != WindowState.Minimized) settings.FullMaximized = Window.WindowState == WindowState.Maximized;
            if (Window.WindowState != WindowState.Normal) return;
            if (settings.CompactMode) { settings.CompactWidth = Window.Width; settings.CompactHeight = Window.Height; }
            else { settings.FullWidth = Window.Width; settings.FullHeight = Window.Height; }
        }
        Rect CurrentWorkArea()
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(Window).Handle;
            if (handle == IntPtr.Zero) return SystemParameters.WorkArea;
            var area = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
            var source = PresentationSource.FromVisual(Window);
            var transform = source == null || source.CompositionTarget == null ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
            return new Rect(transform.Transform(new Point(area.Left, area.Top)), transform.Transform(new Point(area.Right, area.Bottom)));
        }
        internal void UpdateResponsive(double width)
        {
            bool compact = settings.CompactMode;
            C<ColumnDefinition>("ThemeColumn").Width = new GridLength(compact ? 88 : 104);
            bool narrow = width < 1000;
            C<ColumnDefinition>("SidebarColumn").Width = new GridLength(narrow ? 0 : Math.Max(220, Math.Min(270, width * .23)));
            C<ColumnDefinition>("SidebarGap").Width = new GridLength(narrow ? 0 : 12);
            C<Border>("MapSidebar").Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
            C<TextBlock>("Tagline").Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            C<TextBlock>("FrequencyCaption").Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            C<TextBlock>("BrandTitle").FontSize = compact ? 23 : 28;
            var tools = C<Grid>("HeaderTools");
            Grid.SetRow(tools, compact ? 1 : 0); Grid.SetColumn(tools, compact ? 0 : 2); Grid.SetColumnSpan(tools, compact ? 3 : 1);
            tools.Width = compact ? Double.NaN : 326;
            tools.HorizontalAlignment = HorizontalAlignment.Stretch;
            tools.Margin = compact ? new Thickness(0, 8, 0, 0) : new Thickness(0);
            C<Button>("ToggleMode").FontSize = compact ? 11 : 12;
            C<ComboBox>("ThemeChoice").FontSize = compact ? 11 : 12;
            C<StackPanel>("BrandText").HorizontalAlignment = HorizontalAlignment.Left;
            bool shortWindow = C<Grid>("RootLayout").ActualHeight < 650;
            C<TextBlock>("DataState").Visibility = !compact && shortWindow ? Visibility.Collapsed : Visibility.Visible;
            C<TextBlock>("Footer").Visibility = compact || shortWindow ? Visibility.Collapsed : Visibility.Visible;
            var planes = C<DataGrid>("Planes");
            double available = narrow ? width - 32 : width - C<ColumnDefinition>("SidebarColumn").Width.Value - 44;
            planes.Columns[4].Visibility = available < 630 ? Visibility.Collapsed : Visibility.Visible;
            planes.Columns[5].Visibility = available < 720 ? Visibility.Collapsed : Visibility.Visible;
        }
        void ShowSettings() { Navigate("Settings"); }
        internal void Navigate(string page)
        {
            currentPage = page;
            bool home = page == "Home";
            C<Grid>("FullView").Visibility = home && !settings.CompactMode ? Visibility.Visible : Visibility.Collapsed;
            C<ScrollViewer>("CompactView").Visibility = home && settings.CompactMode ? Visibility.Visible : Visibility.Collapsed;
            C<Grid>("SharedPage").Visibility = home ? Visibility.Collapsed : Visibility.Visible;
            C<ScrollViewer>("SettingsContent").Visibility = page.StartsWith("Settings") ? Visibility.Visible : Visibility.Collapsed;
            C<StackPanel>("SettingsMenu").Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
            C<Expander>("AdvancedSection").Visibility = page == "SettingsReceiver" ? Visibility.Visible : Visibility.Collapsed;
            C<Expander>("PathsSection").Visibility = page == "SettingsPaths" ? Visibility.Visible : Visibility.Collapsed;
            C<Expander>("AppearanceSection").Visibility = page == "SettingsAppearance" ? Visibility.Visible : Visibility.Collapsed;
            C<Button>("Help").Visibility = page == "SettingsReceiver" ? Visibility.Visible : Visibility.Collapsed;
            if (page != "SettingsReceiver") C<Expander>("HelpSection").Visibility = Visibility.Collapsed;
            if (page == "SettingsReceiver") C<Expander>("AdvancedSection").IsExpanded = true;
            if (page == "SettingsPaths") C<Expander>("PathsSection").IsExpanded = true;
            if (page == "SettingsAppearance") C<Expander>("AppearanceSection").IsExpanded = true;
            C<ScrollViewer>("DetailsContent").Visibility = page == "Details" ? Visibility.Visible : Visibility.Collapsed;
            C<Grid>("LogContent").Visibility = page == "Log" ? Visibility.Visible : Visibility.Collapsed;
            Text("PageTitle", page.StartsWith("Settings") ? page == "SettingsReceiver" ? L.Source("s129") : page == "SettingsPaths" ? L.Source("s181") : page == "SettingsAppearance" ? L.Source("s095") : L.Source("s212") : page == "Log" ? L.Source("s229") : L.Source("s041"));
            if (page == "Log") { logDirty = true; RefreshLogs(); }
        }
        void UpdateReceiverChoices(VrsConfiguration config)
        {
            var choice = C<ComboBox>("ReceiverChoice");
            if (choice.IsDropDownOpen) return;
            updatingReceivers = true;
            try {
                choice.Items.Clear(); choice.Items.Add(new VrsReceiver { Id = 0, Name = L.Source("s193") });
                foreach (var item in config.Candidates) choice.Items.Add(item);
                if (settings.VrsReceiverId > 0 && !config.Candidates.Any(x => x.Id == settings.VrsReceiverId))
                    choice.Items.Add(new VrsReceiver { Id = settings.VrsReceiverId, Name = L.Source("s103") });
                choice.SelectedItem = choice.Items.Cast<VrsReceiver>().FirstOrDefault(x => x.Id == settings.VrsReceiverId);
            } finally { updatingReceivers = false; }
        }
        static double SafeDimension(double value, double fallback, double minimum)
        {
            return Double.IsNaN(value) || Double.IsInfinity(value) ? fallback : Math.Max(minimum, value);
        }
        void SetVrsState(string state, string text)
        {
            if (displayedText.ContainsKey("VrsState") && displayedText["VrsState"] == text) return;
            Text("VrsState", text);
            if (state != "Checking") AddLog(text, state == "Error" ? "Error" : state == "Mismatch" ? "Warning" : "Info");
            C<System.Windows.Shapes.Ellipse>("VrsLamp").Fill = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(state == "Online" ? "#66B83C" :
                state == "Checking" ? "#4D9FE0" : state == "Mismatch" ? "#DBAA38" : state == "Error" ? "#D46363" : "#8598A4"));
        }
        string BuildDiagnostics(Settings snapshot, VrsConfiguration config, VrsStatus status)
        {
            var data = monitor.Get();
            return L.Source("s172") + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                L.Source("s000") + (File.Exists(Path.Combine(snapshot.DecoderFolder, "airspy_adsb.exe")) ? L.Source("s107") : L.Source("s160")) +
                L.Source("s003") + (decoder.Running ? L.Source("s228") + decoder.Id + "）" : L.Source("s156")) +
                L.Source("s004") + (data.Connected ? data.HasSignal ? L.Source("s112") : L.Source("s111") :
                    Commands.PortInUse(snapshot.Port) ? L.Source("s187") : L.Source("s186")) +
                "\n   127.0.0.1:" + snapshot.Port + L.Source("s005") + config.Message +
                L.Source("s002") + Vrs.ConfigPath + L.Source("s006") + status.Message + "\n   " + (status.RequestUrl ?? Vrs.ApiUri(snapshot.MapUrl, config.ReceiverId).AbsoluteUri) +
                L.Source("s007") + (!status.Online ? L.Source("s125") : status.ReceiverMatches ? L.Source("s069") + config.ReceiverId + "）" :
                    config.ReceiverId == 0 ? L.Source("s161") + status.Feed + L.Source("s255") :
                    L.Source("s054") + config.ReceiverId + L.Source("s253") + status.Feed + L.Source("s256")) +
                L.Source("s001");
        }
        async Task ScanPaths(string selectedFolder)
        {
            if (scanCancel != null || active) return;
            scanCancel = new CancellationTokenSource();
            var token = scanCancel.Token;
            C<Button>("Scan").IsEnabled = false; C<Button>("ScanFolder").IsEnabled = false;
            C<Button>("CancelScan").IsEnabled = true; C<Button>("Start").IsEnabled = false;
            C<Button>("SavePaths").IsEnabled = false;
            Text("ScanStatus", L.Source("s174"));
            C<ComboBox>("DecoderCandidates").ItemsSource = null;
            C<ComboBox>("VrsCandidates").ItemsSource = null;
            try {
                string decoderPath = C<TextBox>("DecoderPath").Text.Trim(), vrsPath = C<TextBox>("VrsPath").Text.Trim();
                var seed = new Settings { DecoderFolder = Path.GetDirectoryName(decoderPath), VrsPath = vrsPath };
                var result = await Task.Run(() => Discovery.Scan(
                    selectedFolder == null ? Discovery.CommonRoots(seed) : new List<string> { selectedFolder },
                    selectedFolder == null ? 4 : 8, selectedFolder == null ? 12000 : 30000,
                    TimeSpan.FromSeconds(selectedFolder == null ? 15 : 60), token,
                    n => Window.Dispatcher.BeginInvoke(new Action(() => { if (!closed) Text("ScanStatus", L.Source("s109") + n + L.Source("s015")); })),
                    Path.Combine(Vrs.Folder, "InstallerConfiguration.xml")), token);
                if (closed) return;
                C<TextBox>("DecoderPath").Text = Discovery.ChooseExistingOrUnique(decoderPath, result.Decoders);
                C<TextBox>("VrsPath").Text = Discovery.ChooseExistingOrUnique(vrsPath, result.VrsPrograms);
                C<ComboBox>("DecoderCandidates").ItemsSource = result.Decoders;
                C<ComboBox>("VrsCandidates").ItemsSource = result.VrsPrograms;
                C<ComboBox>("DecoderCandidates").Visibility = result.Decoders.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                C<ComboBox>("VrsCandidates").Visibility = result.VrsPrograms.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                string currentMap = C<TextBox>("MapUrl").Text.Trim();
                if (String.IsNullOrWhiteSpace(currentMap) || currentMap == "http://127.0.0.1/VirtualRadar/")
                    C<TextBox>("MapUrl").Text = result.MapUrl;
                Text("ScanStatus", L.Source("s182"));
                if (token.IsCancellationRequested) throw new OperationCanceledException();
                var web = await Vrs.Probe(C<TextBox>("MapUrl").Text.Trim());
                token.ThrowIfCancellationRequested();
                if (closed) return;
                Text("ScanStatus", L.Source("s198") + result.Decoders.Count + L.Source("s013") + result.VrsPrograms.Count + L.Source("s012") +
                    (web.Online ? L.Source("s090") : L.Source("s088")) +
                    L.Source("s010") + result.Visited + L.Source("s016") + result.Skipped + L.Source("s014") +
                    (result.Limited ? L.Source("s009") : "") +
                    L.Source("s008"));
            } catch (OperationCanceledException) { if (!closed) Text("ScanStatus", L.Source("s169")); }
              catch (Exception ex) { if (!closed) Text("ScanStatus", L.Source("s168") + ex.Message); }
            finally {
                scanCancel.Dispose(); scanCancel = null;
                if (!closed) { C<Button>("Scan").IsEnabled = !active; C<Button>("ScanFolder").IsEnabled = !active;
                    C<Button>("CancelScan").IsEnabled = false; C<Button>("Start").IsEnabled = !active && !busy; C<Button>("SavePaths").IsEnabled = true; }
            }
        }
    }
}
