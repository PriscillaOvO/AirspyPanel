using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace AirspyPanel
{
    // Resource IDs are stable. Raw parameters (paths, exception details and
    // third-party output) are never used to select application behavior.
    sealed class MessagePart { public string Key, Argument; }
    sealed class LocalizedMessage
    {
        public readonly List<MessagePart> Parts = new List<MessagePart>();
        public string Code { get { return String.Join("+", Parts.Where(p => p.Key != null).Select(p => p.Key)); } }
        public string[] Parameters { get { return Parts.Where(p => p.Key == null).Select(p => p.Argument).ToArray(); } }
        public string Render() { return String.Concat(Parts.Select(p => p.Key == null ? p.Argument : L.Get(p.Key))); }
    }
    static class L
    {
        public static readonly string[] Languages = { "zh-CN", "en-US", "ja-JP", "fr-FR", "de-DE", "ru-RU" };
        public static readonly string[] NativeNames = { "简体中文", "English (US)", "日本語", "Français", "Deutsch", "Русский" };
        static readonly Dictionary<string, Dictionary<string, string>> catalogs = new Dictionary<string, Dictionary<string, string>>();
        static readonly Regex matcher = new Regex("\uE000([A-Za-z0-9_]+)\uE001", RegexOptions.Compiled);
        public static string Language = "zh-CN";
        static L()
        {
            foreach (var lang in Languages) using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AirspyPanel.Resources." + lang + ".xml")) {
                catalogs[lang] = XDocument.Load(stream).Root.Elements("text").ToDictionary(e => (string)e.Attribute("key"), e => e.Value);
            }
        }
        public static string Resolve(string language)
        {
            if (language == "System" || String.IsNullOrEmpty(language)) language = CultureInfo.CurrentUICulture.Name;
            if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
            return Languages.FirstOrDefault(x => language.StartsWith(x.Substring(0, 2), StringComparison.OrdinalIgnoreCase)) ?? "en-US";
        }
        public static string Get(string key) { string value; return catalogs[Language].TryGetValue(key, out value) ? value : catalogs["en-US"][key]; }
        public static string Source(string key) { if (!catalogs["en-US"].ContainsKey(key)) throw new ArgumentException("Unknown resource " + key); return "\uE000" + key + "\uE001"; }
        public static string Text(string source) { return Capture(source).Render(); }
        public static LocalizedMessage Capture(string source)
        {
            var message = new LocalizedMessage(); if (source == null) return message;
            int offset = 0;
            foreach (Match match in matcher.Matches(source)) {
                if (match.Index > offset) message.Parts.Add(new MessagePart { Argument = source.Substring(offset, match.Index - offset) });
                message.Parts.Add(new MessagePart { Key = match.Groups[1].Value }); offset = match.Index + match.Length;
            }
            if (offset < source.Length) message.Parts.Add(new MessagePart { Argument = source.Substring(offset) });
            return message;
        }
        public static void Apply(Window window, string language)
        {
            Language = Resolve(language);
            foreach (var pair in catalogs[Language]) window.Resources["Loc_" + pair.Key] = pair.Value;
            window.Language = System.Windows.Markup.XmlLanguage.GetLanguage(Language);
        }
        public static string Level(string level) { return Get(level == "Error" ? "s244" : level == "Warning" ? "s207" : "s059"); }
        public static void Validate()
        {
            var keys = catalogs["en-US"].Keys.OrderBy(x => x).ToArray();
            foreach (var lang in Languages) {
                if (!keys.SequenceEqual(catalogs[lang].Keys.OrderBy(x => x))) throw new Exception("Resource keys differ: " + lang);
                foreach (var key in keys) if (String.IsNullOrWhiteSpace(catalogs[lang][key])) throw new Exception("Empty resource: " + lang + "/" + key);
            }
        }
    }
    sealed class LocalizedChoice
    {
        public string Value { get; set; }
        public string Key { get; set; }
        public override string ToString() { return Key == null ? Value : L.Get(Key); }
    }
    sealed partial class PanelApp
    {
        readonly Dictionary<string, string> displayedText = new Dictionary<string, string>();
        bool changingLanguage;
        void SetupLanguage()
        {
            var choice = C<ComboBox>("LanguageChoice");
            choice.Items.Add(new LocalizedChoice { Value = "System", Key = "s223" });
            foreach (string name in L.NativeNames) choice.Items.Add(name);
            choice.SelectedIndex = settings.Language == "System" ? 0 : Math.Max(0, Array.IndexOf(L.Languages, settings.Language) + 1);
            choice.SelectionChanged += (s,e) => {
                if (changingLanguage) return;
                ChangeLanguage(choice.SelectedIndex == 0 ? "System" : L.Languages[choice.SelectedIndex - 1]);
            };
            C<Button>("GettingStarted").Click += async (s,e) => { Navigate("SettingsPaths"); await ScanPaths(null); };
            C<Button>("AirspyDownload").Click += (s,e) => OpenOfficial("https://airspy.com/download/");
            C<Button>("VrsDownload").Click += (s,e) => OpenOfficial("https://www.virtualradarserver.co.uk/Download.aspx");
            ChangeLanguage(settings.Language, false);
            if (!preview && (!System.IO.File.Exists(settings.VrsPath) || !System.IO.File.Exists(System.IO.Path.Combine(settings.DecoderFolder, "airspy_adsb.exe")))) Navigate("Settings");
        }
        void OpenOfficial(string url) { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch(Exception ex) { Error(ex); } }
        internal void ChangeLanguage(string language, bool save = true)
        {
            changingLanguage = true;
            try {
                settings.Language = language; L.Apply(Window, language);
                C<ComboBox>("LanguageChoice").SelectedIndex = language == "System" ? 0 : Math.Max(0, Array.IndexOf(L.Languages, language) + 1);
                foreach (var pair in displayedText) C<TextBlock>(pair.Key).Text = L.Text(pair.Value);
                foreach (string name in new[] { "ThemeChoice", "CloseChoice", "LogView", "LogLevelChoice", "LanguageChoice", "Sample", "ReceiverChoice" }) C<ComboBox>(name).Items.Refresh();
                C<Button>("ToggleMode").Content = L.Get(settings.CompactMode ? "s101" : "s191");
                string[] headers = { null, "s081", "s251", "s179", "s122", "s221" };
                for (int i = 1; i < headers.Length; i++) C<DataGrid>("Planes").Columns[i].Header = L.Get(headers[i]);
                RefreshTrayLanguage(); logDirty = true; RefreshLogs();
                if (save) Save();
            } finally { changingLanguage = false; }
        }
    }
}
