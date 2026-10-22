using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AirspyPanel
{
    sealed class LogEntry
    {
        public DateTime Time;
        public string Level, Message;
        public LocalizedMessage Localized;
        public int Repeats = 1;
        public string RenderMessage() { return (Localized ?? L.Capture(Message)).Render(); }
        public override string ToString() { return Time.ToString("HH:mm:ss") + "  [" + L.Level(Level) + "] " + RenderMessage() + (Repeats > 1 ? " ×" + Repeats : ""); }
    }
    sealed class EventBuffer
    {
        public readonly List<LogEntry> Entries = new List<LogEntry>();
        public void Add(LogEntry item)
        {
            var last = Entries.LastOrDefault();
            if (last != null && last.Level == item.Level && last.Message == item.Message) { last.Repeats++; last.Time = item.Time; }
            else { Entries.Add(item); if (Entries.Count > 500) Entries.RemoveAt(0); }
        }
    }
    sealed partial class PanelApp
    {
        readonly Queue<LogEntry> pendingEvents = new Queue<LogEntry>();
        readonly Queue<string> pendingRaw = new Queue<string>();
        readonly Queue<string> rawLines = new Queue<string>();
        readonly EventBuffer events = new EventBuffer();
        bool lastBeastConnection, rebuildingLogs, followEvents = true, followRaw = true, logDirty = true;
        internal void AddLog(string text, string level = "Info")
        {
            lock (pendingEvents) {
                if (pendingEvents.Count >= 1000) pendingEvents.Dequeue();
                pendingEvents.Enqueue(new LogEntry { Time = DateTime.Now, Message = text, Localized = L.Capture(text), Level = level });
            }
        }
        internal void AddRawLog(string text)
        {
            text = L.Text(text);
            if (text.Length > 4096) text = text.Substring(0, 4096) + "…";
            lock (pendingRaw) {
                if (pendingRaw.Count >= 1000) pendingRaw.Dequeue();
                pendingRaw.Enqueue(DateTime.Now.ToString("HH:mm:ss") + "  " + text);
            }
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\s*(error|fatal)\b|failed to|cannot open", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                AddLog(L.Source("s205") + text, "Error");
            else if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\s*warning\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) AddLog(L.Source("s205") + text, "Warning");
        }
        void SetupLogs()
        {
            foreach (string key in new[] { "s065", "s070" }) C<ComboBox>("LogView").Items.Add(new LocalizedChoice { Key = key });
            foreach (string key in new[] { "s064", "s208", "s055" }) C<ComboBox>("LogLevelChoice").Items.Add(new LocalizedChoice { Key = key });
            C<ComboBox>("LogView").SelectedIndex = C<ComboBox>("LogLevelChoice").SelectedIndex = 0;
            C<ComboBox>("LogView").SelectionChanged += (s, e) => { logDirty = true; RefreshLogs(); };
            C<ComboBox>("LogLevelChoice").SelectionChanged += (s, e) => { logDirty = true; RefreshLogs(); };
            C<RichTextBox>("EventLog").AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((s, e) => {
                if (!rebuildingLogs && e.VerticalChange != 0 && e.ExtentHeightChange == 0) { followEvents = e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 2; UpdateLatestButton(); }
            }));
            C<TextBox>("Log").AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((s, e) => {
                if (!rebuildingLogs && e.VerticalChange != 0 && e.ExtentHeightChange == 0) { followRaw = e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 2; UpdateLatestButton(); }
            }));
            C<Button>("LatestLog").Click += (s, e) => { followEvents = followRaw = true; C<RichTextBox>("EventLog").ScrollToEnd(); C<TextBox>("Log").ScrollToEnd(); UpdateLatestButton(); };
        }
        void UpdateLatestButton()
        {
            bool follow = C<ComboBox>("LogView").SelectedIndex == 1 ? followRaw : followEvents;
            C<Button>("LatestLog").Visibility = follow ? Visibility.Collapsed : Visibility.Visible;
        }
        IEnumerable<LogEntry> VisibleEvents()
        {
            int level = C<ComboBox>("LogLevelChoice").SelectedIndex;
            return events.Entries.Where(x => level == 0 || x.Level == "Error" || level == 1 && x.Level == "Warning");
        }
        internal void RefreshLogs()
        {
            lock (pendingEvents) { while (pendingEvents.Count > 0) { events.Add(pendingEvents.Dequeue()); logDirty = true; } }
            lock (pendingRaw) { while (pendingRaw.Count > 0) { rawLines.Enqueue(pendingRaw.Dequeue()); logDirty = true; } }
            while (rawLines.Count > 500) rawLines.Dequeue();
            if (!logDirty || currentPage != "Log") return;
            logDirty = false; rebuildingLogs = true;
            try {
                bool raw = C<ComboBox>("LogView").SelectedIndex == 1;
                var box = C<RichTextBox>("EventLog"); var plain = C<TextBox>("Log");
                box.Visibility = raw ? Visibility.Collapsed : Visibility.Visible;
                plain.Visibility = raw ? Visibility.Visible : Visibility.Collapsed;
                C<ComboBox>("LogLevelChoice").Visibility = raw ? Visibility.Collapsed : Visibility.Visible;
                double offset = box.VerticalOffset, rawOffset = plain.VerticalOffset;
                box.Document.Blocks.Clear(); box.Document.PagePadding = new Thickness(4);
                bool dark = ThemeManager.Effective(settings.Theme) == "Dark";
                foreach (var item in VisibleEvents()) {
                    var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
                    var time = new Run(item.Time.ToString("HH:mm:ss") + "  "); time.SetResourceReference(TextElement.ForegroundProperty, "Brush526F83"); paragraph.Inlines.Add(time);
                    var body = new Run("[" + L.Level(item.Level) + "] " + item.RenderMessage() + (item.Repeats > 1 ? " ×" + item.Repeats : ""));
                    if (item.Level == "Error" || item.Level == "Warning") {
                        body.FontWeight = FontWeights.SemiBold;
                        body.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.Level == "Error" ? dark ? "#FFAB9C" : "#A82E27" : dark ? "#F5D28E" : "#865700"));
                    } else body.SetResourceReference(TextElement.ForegroundProperty, "Brush284A62");
                    paragraph.Inlines.Add(body); box.Document.Blocks.Add(paragraph);
                }
                plain.Text = String.Join(Environment.NewLine, rawLines);
                if (followEvents) box.ScrollToEnd(); else box.ScrollToVerticalOffset(offset);
                if (followRaw) plain.ScrollToEnd(); else plain.ScrollToVerticalOffset(rawOffset);
                UpdateLatestButton();
            } finally { rebuildingLogs = false; }
        }
        void ClearLogs()
        {
            lock (pendingEvents) pendingEvents.Clear(); lock (pendingRaw) pendingRaw.Clear();
            events.Entries.Clear(); rawLines.Clear(); logDirty = true; RefreshLogs();
        }
        internal string ExportLogText()
        {
            RefreshLogs(); return C<ComboBox>("LogView").SelectedIndex == 1 ? String.Join(Environment.NewLine, rawLines) : String.Join(Environment.NewLine, VisibleEvents().Select(x => x.ToString()));
        }
    }
}
