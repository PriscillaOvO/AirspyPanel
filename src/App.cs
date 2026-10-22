using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace AirspyPanel
{
    sealed partial class PanelApp : IDisposable
    {
        public readonly Window Window;
        readonly string root = AppDomain.CurrentDomain.BaseDirectory;
        readonly IReceiverBackend decoder;
        readonly IMapService mapService;
        readonly BeastMonitor monitor = new BeastMonitor();
        readonly DispatcherTimer timer = new DispatcherTimer();
        Settings settings;
        bool active, closed, busy, initializing;
        Task probeTask;
        internal Func<Task> CloseVrs;
        internal Func<string, int, Task<VrsStatus>> ProbeVrs;
        internal Func<int, VrsConfiguration> ReadVrsConfig;
        int settingsRevision;
        DateTime started, cpuAt, lastProbe = DateTime.MinValue;
        TimeSpan lastCpu;
        string configStatus = "", lastVrsMessage = L.Source("s032");
        int vrsCount;
        bool vrsOnline;
        readonly bool preview;
        public PanelApp(bool previewOnly) : this(previewOnly, new Decoder(), new VrsService()) { }
        internal PanelApp(bool previewOnly, IReceiverBackend receiver, IMapService maps)
        {
            decoder = receiver; mapService = maps; CloseVrs = maps.Close; ProbeVrs = maps.Probe;
            preview = previewOnly;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AirspyPanel.Panel.xaml"))
                Window = (Window)XamlReader.Load(stream);
            settings = Load();
            if (preview) settings.Language = "zh-CN";
            L.Apply(Window, settings.Language);
            ReadVrsConfig = port => mapService.ReadConfiguration(port, settings.VrsReceiverId);
            using (var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("AirspyPanel.Airplane.xaml")) {
                var drawing = (DrawingImage)XamlReader.Load(icon);
                Window.Resources["AirplaneIcon"] = drawing; Window.Icon = drawing;
            }
            ThemeManager.Apply(Window, settings.Theme);
            initializing = true;
            var gain = C<ComboBox>("Gain"); gain.Items.Add("auto");
            for (int i = 0; i <= 21; i++) gain.Items.Add(i.ToString());
            C<ComboBox>("Sample").SelectedValuePath = "Value";
            foreach (var x in new[] { "auto", "12", "20", "24" }) C<ComboBox>("Sample").Items.Add(new LocalizedChoice { Value = x, Key = x == "auto" ? "s192" : null });
            C<TextBox>("Port").Text = settings.Port.ToString(); C<TextBox>("Filter").Text = settings.Filter.ToString();
            gain.SelectedItem = settings.Gain; C<ComboBox>("Sample").SelectedValue = settings.Sample;
            C<CheckBox>("Packing").IsChecked = settings.Packing;
            C<TextBox>("DecoderPath").Text = Path.Combine(settings.DecoderFolder, "airspy_adsb.exe");
            C<TextBox>("VrsPath").Text = settings.VrsPath; C<TextBox>("MapUrl").Text = settings.MapUrl;
            initializing = false;
            var work = SystemParameters.WorkArea;
            Window.Width = Math.Min(1180, work.Width - 28); Window.Height = Math.Min(820, work.Height - 28);
            Window.MinWidth = Math.Min(820, work.Width - 28); Window.MinHeight = Math.Min(620, work.Height - 28);
            SetupPresentation();
            C<Button>("Start").Click += (s, e) => Start();
            C<Button>("Stop").Click += (s, e) => Stop();
            C<Button>("Save").Click += (s, e) => { try { Collect(); Save(); AddLog(L.Source("s240")); } catch (Exception ex) { Error(ex); } };
            C<Button>("SavePaths").Click += (s, e) => { try { Collect(); Save(); AddLog(L.Source("s224")); lastProbe = DateTime.MinValue; } catch (Exception ex) { Error(ex); } };
            C<Button>("BrowseDecoder").Click += (s, e) => Browse("DecoderPath", "airspy_adsb.exe");
            C<Button>("BrowseVrs").Click += (s, e) => Browse("VrsPath", "VirtualRadar.exe");
            C<Button>("LinkVrs").Click += async (s, e) => await LinkVrs();
            C<Button>("ClearLog").Click += (s, e) => ClearLogs();
            C<Button>("ExportLog").Click += (s, e) => Export();
            C<TextBox>("Port").TextChanged += (s, e) => PreviewCommand();
            C<TextBox>("Filter").TextChanged += (s, e) => PreviewCommand();
            C<ComboBox>("Gain").SelectionChanged += (s, e) => PreviewCommand();
            C<ComboBox>("Sample").SelectionChanged += (s, e) => PreviewCommand();
            C<CheckBox>("Packing").Click += (s, e) => PreviewCommand();
            decoder.Line += AddRawLog;
            decoder.Ended += (code, requested) => {
                if (!closed) Window.Dispatcher.BeginInvoke(new Action(() => {
                    active = false; ControlsRunning(false); monitor.Stop();
                    Text("RunState", requested ? L.Source("s104") : code == 0 ? L.Source("s110") : L.Source("s133"));
                    AddLog(requested ? L.Source("s105") : L.Source("s204") + code + L.Source("s051"), !requested && code != 0 ? "Error" : "Info");
                    if (!requested && code != 0) Text("DataState", L.Source("s201"));
                }));
            };
            SetupLifecycle();
            SetupLanguage();
            Window.Loaded += (s, e) => {
                if (preview) return;
                AddLog(L.Source("s140"));
                AddLog(L.Source("s213"));
                timer.Start();
            };
            timer.Interval = TimeSpan.FromMilliseconds(700);
            timer.Tick += async (s, e) => {
                Refresh();
                if ((DateTime.UtcNow - lastProbe).TotalSeconds > 4 && (probeTask == null || probeTask.IsCompleted)) await CheckVrs();
            };
            PreviewCommand(); Refresh();
        }
        public T C<T>(string name) where T : class { return Window.FindName(name) as T; }
        internal void Text(string name, string value) { displayedText[name] = value; C<TextBlock>(name).Text = L.Text(value); }
        Settings Load()
        {
            var s = new Settings {
                DecoderFolder = root,
                VrsPath = InstallationPaths.FindVrs()
            };
            try {
                if (preview) return s;
                string path = SettingsStore.FilePath;
                string legacy = Path.Combine(root, "AirspyPanel.settings.xml");
                if (!File.Exists(path) && File.Exists(legacy)) SettingsStore.Write(path, SettingsStore.Read(legacy));
                if (File.Exists(path)) s = SettingsStore.Read(path);
                s.Format = "Beast";
                if (String.IsNullOrWhiteSpace(s.DecoderFolder)) s.DecoderFolder = root;
                if (String.IsNullOrWhiteSpace(s.MapUrl)) s.MapUrl = "http://127.0.0.1/VirtualRadar/";
                Commands.Build(s);
            } catch (Exception ex) { AddLog(L.Source("s219") + ex.Message, "Error"); }
            return s;
        }
        void Collect()
        {
            int port, filter;
            if (!Int32.TryParse(C<TextBox>("Port").Text, out port)) throw new ArgumentException(L.Source("s214"));
            if (!Int32.TryParse(C<TextBox>("Filter").Text, out filter)) throw new ArgumentException(L.Source("s215"));
            var candidate = new Settings {
                Port = port, Filter = filter, Format = "Beast", Gain = Convert.ToString(C<ComboBox>("Gain").SelectedItem),
                Sample = Convert.ToString(C<ComboBox>("Sample").SelectedValue), Packing = C<CheckBox>("Packing").IsChecked == true,
                VrsPath = C<TextBox>("VrsPath").Text.Trim(), MapUrl = C<TextBox>("MapUrl").Text.Trim(),
                DecoderFolder = InstallationPaths.DecoderFolder(C<TextBox>("DecoderPath").Text),
                Language = settings.Language, Theme = settings.Theme, CompactMode = settings.CompactMode,
                FullWidth = settings.FullWidth, FullHeight = settings.FullHeight,
                CompactWidth = settings.CompactWidth, CompactHeight = settings.CompactHeight,
                FullMaximized = settings.FullMaximized, VrsReceiverId = settings.VrsReceiverId, CloseAction = settings.CloseAction
            };
            Commands.Build(candidate); Vrs.ApiUri(candidate.MapUrl);
            if (candidate.MapUrl != settings.MapUrl || candidate.Port != settings.Port ||
                candidate.DecoderFolder != settings.DecoderFolder || candidate.VrsPath != settings.VrsPath) settingsRevision++;
            settings = candidate;
        }
        void PreviewCommand()
        {
            if (initializing) return;
            try { Collect(); C<TextBox>("Command").Text = "airspy_adsb.exe " + Commands.Build(settings); }
            catch (Exception ex) { C<TextBox>("Command").Text = L.Text(ex.Message); }
        }
        void Save()
        {
            if (!preview) SettingsStore.Write(SettingsStore.FilePath, settings);
        }
        void ControlsRunning(bool running)
        {
            C<Button>("Start").IsEnabled = !running && !busy; C<Button>("Stop").IsEnabled = CanStopAll();
            C<System.Windows.Shapes.Ellipse>("ReceptionLamp").SetResourceReference(System.Windows.Shapes.Shape.FillProperty, running ? "Brush6ACB39" : "Brush8598A4");
            C<Grid>("InputGrid").IsEnabled = !running;
            C<TextBox>("DecoderPath").IsEnabled = !running; C<Button>("BrowseDecoder").IsEnabled = !running;
            C<Button>("Recommended").IsEnabled = !running;
            C<Button>("Scan").IsEnabled = !running; C<Button>("ScanFolder").IsEnabled = !running;
            C<Button>("Start").IsEnabled = !running && !busy && scanCancel == null;
        }
        void Start()
        {
            try {
                if (active || busy) return;
                Collect();
                string exe = Path.Combine(settings.DecoderFolder, "airspy_adsb.exe");
                if (!File.Exists(exe)) throw new FileNotFoundException(L.Source("s158"));
                using (var processes = new ProcessList("airspy_adsb")) {
                    if (processes.Items.Length > 0) throw new InvalidOperationException(L.Source("s108"));
                }
                if (Commands.PortInUse(settings.Port)) throw new InvalidOperationException(L.Source("s184") + settings.Port + L.Source("s017"));
                if (settings.Sample == "24" && MessageBox.Show(Window, L.Get("s020"), L.Get("s211"), MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                Save(); string command = Commands.Build(settings);
                started = cpuAt = DateTime.UtcNow; lastCpu = TimeSpan.Zero;
                decoder.Start(exe, command); active = true; ControlsRunning(true);
                monitor.Start(settings.Port);
                Text("RunState", L.Source("s173")); AddLog(L.Source("s136")); AddRawLog(L.Source("s080") + command);
                AddLog(L.Source("s239") + settings.Port + "。");
            } catch (Exception ex) { Error(ex); }
        }
        async void Stop() { await StopAll(); }
        internal async Task<bool> StopAll(bool reportError = true)
        {
            if (busy) return false;
            busy = true; settingsRevision++;
            C<Button>("Stop").IsEnabled = false; C<Button>("Start").IsEnabled = false; C<Button>("LinkVrs").IsEnabled = false;
            try {
                decoder.Stop();
                if (active) Text("RunState", L.Source("s060"));
                await CloseVrs();
                if (closed) return false;
                vrsOnline = false; Text("VrsCount", L.Source("s164"));
                SetVrsState("Error", L.Source("s038"));
                AddLog(L.Source("s063"));
                return true;
            } catch (Exception ex) { if (!closed) { if (reportError) Error(ex); else AddLog(ex.Message, "Error"); } return false; }
            finally {
                busy = false; settingsRevision++; lastProbe = DateTime.MinValue;
                if (!closed) { ControlsRunning(active); C<Button>("LinkVrs").IsEnabled = true; }
            }
        }
        bool CanStopAll()
        {
            if (busy) return false;
            if (active) return true;
            if (preview) return false;
            return mapService.IsRunning;
        }
        static string Compact(long n) { return n < 100000 ? n.ToString("N0") : (n / 10000.0).ToString("0.0") + L.Source("s053"); }
        void Refresh()
        {
            var s = monitor.Get();
            Text("Aircraft", s.Connected ? s.Planes.Count.ToString() : "—");
            Text("Rate", s.Connected ? s.Rate.ToString("0.0") : "—"); Text("Total", Compact(s.Total));
            Text("Mean", s.Connected && s.HasSignal ? s.Mean.ToString("0.0") + "%" : L.Source("s151"));
            Text("Peak", s.Connected && s.HasSignal ? s.Peak.ToString("0.0") + "%" : L.Source("s151"));
            C<ProgressBar>("MeanBar").Value = s.Connected && s.HasSignal ? s.Mean : 0;
            C<ProgressBar>("PeakBar").Value = s.Connected && s.HasSignal ? s.Peak : 0;
            C<DataGrid>("Planes").ItemsSource = s.Planes;
            C<TextBlock>("Empty").Visibility = s.Planes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Text("Empty", !s.Connected ? L.Source("s152") : L.Source("s146"));
            Text("DataState", s.Connected ? (s.HasSignal ? L.Source("s049") : L.Source("s050")) : s.State);
            string cpu = "—", uptime = "00:00:00";
            if (active) {
                var now = DateTime.UtcNow; var used = decoder.Cpu;
                double pct = (used - lastCpu).TotalMilliseconds / Math.Max(1, (now - cpuAt).TotalMilliseconds) / Environment.ProcessorCount * 100;
                cpu = Math.Max(0, Math.Min(100, pct)).ToString("0.0") + "%";
                lastCpu = used; cpuAt = now; var t = now - started;
                uptime = ((int)t.TotalHours).ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
            }
            Text("Footer", L.Source("s227") + uptime + "  ·  CPU " + cpu + L.Source("s011") + settings.Port + " / Beast");
            if (lastBeastConnection != s.Connected) { lastBeastConnection = s.Connected; AddLog(s.Connected ? L.Source("s026") : L.Source("s027"), s.Connected ? "Info" : "Warning"); }
            if (!preview) C<Button>("Stop").IsEnabled = CanStopAll();
            RefreshLogs(); UpdateTray();
        }
        internal Task CheckVrs()
        {
            if (closed) return Task.FromResult(0);
            if (probeTask != null && !probeTask.IsCompleted) return probeTask;
            probeTask = CheckVrsCore();
            return probeTask;
        }
        async Task CheckVrsCore()
        {
            lastProbe = DateTime.UtcNow;
            var snapshot = settings;
            int revision = settingsRevision;
            try {
                var config = ReadVrsConfig(snapshot.Port);
                var status = await ProbeVrs(snapshot.MapUrl, config.ReceiverId);
                if (closed || busy || revision != settingsRevision) { lastProbe = DateTime.MinValue; return; }
                configStatus = config.Message;
                UpdateReceiverChoices(config);
                vrsOnline = status.Online && status.ReceiverMatches && config.Matches;
                vrsCount = status.Count; lastVrsMessage = status.Message;
                Text("VrsCount", status.Online ? vrsCount.ToString() : L.Source("s164"));
                Text("VrsHint", configStatus + "\n" + status.Message);
                SetVrsState(vrsOnline ? "Online" : status.Online ? "Mismatch" : "Error",
                    vrsOnline ? L.Source("s030") : status.Online ? config.ReceiverId > 0 && !status.ReceiverMatches ? L.Source("s084") : L.Source("s085") : L.Source("s038"));
                Text("Diagnostics", BuildDiagnostics(snapshot, config, status));
                Text("CheckResult", DateTime.Now.ToString("HH:mm:ss") + "  " + (vrsOnline ? L.Source("s171") : status.Message));
            } catch (Exception ex) {
                if (!closed && !busy && revision == settingsRevision) { SetVrsState("Error", L.Source("s038")); Text("CheckResult", DateTime.Now.ToString("HH:mm:ss") + "  " + ex.Message); }
            }
        }
        async Task LinkVrs()
        {
            if (busy) return;
            busy = true; C<Button>("LinkVrs").IsEnabled = false; C<Button>("Stop").IsEnabled = false;
            try {
                Collect();
                if (!File.Exists(settings.VrsPath)) throw new FileNotFoundException(L.Source("s157"));
                await CloseVrs();
                if (closed) return;
                string backup = mapService.Configure(settings.Port);
                settings.VrsReceiverId = (int)System.Xml.Linq.XDocument.Load(Vrs.ConfigPath).Root.Element("GoogleMapSettings").Element("WebSiteReceiverId");
                settingsRevision++;
                AddLog(L.Source("s034") + settings.Port + L.Source("s254"));
                if (backup.Length > 0) AddLog(L.Source("s071") + backup);
                Save();
                mapService.Start(settings.VrsPath);
                SetVrsState("Checking", L.Source("s029")); AddLog(L.Source("s039"));
                lastProbe = DateTime.MinValue;
                await Task.Delay(1400);
                if (probeTask != null && !probeTask.IsCompleted) await probeTask;
                await CheckVrs();
            } catch (Exception ex) { Error(ex); }
            finally { busy = false; lastProbe = DateTime.MinValue; if (!closed) { C<Button>("LinkVrs").IsEnabled = true; ControlsRunning(active); } }
        }
        Task OpenMap()
        {
            try {
                var url = new Uri(C<TextBox>("MapUrl").Text.Trim());
                if (url.Scheme != "http" && url.Scheme != "https") throw new ArgumentException(L.Source("s089"));
                Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
                AddLog(L.Source("s106") + url.AbsoluteUri);
            } catch (Exception ex) { Error(ex); }
            return Task.FromResult(0);
        }
        void Browse(string control, string file)
        {
            var picker = new OpenFileDialog { Filter = file + "|" + file };
            if (picker.ShowDialog(Window) == true) C<TextBox>(control).Text = picker.FileName;
        }
        void Export()
        {
            var dialog = new SaveFileDialog { Filter = L.Get("s149"), FileName = "Airspy-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt" };
            if (dialog.ShowDialog(Window) == true) try { File.WriteAllText(dialog.FileName, ExportLogText(), new UTF8Encoding(true)); } catch (Exception ex) { Error(ex); }
        }
        void Error(Exception ex) { AddLog(ex.Message, "Error"); MessageBox.Show(Window, L.Text(ex.Message), L.Get("s022"), MessageBoxButton.OK, MessageBoxImage.Information); }
        public void Dispose()
        {
            if (closed) return;
            if (!preview) {
                RememberWindow();
                try { Save(); } catch (Exception) { }
            }
            closed = true; timer.Stop(); DisposeTray();
            if (scanCancel != null) scanCancel.Cancel();
            SystemEvents.UserPreferenceChanged -= SystemThemeChanged;
            monitor.Dispose(); decoder.Dispose();
        }
    }
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--test-worker") {
                Console.Out.Write("first\rsecond\n"); Console.Out.Flush();
                Console.Error.WriteLine("stderr-message"); Console.Error.Flush(); Thread.Sleep(100);
                Console.Out.Write("partial-final"); Console.Out.Flush(); return 7;
            }
            if (args.Length > 0 && args[0] == "--test-wait") { Thread.Sleep(30000); return 0; }
            if (args.Length > 0 && args[0] == "--self-test") return Tests.Run();
            if (args.Length > 0 && args[0] == "--render") return LayoutTests.Run();
            if (args.Length > 0 && args[0] == "--render-languages") return InternationalTests.Run();
            bool created;
            using (var mutex = new Mutex(true, @"Local\AirspyPanel", out created))
            {
                if (!created) { InstanceSignal.Wake(); return 0; }
                try {
                    var app = new Application();
                    using (var panel = new PanelApp(false)) { app.SessionEnding += (s, e) => panel.SystemExit(); app.Run(panel.Window); }
                } catch (Exception ex) {
                    Directory.CreateDirectory(SettingsStore.Folder);
                    File.WriteAllText(Path.Combine(SettingsStore.Folder, "AirspyPanel-error.txt"), ex.ToString());
                    MessageBox.Show(L.Get("s139") + L.Text(ex.Message));
                    return 1;
                }
            }
            return 0;
        }
    }
}
