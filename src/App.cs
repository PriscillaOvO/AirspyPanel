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
    sealed class PanelApp : IDisposable
    {
        public readonly Window Window;
        readonly string root = AppDomain.CurrentDomain.BaseDirectory;
        readonly Decoder decoder = new Decoder();
        readonly BeastMonitor monitor = new BeastMonitor();
        readonly DispatcherTimer timer = new DispatcherTimer();
        readonly Queue<string> pending = new Queue<string>();
        Settings settings;
        bool active, closed, busy, probeRunning, initializing;
        DateTime started, cpuAt, lastProbe = DateTime.MinValue;
        TimeSpan lastCpu;
        string configStatus = "", lastVrsMessage = "VRS 尚未检查";
        int vrsCount;
        bool vrsOnline;
        readonly bool preview;
        public PanelApp(bool previewOnly)
        {
            preview = previewOnly;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AirspyPanel.Panel.xaml"))
                Window = (Window)XamlReader.Load(stream);
            settings = Load();
            initializing = true;
            var gain = C<ComboBox>("Gain"); gain.Items.Add("auto");
            for (int i = 0; i <= 21; i++) gain.Items.Add(i.ToString());
            foreach (var x in new[] { "自动", "12", "20", "24" }) C<ComboBox>("Sample").Items.Add(x);
            C<TextBox>("Port").Text = settings.Port.ToString(); C<TextBox>("Filter").Text = settings.Filter.ToString();
            gain.SelectedItem = settings.Gain; C<ComboBox>("Sample").SelectedItem = settings.Sample;
            C<CheckBox>("Packing").IsChecked = settings.Packing;
            C<TextBox>("DecoderPath").Text = Path.Combine(settings.DecoderFolder, "airspy_adsb.exe");
            C<TextBox>("VrsPath").Text = settings.VrsPath; C<TextBox>("MapUrl").Text = settings.MapUrl;
            initializing = false;
            var work = SystemParameters.WorkArea;
            Window.Width = Math.Min(1180, work.Width - 28); Window.Height = Math.Min(820, work.Height - 28);
            Window.MinWidth = Math.Min(820, work.Width - 28); Window.MinHeight = Math.Min(620, work.Height - 28);
            Window.SizeChanged += (s, e) => {
                C<ColumnDefinition>("SidebarColumn").Width = new GridLength(Window.ActualWidth < 980 ? 240 : 270);
            };
            C<Button>("Start").Click += (s, e) => Start();
            C<Button>("Stop").Click += (s, e) => Stop();
            C<Button>("Save").Click += (s, e) => { try { Collect(); Save(); AddLog("配置已保存。"); } catch (Exception ex) { Error(ex); } };
            C<Button>("SavePaths").Click += (s, e) => { try { Collect(); Save(); AddLog("路径与地图地址已保存。"); lastProbe = DateTime.MinValue; } catch (Exception ex) { Error(ex); } };
            C<Button>("BrowseDecoder").Click += (s, e) => Browse("DecoderPath", "airspy_adsb.exe");
            C<Button>("BrowseVrs").Click += (s, e) => Browse("VrsPath", "VirtualRadar.exe");
            C<Button>("LinkVrs").Click += async (s, e) => await LinkVrs();
            C<Button>("Map").Click += async (s, e) => await OpenMap();
            C<Button>("Diagnose").Click += async (s, e) => { try { Collect(); await CheckVrs(); } catch (Exception ex) { Error(ex); } };
            C<Button>("ClearLog").Click += (s, e) => C<TextBox>("Log").Clear();
            C<Button>("ExportLog").Click += (s, e) => Export();
            C<TextBox>("Port").TextChanged += (s, e) => PreviewCommand();
            C<TextBox>("Filter").TextChanged += (s, e) => PreviewCommand();
            C<ComboBox>("Gain").SelectionChanged += (s, e) => PreviewCommand();
            C<ComboBox>("Sample").SelectionChanged += (s, e) => PreviewCommand();
            C<CheckBox>("Packing").Click += (s, e) => PreviewCommand();
            decoder.Line += AddLog;
            decoder.Ended += (code, requested) => {
                if (!closed) Window.Dispatcher.BeginInvoke(new Action(() => {
                    active = false; ControlsRunning(false); monitor.Stop();
                    Text("RunState", requested ? "已停止" : code == 0 ? "已结束" : "接收失败");
                    AddLog(requested ? "已停止接收并释放设备。" : "解码器退出，返回码 " + code + "。检查设备、USB、驱动或占用。");
                    if (!requested && code != 0) Text("DataState", "解码器启动失败，请查看运行日志。");
                }));
            };
            Window.Closing += (s, e) => {
                if (active && MessageBox.Show(Window, "关闭控制台会停止当前接收，确定关闭？", "退出控制台",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { e.Cancel = true; return; }
                Dispose();
            };
            Window.Loaded += (s, e) => {
                if (preview) return;
                AddLog("控制台 2.0 已就绪。飞机数量与双电平表直接来自真实 Beast 报文。");
                AddLog("请确认接入 Airspy R0 / R2 / Mini 与 1090 MHz 天线。");
                timer.Start();
            };
            timer.Interval = TimeSpan.FromMilliseconds(700);
            timer.Tick += async (s, e) => {
                Refresh();
                if ((DateTime.UtcNow - lastProbe).TotalSeconds > 4 && !probeRunning) await CheckVrs();
            };
            PreviewCommand(); Refresh();
        }
        public T C<T>(string name) where T : class { return Window.FindName(name) as T; }
        void Text(string name, string value) { C<TextBlock>(name).Text = value; }
        void AddLog(string value)
        {
            lock (pending) {
                if (pending.Count > 2000) pending.Dequeue();
                pending.Enqueue(DateTime.Now.ToString("HH:mm:ss") + "  " + value);
            }
        }
        Settings Load()
        {
            var s = new Settings {
                DecoderFolder = root,
                VrsPath = InstallationPaths.FindVrs()
            };
            try {
                string path = Path.Combine(root, "AirspyPanel.settings.xml");
                if (File.Exists(path)) using (var file = File.OpenRead(path)) s = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(file);
                s.Format = "Beast";
                if (String.IsNullOrWhiteSpace(s.DecoderFolder)) s.DecoderFolder = root;
                if (String.IsNullOrWhiteSpace(s.MapUrl)) s.MapUrl = "http://127.0.0.1/VirtualRadar/";
                Commands.Build(s);
            } catch (Exception ex) { AddLog("读取配置失败：" + ex.Message); }
            return s;
        }
        void Collect()
        {
            int port, filter;
            if (!Int32.TryParse(C<TextBox>("Port").Text, out port)) throw new ArgumentException("请输入有效的数据端口。");
            if (!Int32.TryParse(C<TextBox>("Filter").Text, out filter)) throw new ArgumentException("请输入有效的过滤强度。");
            var candidate = new Settings {
                Port = port, Filter = filter, Format = "Beast", Gain = Convert.ToString(C<ComboBox>("Gain").SelectedItem),
                Sample = Convert.ToString(C<ComboBox>("Sample").SelectedItem), Packing = C<CheckBox>("Packing").IsChecked == true,
                VrsPath = C<TextBox>("VrsPath").Text.Trim(), MapUrl = C<TextBox>("MapUrl").Text.Trim(),
                DecoderFolder = InstallationPaths.DecoderFolder(C<TextBox>("DecoderPath").Text)
            };
            Commands.Build(candidate); Vrs.ApiUri(candidate.MapUrl);
            settings = candidate;
        }
        void PreviewCommand()
        {
            if (initializing) return;
            try { Collect(); C<TextBox>("Command").Text = "airspy_adsb.exe " + Commands.Build(settings); }
            catch (Exception ex) { C<TextBox>("Command").Text = ex.Message; }
        }
        void Save()
        {
            string path = Path.Combine(root, "AirspyPanel.settings.xml");
            using (var stream = File.Create(path + ".tmp")) new XmlSerializer(typeof(Settings)).Serialize(stream, settings);
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null); else File.Move(path + ".tmp", path);
        }
        void ControlsRunning(bool running)
        {
            C<Button>("Start").IsEnabled = !running; C<Button>("Stop").IsEnabled = running;
            C<Grid>("InputGrid").IsEnabled = !running;
            C<TextBox>("DecoderPath").IsEnabled = !running; C<Button>("BrowseDecoder").IsEnabled = !running;
        }
        void Start()
        {
            try {
                if (active) return;
                Collect();
                string exe = Path.Combine(settings.DecoderFolder, "airspy_adsb.exe");
                if (!File.Exists(exe)) throw new FileNotFoundException("未找到 airspy_adsb.exe，请在左侧设置程序路径。");
                using (var processes = new ProcessList("airspy_adsb")) {
                    if (processes.Items.Length > 0) throw new InvalidOperationException("已有 ADS-B SPY 在运行。请先关闭原解码窗口，避免同一接收机被重复占用。");
                }
                if (Commands.PortInUse(settings.Port)) throw new InvalidOperationException("端口 " + settings.Port + " 已被占用，请修改端口后重新联动 VRS。");
                if (settings.Sample == "24" && MessageBox.Show(Window, "24 MHz 仅适用于支持该模式的 Airspy R2，确认继续？", "设备模式", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                Save(); string command = Commands.Build(settings);
                started = cpuAt = DateTime.UtcNow; lastCpu = TimeSpan.Zero;
                decoder.Start(exe, command); active = true; ControlsRunning(true);
                monitor.Start(settings.Port);
                Text("RunState", "正在接收"); AddLog("启动：" + command);
                AddLog("遥测客户端正在连接本机 Beast 端口 " + settings.Port + "。");
            } catch (Exception ex) { Error(ex); }
        }
        void Stop()
        {
            try { C<Button>("Stop").IsEnabled = false; decoder.Stop(); Text("RunState", "停止中"); }
            catch (Exception ex) { C<Button>("Stop").IsEnabled = true; Error(ex); }
        }
        static string Compact(long n) { return n < 100000 ? n.ToString("N0") : (n / 10000.0).ToString("0.0") + "万"; }
        void Refresh()
        {
            var s = monitor.Get();
            Text("Aircraft", s.Connected ? s.Planes.Count.ToString() : "—");
            Text("Rate", s.Connected ? s.Rate.ToString("0.0") : "—"); Text("Total", Compact(s.Total));
            Text("Mean", s.Connected && s.HasSignal ? s.Mean.ToString("0.0") + "%" : "暂无数据");
            Text("Peak", s.Connected && s.HasSignal ? s.Peak.ToString("0.0") + "%" : "暂无数据");
            C<ProgressBar>("MeanBar").Value = s.Connected && s.HasSignal ? s.Mean : 0;
            C<ProgressBar>("PeakBar").Value = s.Connected && s.HasSignal ? s.Peak : 0;
            C<DataGrid>("Planes").ItemsSource = s.Planes;
            C<TextBlock>("Empty").Visibility = s.Planes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Text("Empty", !s.Connected ? "暂无飞机\n接入接收机并点击“开始接收”" : "数据连接正常，尚未识别到飞机\n请检查天线与接收条件");
            Text("DataState", s.Connected ? (s.HasSignal ? "● Beast 已连接 · 正在收到报文" : "● Beast 已连接 · 等待报文") : s.State);
            string cpu = "—", uptime = "00:00:00";
            if (active) {
                var now = DateTime.UtcNow; var used = decoder.Cpu;
                double pct = (used - lastCpu).TotalMilliseconds / Math.Max(1, (now - cpuAt).TotalMilliseconds) / Environment.ProcessorCount * 100;
                cpu = Math.Max(0, Math.Min(100, pct)).ToString("0.0") + "%";
                lastCpu = used; cpuAt = now; var t = now - started;
                uptime = ((int)t.TotalHours).ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
            }
            Text("Footer", "运行 " + uptime + "  ·  CPU " + cpu + "  ·  本机数据 127.0.0.1:" + settings.Port + " / Beast");
            Text("Diagnostics", "解码器：" + (active ? "面板已启动，进程 " + decoder.Id : "未由面板启动") +
                "\n数据流：" + s.State + "\n\n" + configStatus + "\n\n网页：" + lastVrsMessage +
                "\n地址：" + settings.MapUrl + "\n\n若连接正常但无报文，请检查天线、USB 和接收范围。");
            var batch = new StringBuilder();
            lock (pending) { for (int i = 0; i < 200 && pending.Count > 0; i++) batch.AppendLine(pending.Dequeue()); }
            if (batch.Length > 0) {
                var log = C<TextBox>("Log"); if (log.Text.Length > 140000) log.Text = log.Text.Substring(log.Text.Length - 90000);
                log.AppendText(batch.ToString()); log.ScrollToEnd();
            }
        }
        async Task CheckVrs()
        {
            if (probeRunning || closed) return;
            probeRunning = true; lastProbe = DateTime.UtcNow;
            try {
                configStatus = Vrs.CheckConfig(settings.Port);
                var status = await Vrs.Probe(settings.MapUrl);
                if (closed) return;
                vrsOnline = status.Online && status.ReceiverMatches; vrsCount = status.Count; lastVrsMessage = status.Message;
                Text("VrsCount", vrsOnline ? vrsCount.ToString() : status.Online ? "来源不符" : "未连接");
                Text("VrsHint", configStatus + "\n" + status.Message);
            } finally { probeRunning = false; }
        }
        async Task LinkVrs()
        {
            if (busy) return;
            busy = true; C<Button>("LinkVrs").IsEnabled = false;
            try {
                Collect();
                if (!File.Exists(settings.VrsPath)) throw new FileNotFoundException("未找到 VirtualRadar.exe，请在左侧设置 VRS 程序路径。");
                using (var processes = new ProcessList("VirtualRadar")) {
                    if (processes.Items.Length > 0) {
                        if (MessageBox.Show(Window, "同步配置需要正常关闭并重新启动 VRS。\n当前飞机列表会重新建立，配置会先备份。继续？", "联动 VRS", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                        foreach (var p in processes.Items) {
                            if (!p.CloseMainWindow()) throw new InvalidOperationException("VRS 无法自动关闭。请先退出 VRS，再点击联动。");
                            bool exited = await Task.Run(() => p.WaitForExit(6000));
                            if (!exited) throw new InvalidOperationException("VRS 尚未退出，请完成其窗口操作后再试。");
                        }
                    }
                }
                string backup = Vrs.Prepare(settings.Port, Vrs.ConfigPath);
                AddLog("VRS 已配置为 Beast / 127.0.0.1:" + settings.Port + "，接收机 Airspy Panel。");
                if (backup.Length > 0) AddLog("原配置备份：" + backup);
                Save();
                Process.Start(new ProcessStartInfo(settings.VrsPath) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(settings.VrsPath) });
                AddLog("VRS 正在启动。网页状态会自动更新。");
                lastProbe = DateTime.MinValue;
                await Task.Delay(1400); await CheckVrs();
            } catch (Exception ex) { Error(ex); }
            finally { busy = false; if (!closed) C<Button>("LinkVrs").IsEnabled = true; }
        }
        async Task OpenMap()
        {
            try {
                Collect(); configStatus = Vrs.CheckConfig(settings.Port);
                var status = await Vrs.Probe(settings.MapUrl);
                if (!status.Online || !status.ReceiverMatches) { AddLog("VRS 网页尚未可用或来源不符，请先点击“一键联动 VRS”，再检查地址。"); Text("VrsHint", status.Message); return; }
                var url = new Uri(settings.MapUrl);
                if (url.Scheme != "http" && url.Scheme != "https") throw new ArgumentException("地图地址无效。");
                Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
                AddLog("已打开经过检查的地图地址：" + url.AbsoluteUri);
            } catch (Exception ex) { Error(ex); }
        }
        void Browse(string control, string file)
        {
            var picker = new OpenFileDialog { Filter = file + "|" + file };
            if (picker.ShowDialog(Window) == true) C<TextBox>(control).Text = picker.FileName;
        }
        void Export()
        {
            var dialog = new SaveFileDialog { Filter = "文本日志|*.txt", FileName = "Airspy-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt" };
            if (dialog.ShowDialog(Window) == true) try { File.WriteAllText(dialog.FileName, C<TextBox>("Log").Text, new UTF8Encoding(true)); } catch (Exception ex) { Error(ex); }
        }
        void Error(Exception ex) { AddLog(ex.Message); MessageBox.Show(Window, ex.Message, "Airspy 控制台", MessageBoxButton.OK, MessageBoxImage.Information); }
        public void Dispose()
        {
            if (closed) return; closed = true; timer.Stop(); monitor.Dispose(); decoder.Dispose();
        }
        sealed class ProcessList : IDisposable
        {
            public readonly Process[] Items;
            public ProcessList(string name) { Items = Process.GetProcessesByName(name); }
            public void Dispose() { foreach (var p in Items) p.Dispose(); }
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
            if (args.Length > 0 && args[0] == "--render") return Tests.Render();
            bool created;
            using (var mutex = new Mutex(true, @"Local\AirspyPanel", out created))
            {
                if (!created) { MessageBox.Show("控制台已打开，请从任务栏切换。"); return 0; }
                try {
                    var app = new Application();
                    using (var panel = new PanelApp(false)) app.Run(panel.Window);
                } catch (Exception ex) {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AirspyPanel-error.txt"), ex.ToString());
                    MessageBox.Show("控制台启动异常：" + ex.Message);
                    return 1;
                }
            }
            return 0;
        }
    }
}
