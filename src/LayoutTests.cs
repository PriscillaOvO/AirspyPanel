using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AirspyPanel
{
    static class LayoutTests
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
        public static int Run()
        {
            try {
                var app = new Application();
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                int renders = 0;
                using (var panel = new PanelApp(true)) {
                    var element = (FrameworkElement)panel.Window.Content;
                    panel.AddLog("布局测试：接收已启动");
                    panel.AddLog("布局测试：暂未收到报文", "Warning"); panel.AddLog("布局测试：暂未收到报文", "Warning");
                    panel.AddLog("布局测试：模拟连接错误", "Error");
                    panel.AddRawLog("fixture raw CPU 1% / errors 0");
                    panel.Navigate("Log"); panel.RefreshLogs();
                    string important = panel.ExportLogText();
                    Check(important.Contains("×2") && !important.Contains("raw CPU"), "important events separate from raw and coalesced");
                    panel.C<ComboBox>("LogLevelChoice").SelectedIndex = 2;
                    Check(panel.ExportLogText().Contains("模拟连接错误") && !panel.ExportLogText().Contains("接收已启动"), "error filter");
                    panel.C<ComboBox>("LogView").SelectedIndex = 1;
                    Check(panel.ExportLogText().Contains("raw CPU") && !panel.ExportLogText().Contains("模拟连接错误"), "raw export selection");
                    panel.C<ComboBox>("LogView").SelectedIndex = 0; panel.C<ComboBox>("LogLevelChoice").SelectedIndex = 0;
                    foreach (string theme in new[] { "Light", "Dark" }) {
                        panel.C<ComboBox>("ThemeChoice").SelectedIndex = theme == "Light" ? 0 : 1;
                        foreach (bool compact in new[] { false, true }) {
                            panel.Navigate("Home"); panel.SetCompact(compact, false);
                            Check(panel.Window.ResizeMode == (compact ? ResizeMode.CanMinimize : ResizeMode.CanResize), "mode resize policy");
                            if (compact) Check(panel.Window.MinWidth == panel.Window.MaxWidth && panel.Window.MinHeight == panel.Window.MaxHeight, "compact fixed dimensions");
                            foreach (var size in compact ? new[] { new Size(304, 570), new Size(320, 620), new Size(304, 520) } : new[] { new Size(804, 580), new Size(1180, 780), new Size(1600, 900) }) {
                                foreach (int dpi in new[] { 96, 144, 192, 240 }) {
                                    Layout(element, size);
                                    Check(panel.C<Border>("FullHeader").Visibility == Visibility.Visible, "banner visible");
                                    var start = panel.C<Button>(compact ? "CompactStart" : "Start");
                                    Check(start.ActualHeight >= 25 && start.ActualWidth >= 65, "start button size");
                                    var toggle = panel.C<Button>("ToggleMode"); var themeChoice = panel.C<ComboBox>("ThemeChoice"); var state = panel.C<Border>("HeaderStatus");
                                    double y = toggle.TranslatePoint(new Point(0, toggle.ActualHeight / 2), element).Y;
                                    Check(Math.Abs(y - themeChoice.TranslatePoint(new Point(0, themeChoice.ActualHeight / 2), element).Y) < 1, "theme aligned");
                                    Check(Math.Abs(y - state.TranslatePoint(new Point(0, state.ActualHeight / 2), element).Y) < 1, "status aligned");
                                    if (compact) {
                                        var link = panel.C<Button>("CompactLink"); var map = panel.C<Button>("CompactMap"); var details = panel.C<Button>("CompactVrsDetails");
                                        Check(Math.Abs(link.ActualWidth - map.ActualWidth) < .1 && Math.Abs(map.ActualWidth - details.ActualWidth) < .1, "VRS equal widths");
                                        Check(link.ActualHeight == 36 && map.ActualHeight == 36 && details.ActualHeight == 36, "VRS 36 DIP heights");
                                        double linkY = link.TranslatePoint(new Point(), element).Y, mapY = map.TranslatePoint(new Point(), element).Y, detailsY = details.TranslatePoint(new Point(), element).Y;
                                        Check(Math.Abs(mapY - linkY - 42) < .1 && Math.Abs(detailsY - mapY - 42) < .1, "VRS order and spacing");
                                        if (size.Height >= 570) Check(panel.C<ScrollViewer>("CompactView").ScrollableHeight < 1, "compact home no scrolling");
                                    }
                                    if (!compact) Check(panel.C<DataGrid>("Planes").ActualHeight > 100, "table height");
                                    Save(element, size, dpi, "preview-" + theme.ToLowerInvariant() + "-" + (compact ? "compact" : "full") + "-" + size.Width + "-" + (int)size.Height + "-" + dpi + ".png");
                                    renders++;
                                }
                            }
                            panel.Navigate("Settings");
                            double navWidth = panel.Window.Width, navHeight = panel.Window.Height;
                            panel.C<Button>("SettingsLog").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Check(panel.C<StackPanel>("SettingsMenu").Visibility == Visibility.Collapsed, "log leaves settings menu");
                            panel.C<Button>("PageBack").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Check(panel.C<StackPanel>("SettingsMenu").Visibility == Visibility.Visible, "log returns to settings");
                            Check(panel.Window.Width == navWidth && panel.Window.Height == navHeight && panel.Window.ResizeMode == (compact ? ResizeMode.CanMinimize : ResizeMode.CanResize), "log navigation preserves mode and size");
                            foreach (string page in new[] { "Settings", "SettingsReceiver", "SettingsPaths", "SettingsAppearance", "Log", "Details" }) {
                                double width = panel.Window.Width, height = panel.Window.Height;
                                panel.Navigate(page);
                                Check(panel.Window.Width == width && panel.Window.Height == height, "navigation preserves window dimensions");
                                var size = compact ? new Size(304, 570) : new Size(1180, 780);
                                Layout(element, size);
                                Check(panel.C<Grid>("SharedPage").Visibility == Visibility.Visible, "internal page visible");
                                Save(element, size, 96, "preview-" + theme.ToLowerInvariant() + "-" + (compact ? "compact-" : "full-") + page.ToLowerInvariant() + ".png");
                            }
                        }
                        var result = new CloseDecision(); var dialog = panel.CreateCloseDialog(result);
                        var content = (FrameworkElement)dialog.Content; var closeSize = new Size(300, 300);
                        Layout(content, closeSize); Save(content, closeSize, 96, "preview-" + theme.ToLowerInvariant() + "-close-choice.png");
                        var stack = (StackPanel)((Border)content).Child;
                        Check(((RadioButton)stack.Children[1]).IsChecked == true && ((CheckBox)stack.Children[3]).IsChecked == false, "close dialog defaults");
                    }
                    int closeCalls = 0; var closing = new TaskCompletionSource<bool>();
                    panel.CloseVrs = () => { closeCalls++; return closing.Task; };
                    var stopping = panel.StopAll();
                    Check(!panel.C<Button>("Stop").IsEnabled && !panel.C<Button>("LinkVrs").IsEnabled, "stop all disables concurrent operations");
                    Pump(panel.StopAll()); Check(closeCalls == 1, "stop all merges repeated clicks");
                    closing.SetResult(true); Pump(stopping);
                    Check(panel.C<TextBlock>("VrsState").Text == "VRS 未连接" && panel.C<Button>("LinkVrs").IsEnabled, "stop all closes VRS and restores operations");
                    panel.Navigate("Home"); panel.SetCompact(false, false);
                    panel.C<TextBlock>("Aircraft").Text = "128";
                    panel.C<Button>("Start").IsEnabled = false; panel.C<Button>("Stop").IsEnabled = true;
                    panel.SetCompact(true, false); Layout(element, new Size(304, 570));
                    Check(!panel.C<Button>("CompactStart").IsEnabled && panel.C<Button>("CompactStop").IsEnabled && panel.C<TextBlock>("Aircraft").Text == "128", "shared receive state");
                    panel.ReadVrsConfig = port => new VrsConfiguration { Exists = true, Matches = true, ReceiverId = 7, Message = "测试配置匹配" };
                    var pending = new TaskCompletionSource<VrsStatus>(); int calls = 0;
                    panel.ProbeVrs = (url, feed) => { calls++; return pending.Task; };
                    string before = panel.C<TextBlock>("VrsState").Text;
                    Task first = panel.CheckVrs(), shared = panel.CheckVrs();
                    Check(Object.ReferenceEquals(first, shared) && calls == 1, "shared probe");
                    Check(panel.C<TextBlock>("VrsState").Text == before, "poll does not flash checking");
                    pending.SetResult(new VrsStatus { Online = true, ReceiverMatches = true, Count = 0, Message = "测试接口在线" }); Pump(first);
                    Check(panel.C<TextBlock>("VrsState").Text == "VRS 在线" && panel.C<TextBlock>("VrsCount").Text == "0", "online zero aircraft");
                    pending = new TaskCompletionSource<VrsStatus>(); var next = panel.CheckVrs();
                    Check(panel.C<TextBlock>("VrsState").Text == "VRS 在线", "retain online while polling");
                    panel.C<TextBox>("Port").Text = "30006";
                    pending.SetResult(new VrsStatus { Online = true, ReceiverMatches = true, Count = 99 }); Pump(next);
                    Check(panel.C<TextBlock>("VrsCount").Text != "99", "stale snapshot ignored");
                    panel.ProbeVrs = (url, feed) => Task.FromResult(new VrsStatus { Message = "测试服务未启动" }); Pump(panel.CheckVrs());
                    Check(panel.C<TextBlock>("VrsState").Text == "VRS 未连接", "stable disconnected state");
                    panel.C<ComboBox>("CloseChoice").SelectedIndex = 0;
                    panel.AskClose = () => new CloseDecision { Action = "Cancel", Remember = true };
                    Check(panel.ResolveClose() == "Cancel" && panel.C<ComboBox>("CloseChoice").SelectedIndex == 0, "cancel does not remember");
                    panel.AskClose = () => new CloseDecision { Action = "Tray", Remember = false };
                    Check(panel.ResolveClose() == "Tray" && panel.C<ComboBox>("CloseChoice").SelectedIndex == 0, "one time tray choice");
                    panel.AskClose = () => new CloseDecision { Action = "Tray", Remember = true };
                    Check(panel.ResolveClose() == "Tray" && panel.C<ComboBox>("CloseChoice").SelectedIndex == 1, "remember tray");
                    panel.AskClose = () => { throw new Exception("remembered action prompted"); };
                    Check(panel.ResolveClose() == "Tray", "remembered tray no prompt");
                    panel.C<ComboBox>("CloseChoice").SelectedIndex = 2;
                    Check(panel.ResolveClose() == "Exit", "settings change exit policy");
                    panel.Navigate("Log");
                    for (int i = 0; i < 80; i++) panel.AddLog("布局测试：滚动条记录 " + i);
                    panel.RefreshLogs(); Layout(element, new Size(304, 570));
                    var log = panel.C<RichTextBox>("EventLog");
                    log.ScrollToEnd(); Flush(); log.ScrollToHome(); Flush();
                    Check(panel.C<Button>("LatestLog").Visibility == Visibility.Visible, "scrolling up pauses follow");
                    panel.AddLog("布局测试：暂停时的新记录"); panel.RefreshLogs(); Layout(element, new Size(304, 570));
                    Check(log.VerticalOffset < 2, "new event preserves paused position");
                    panel.C<Button>("LatestLog").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
                    Check(panel.C<Button>("LatestLog").Visibility == Visibility.Collapsed, "resume latest");
                    panel.SetCompact(false, false); panel.Window.WindowState = WindowState.Normal;
                    panel.Window.Width = 1100; panel.Window.Height = 760;
                    panel.SetCompact(true, false); panel.SetCompact(false, false);
                    Check(panel.Window.Width == 1100 && panel.Window.Height == 760, "full dimensions restored");
                    panel.Window.WindowState = WindowState.Maximized; panel.SetCompact(true, false); panel.SetCompact(false, false);
                    Check(panel.Window.WindowState == WindowState.Maximized, "full maximized restored");
                }
                File.WriteAllText(Path.Combine(Root, "layout-test.txt"), "PASS: " + renders + " theme/mode/size/DPI renders; aligned controls; VRS equal 36 DIP controls and order; settings log roundtrip; stop-all VRS close and duplicate-operation guard; fixed compact mode and internal pages; shared receive state; non-flashing concurrent probes; stale snapshot rejection; close choices and window restoration.\n");
                return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(Root, "layout-test.txt"), "FAIL\n" + ex); return 1; }
        }
        static void Layout(FrameworkElement element, Size size)
        {
            Invalidate(element);
            element.Measure(size); element.Arrange(new Rect(size)); element.UpdateLayout();
            Flush(); element.Measure(size); element.Arrange(new Rect(size)); element.UpdateLayout();
        }
        static void Invalidate(DependencyObject node)
        {
            var ui = node as UIElement;
            if (ui != null) { ui.InvalidateMeasure(); ui.InvalidateArrange(); }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Invalidate(VisualTreeHelper.GetChild(node, i));
        }
        static void Flush()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
        static void Pump(Task task)
        {
            var until = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && DateTime.UtcNow < until) Flush();
            Check(task.IsCompleted, "UI task timeout"); task.GetAwaiter().GetResult();
        }
        static void Save(FrameworkElement element, Size size, int dpi, string name)
        {
            double scale = dpi / 96.0;
            var bitmap = new RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(Root, name))) encoder.Save(file);
        }
    }
}
