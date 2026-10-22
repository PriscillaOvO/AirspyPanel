using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace AirspyPanel
{
    sealed class CloseDecision
    {
        public string Action = "Cancel";
        public bool Remember;
    }
    static class InstanceSignal
    {
        public const string Name = @"Local\AirspyPanel.Activate";
        public static void Wake(string name = Name)
        {
            for (int i = 0; i < 10; i++) {
                try { using (var signal = EventWaitHandle.OpenExisting(name)) signal.Set(); return; }
                catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(100); }
            }
            MessageBox.Show(L.Get("s247"), "AirspyPanel");
        }
    }
    sealed partial class PanelApp
    {
        Forms.NotifyIcon tray;
        System.Drawing.Icon trayIcon;
        Forms.ToolStripMenuItem trayStart, trayStop;
        EventWaitHandle activation;
        RegisteredWaitHandle activationWait;
        bool exiting;
        internal Func<CloseDecision> AskClose;
        void SetupLifecycle()
        {
            AskClose = ShowCloseChoice;
            Window.Closing += (s, e) => {
                if (exiting) { Dispose(); return; }
                string action = ResolveClose();
                if (action == "Exit") { e.Cancel = true; RequestExit(); return; }
                e.Cancel = true;
                if (action == "Tray") HideToTray();
            };
            if (preview) return;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AirspyPanel.Airplane.ico"))
                trayIcon = new System.Drawing.Icon(stream);
            tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "AirspyPanel", Visible = true };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add(L.Get("s150"), null, (s, e) => OnUi(RestoreFromTray));
            trayStart = new Forms.ToolStripMenuItem(L.Get("s114"), null, (s, e) => OnUi(Start));
            trayStop = new Forms.ToolStripMenuItem(L.Get("s062"), null, (s, e) => OnUi(Stop)) { ToolTipText = L.Get("s061") };
            menu.Items.Add(trayStart); menu.Items.Add(trayStop);
            menu.Items.Add(L.Get("s118"), null, (s, e) => OnUi(async () => await OpenMap()));
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(L.Get("s233"), null, (s, e) => OnUi(RequestExit));
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => OnUi(RestoreFromTray);
            activation = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceSignal.Name);
            activationWait = ThreadPool.RegisterWaitForSingleObject(activation, (s, timeout) => OnUi(RestoreFromTray), null, Timeout.Infinite, false);
            UpdateTray();
        }
        void OnUi(Action action)
        {
            if (!closed && !Window.Dispatcher.HasShutdownStarted)
                Window.Dispatcher.BeginInvoke(new Action(() => { if (!closed) action(); }));
        }
        internal string ResolveClose()
        {
            if (settings.CloseAction == "Tray" || settings.CloseAction == "Exit") return settings.CloseAction;
            var decision = AskClose();
            if (decision == null || (decision.Action != "Tray" && decision.Action != "Exit")) return "Cancel";
            if (decision.Remember) {
                settings.CloseAction = decision.Action;
                C<ComboBox>("CloseChoice").SelectedIndex = decision.Action == "Tray" ? 1 : 2;
            }
            return decision.Action;
        }
        void HideToTray()
        {
            if (tray == null && !preview) { AddLog(L.Source("s120"), "Warning"); return; }
            try { RememberWindow(); Save(); Window.ShowInTaskbar = false; Window.Hide(); }
            catch (Exception ex) { AddLog(L.Source("s144") + ex.Message, "Error"); }
        }
        void RestoreFromTray()
        {
            if (closed) return;
            Window.ShowInTaskbar = true; Window.Show();
            if (Window.WindowState == WindowState.Minimized)
                Window.WindowState = !settings.CompactMode && settings.FullMaximized ? WindowState.Maximized : WindowState.Normal;
            Window.Activate();
        }
        void UpdateTray()
        {
            if (tray == null) return;
            string text = "AirspyPanel · " + C<TextBlock>("RunState").Text + " · " + C<TextBlock>("VrsState").Text;
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            trayStart.Enabled = C<Button>("Start").IsEnabled;
            trayStop.Enabled = C<Button>("Stop").IsEnabled;
        }
        bool exitPending;
        async void RequestExit()
        {
            if (exiting || exitPending || busy) return;
            exitPending = true;
            try {
                while (!await StopAll(false)) {
                    if (closed) return;
                    RestoreFromTray();
                    var answer = MessageBox.Show(Window, L.Get("ExitFailure"), "AirspyPanel", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                    if (answer == MessageBoxResult.Cancel) return;
                    if (answer == MessageBoxResult.No) break;
                }
                exiting = true; Window.Close();
            } finally { exitPending = false; }
        }
        void RefreshTrayLanguage()
        {
            if (tray == null) return;
            string[] keys = { "s150", "s114", "s062", "s118", null, "s233" };
            for (int i = 0; i < keys.Length; i++) if (keys[i] != null) tray.ContextMenuStrip.Items[i].Text = L.Get(keys[i]);
            trayStop.ToolTipText = L.Get("s061"); UpdateTray();
        }
        internal void SystemExit() {
            exiting = true;
            // Do not delay Windows shutdown or show a dialog. Request graceful VRS exit.
            try { using (var list = new ProcessList("VirtualRadar")) foreach (var p in list.Items) if (!p.HasExited) p.CloseMainWindow(); } catch { }
            Dispose();
        }
        void DisposeTray()
        {
            if (activationWait != null) { activationWait.Unregister(null); activationWait = null; }
            if (activation != null) { activation.Dispose(); activation = null; }
            if (tray != null) { tray.Visible = false; if (tray.ContextMenuStrip != null) tray.ContextMenuStrip.Dispose(); tray.Dispose(); tray = null; }
            if (trayIcon != null) { trayIcon.Dispose(); trayIcon = null; }
        }
        CloseDecision ShowCloseChoice()
        {
            var result = new CloseDecision();
            CreateCloseDialog(result).ShowDialog(); return result;
        }
        internal Window CreateCloseDialog(CloseDecision result)
        {
            var dialog = new Window { Title = L.Get("s066"), Owner = Window.IsVisible ? Window : null, Width = 310, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false, FontFamily = Window.FontFamily, FontSize = 12, Resources = Window.Resources };
            dialog.SetResourceReference(Window.BackgroundProperty, "BrushEAF4FA");
            dialog.SetResourceReference(Window.ForegroundProperty, "Brush284A62");
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = L.Get("s067"), FontSize = 16, Margin = new Thickness(0, 0, 0, 12) });
            var toTray = new RadioButton { Content = new TextBlock { Text = L.Get("s145"), TextWrapping = TextWrapping.Wrap, MaxWidth = 245 }, IsChecked = true, Margin = new Thickness(0, 8, 0, 8) };
            var quit = new RadioButton { Content = new TextBlock { Text = L.Get("s234"), TextWrapping = TextWrapping.Wrap, MaxWidth = 245 }, Margin = new Thickness(0, 8, 0, 8) };
            toTray.SetResourceReference(Control.ForegroundProperty, "Brush284A62"); quit.SetResourceReference(Control.ForegroundProperty, "Brush284A62");
            stack.Children.Add(toTray); stack.Children.Add(quit);
            var remember = new CheckBox { Content = new TextBlock { Text = L.Get("s210"), TextWrapping = TextWrapping.Wrap, MaxWidth = 245 }, IsChecked = false, Margin = new Thickness(0, 12, 0, 8) };
            stack.Children.Add(remember);
            stack.Children.Add(new TextBlock { Text = L.Get("s077"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = L.Get("s075"), IsCancel = true, MinWidth = 72, Margin = new Thickness(0, 0, 8, 0) };
            var ok = new Button { Content = L.Get("s180"), IsDefault = true, MinWidth = 72 };
            ok.Click += (s, e) => { result.Action = toTray.IsChecked == true ? "Tray" : "Exit"; result.Remember = remember.IsChecked == true; dialog.DialogResult = true; };
            buttons.Children.Add(cancel); buttons.Children.Add(ok); stack.Children.Add(buttons);
            var surface = new Border { Padding = new Thickness(16), Child = stack };
            surface.SetResourceReference(Border.BackgroundProperty, "BrushEAF4FA"); dialog.Content = surface;
            dialog.Icon = Window.Icon;
            return dialog;
        }
    }
}
