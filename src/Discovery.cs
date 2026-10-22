using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Win32;

namespace AirspyPanel
{
    sealed class DiscoveryResult
    {
        public readonly List<string> Decoders = new List<string>();
        public readonly List<string> VrsPrograms = new List<string>();
        public string MapUrl;
        public int Visited, Skipped;
        public bool Limited;
    }
    static class Discovery
    {
        public static string MapAddress(string installerFile)
        {
            try {
                var doc = XDocument.Load(installerFile);
                int port;
                string value = doc.Descendants("WebServerPort").Select(x => x.Value).FirstOrDefault();
                if (Int32.TryParse(value, out port) && port > 0 && port <= 65535)
                    return "http://127.0.0.1" + (port == 80 ? "" : ":" + port) + "/VirtualRadar/";
            } catch (Exception) { }
            return "http://127.0.0.1/VirtualRadar/";
        }
        public static List<string> CommonRoots(Settings settings)
        {
            var result = new List<string> { settings.DecoderFolder, Path.GetDirectoryName(settings.VrsPath ?? ""),
                AppDomain.CurrentDomain.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") };
            foreach (var name in new[] { "airspy_adsb", "VirtualRadar" })
                foreach (var process in Process.GetProcessesByName(name))
                    using (process) { try { result.Insert(0, Path.GetDirectoryName(process.MainModule.FileName)); } catch (Exception) { } }
            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
                foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                    try {
                        using (var key = RegistryKey.OpenBaseKey(hive, view))
                        using (var uninstall = key.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                            if (uninstall != null)
                                foreach (var sub in uninstall.GetSubKeyNames())
                                    using (var entry = uninstall.OpenSubKey(sub)) {
                                        if (entry == null) continue;
                                        string display = Convert.ToString(entry.GetValue("DisplayName"));
                                        if (display.IndexOf("Virtual Radar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            display.IndexOf("Airspy", StringComparison.OrdinalIgnoreCase) >= 0)
                                            result.Add(Convert.ToString(entry.GetValue("InstallLocation")));
                                    }
                    } catch (Exception) { }
            foreach (var drive in DriveInfo.GetDrives())
                try { if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    foreach (var name in new[] { "App", "Apps", "SDR" }) result.Add(Path.Combine(drive.RootDirectory.FullName, name)); }
                catch (IOException) { }
            return result.Where(x => !String.IsNullOrWhiteSpace(x) && Directory.Exists(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static DiscoveryResult Scan(IEnumerable<string> roots, int depth, int maxDirectories,
            TimeSpan budget, CancellationToken token, Action<int> progress, string installerFile)
        {
            var found = new DiscoveryResult { MapUrl = MapAddress(installerFile) };
            var queue = new Queue<Tuple<string, int>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots) if (!String.IsNullOrWhiteSpace(root)) queue.Enqueue(Tuple.Create(root, 0));
            var clock = Stopwatch.StartNew();
            while (queue.Count > 0) {
                token.ThrowIfCancellationRequested();
                if (found.Visited >= maxDirectories || clock.Elapsed > budget) { found.Limited = true; break; }
                var item = queue.Dequeue();
                try {
                    string folder = Path.GetFullPath(item.Item1);
                    if (folder.Length > Path.GetPathRoot(folder).Length) folder = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (!seen.Add(folder) || !Directory.Exists(folder)) continue;
                    if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) { found.Skipped++; continue; }
                    found.Visited++;
                    foreach (var name in new[] { "airspy_adsb.exe", "VirtualRadar.exe" }) {
                        string path = Path.Combine(folder, name);
                        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0) {
                            var candidates = name == "airspy_adsb.exe" ? found.Decoders : found.VrsPrograms;
                            if (!candidates.Contains(path, StringComparer.OrdinalIgnoreCase)) candidates.Add(path);
                        }
                    }
                    if (item.Item2 < depth)
                        foreach (var sub in Directory.EnumerateDirectories(folder)) queue.Enqueue(Tuple.Create(sub, item.Item2 + 1));
                } catch (UnauthorizedAccessException) { found.Skipped++; }
                  catch (IOException) { found.Skipped++; }
                  catch (System.Security.SecurityException) { found.Skipped++; }
                if (progress != null && found.Visited % 50 == 0) progress(found.Visited);
            }
            return found;
        }
        public static string ChooseExistingOrUnique(string current, IList<string> candidates)
        {
            if (File.Exists(current)) return current;
            return candidates.Count == 1 ? candidates[0] : current;
        }
    }
}
