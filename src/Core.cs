using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Xml.Serialization;

namespace AirspyPanel
{
    public class Settings
    {
        public string Language = "System";
        public int Port = 30005;
        public string Format = "Beast";
        public string Gain = "auto";
        public string Sample = "auto";
        public int Filter = 4;
        public bool Packing = false;
        public string VrsPath = "";
        public string MapUrl = "http://127.0.0.1/VirtualRadar/";
        public string DecoderFolder = "";
        public string Theme = "Light";
        public bool CompactMode = false;
        public double FullWidth = 1180;
        public double FullHeight = 820;
        public double CompactWidth = 320, CompactHeight = 620;
        public bool FullMaximized = false;
        public int VrsReceiverId = 0;
        public string CloseAction = "Ask";
    }

    static class SettingsStore
    {
        public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AirspyPanel"); } }
        public static string FilePath { get { return Path.Combine(Folder, "AirspyPanel.settings.xml"); } }
        public static Settings Read(string path)
        {
            using (var file = File.OpenRead(path)) {
                var document = System.Xml.Linq.XDocument.Load(file);
                using (var reader = document.CreateReader()) {
                    var settings = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(reader);
                    if (document.Root.Element("Language") == null) settings.Language = "zh-CN";
                    if (settings.Sample == "自动") settings.Sample = "auto";
                    return settings;
                }
            }
        }
        public static void Write(string path, Settings value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var file = File.Create(path + ".tmp")) new XmlSerializer(typeof(Settings)).Serialize(file, value);
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
            else File.Move(path + ".tmp", path);
        }
    }

    static class InstallationPaths
    {
        public static string FindVrs()
        {
            foreach (var folder in new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            {
                if (String.IsNullOrWhiteSpace(folder)) continue;
                string path = Path.Combine(folder, "VirtualRadar", "VirtualRadar.exe");
                if (File.Exists(path)) return path;
            }
            return "";
        }
        public static string DecoderFolder(string executable)
        {
            if (String.IsNullOrWhiteSpace(executable))
                throw new ArgumentException(L.Source("s216"));
            string path = Path.GetFullPath(executable.Trim());
            if (!String.Equals(Path.GetFileName(path), "airspy_adsb.exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(L.Source("s202"));
            return Path.GetDirectoryName(path);
        }
    }

    static class Commands
    {
        public static string Build(Settings s)
        {
            if (s.Sample == "自动") s.Sample = "auto";
            if (s.Port < 1024 || s.Port > 65535) throw new ArgumentException(L.Source("s185"));
            if (s.Format != "Beast" && s.Format != "AVR" && s.Format != "ASAVR") throw new ArgumentException(L.Source("s162"));
            int gain;
            if (s.Gain != "auto" && (!Int32.TryParse(s.Gain, out gain) || gain < 0 || gain > 21))
                throw new ArgumentException(L.Source("s092"));
            if (s.Sample != "auto" && s.Sample != "12" && s.Sample != "20" && s.Sample != "24")
                throw new ArgumentException(L.Source("s163"));
            if (s.Filter < 1 || s.Filter > 60) throw new ArgumentException(L.Source("s226"));
            return "-v -l " + s.Port + ":" + s.Format.ToLowerInvariant() +
                " -R rms -g " + s.Gain + " -e " + s.Filter +
                (s.Sample == "auto" ? "" : " -m " + s.Sample) +
                (s.Packing ? " -p" : "");
        }
        public static bool PortInUse(int port)
        {
            foreach (System.Net.IPEndPoint ep in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                if (ep.Port == port) return true;
            return false;
        }
    }

}
