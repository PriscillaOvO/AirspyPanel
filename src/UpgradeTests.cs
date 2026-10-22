using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AirspyPanel
{
    static class UpgradeTests
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "AirspyPanel.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string configPath = Path.Combine(root, "settings.xml");
            File.WriteAllText(configPath, "<Settings><Port>30003</Port><Packing>true</Packing></Settings>");
            var old = SettingsStore.Read(configPath);
            Check(old.CloseAction == "Ask", "legacy close choice defaults to ask");
            Check(old.Language == "zh-CN", "legacy language remains Chinese");
            old.Language = "de-DE"; old.Sample = "auto";
            old.CloseAction = "Tray";
            Check(old.Theme == "Light" && !old.CompactMode && old.Port == 30003 && old.Packing, "legacy settings defaults");
            old.Theme = "Dark"; old.CompactMode = true; old.FullWidth = 1090; old.FullHeight = 730;
            SettingsStore.Write(configPath, old);
            var restored = SettingsStore.Read(configPath);
            Check(restored.Language == "de-DE" && restored.Sample == "auto", "stable language and sampling IDs");
            L.Validate(); Check(L.Resolve("es-ES") == "en-US", "unsupported language fallback");
            Check(restored.CloseAction == "Tray", "close policy persistence");
            var buffer = new EventBuffer();
            buffer.Add(new LogEntry { Time = DateTime.Now, Message = "same", Level = "Warning" });
            buffer.Add(new LogEntry { Time = DateTime.Now, Message = "same", Level = "Warning" });
            Check(buffer.Entries.Count == 1 && buffer.Entries[0].Repeats == 2, "event coalescing");
            for (int i = 0; i < 600; i++) buffer.Add(new LogEntry { Time = DateTime.Now, Message = i.ToString(), Level = "Info" });
            Check(buffer.Entries.Count == 500 && buffer.Entries[0].Message == "100", "event buffer bounded");
            string signalName = @"Local\AirspyPanel.Tests." + Guid.NewGuid().ToString("N");
            using (var signal = new EventWaitHandle(false, EventResetMode.AutoReset, signalName)) {
                InstanceSignal.Wake(signalName); Check(signal.WaitOne(1000), "single instance wake signal");
            }
            Check(restored.Theme == "Dark" && restored.CompactMode && restored.FullWidth == 1090 && restored.Packing, "presentation settings persistence");
            old.VrsReceiverId = 4; old.CompactWidth = 345; old.CompactHeight = 680; old.FullMaximized = true;
            SettingsStore.Write(configPath, old); restored = SettingsStore.Read(configPath);
            Check(restored.VrsReceiverId == 4 && restored.CompactWidth == 345 && restored.CompactHeight == 680 && restored.FullMaximized, "receiver and both window modes roundtrip");
            foreach (string suffix in new[] { "/VirtualRadar", "/VirtualRadar/", "/VirtualRadar/desktop.html#", "/VirtualRadar/AircraftList.json?feed=99" }) {
                var uri = Vrs.ApiUri("http://127.0.0.1:8088" + suffix, 4);
                Check(uri.AbsolutePath == "/VirtualRadar/AircraftList.json" && uri.Port == 8088 && uri.Query == "?feed=4" && uri.Fragment == "", "map URL normalization " + suffix);
            }
            string first = Path.Combine(root, "first"), second = Path.Combine(root, "second");
            Directory.CreateDirectory(first); Directory.CreateDirectory(second);
            File.WriteAllText(Path.Combine(first, "airspy_adsb.exe"), "test fixture - never executed");
            File.WriteAllText(Path.Combine(second, "airspy_adsb.exe"), "test fixture - never executed");
            File.WriteAllText(Path.Combine(first, "VirtualRadar.exe"), "test fixture - never executed");
            string installer = Path.Combine(root, "InstallerConfiguration.xml");
            File.WriteAllText(installer, "<InstallerConfiguration><WebServerPort>8088</WebServerPort></InstallerConfiguration>");
            Check(Discovery.MapAddress(installer) == "http://127.0.0.1:8088/VirtualRadar/", "installer web port");
            var found = Discovery.Scan(new[] { root, root, first + Path.DirectorySeparatorChar }, 3, 100, TimeSpan.FromSeconds(3), CancellationToken.None, null, installer);
            Check(found.Decoders.Count == 2 && found.VrsPrograms.Count == 1, "discovery multiple/dedup");
            Check(Discovery.ChooseExistingOrUnique("missing", found.Decoders) == "missing", "ambiguous scan must not auto-select");
            Check(Discovery.ChooseExistingOrUnique("missing", found.VrsPrograms) == found.VrsPrograms[0], "unique scan selection");
            Check(Discovery.ChooseExistingOrUnique(found.Decoders[0], found.VrsPrograms) == found.Decoders[0], "preserve valid path");
            var absent = Discovery.Scan(new[] { Path.Combine(root, "absent") }, 3, 100, TimeSpan.FromSeconds(3), CancellationToken.None, null, installer);
            Check(absent.Decoders.Count == 0 && absent.VrsPrograms.Count == 0, "missing folder");
            var limited = Discovery.Scan(new[] { root }, 3, 1, TimeSpan.FromSeconds(3), CancellationToken.None, null, installer);
            Check(limited.Limited, "directory scan budget");
            using (var cancel = new CancellationTokenSource()) {
                cancel.Cancel(); bool cancelled = false;
                try { Discovery.Scan(new[] { root }, 3, 100, TimeSpan.FromSeconds(3), cancel.Token, null, installer); }
                catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled, "scan cancellation");
            }
            string vrsFile = Path.Combine(root, "vrs.xml");
            Check(!Vrs.ReadConfig(30003, vrsFile).Exists, "missing VRS config");
            Vrs.Prepare(30003, vrsFile);
            Check(Vrs.ReadConfig(30003, vrsFile).Matches && !Vrs.ReadConfig(30005, vrsFile).Matches, "structured config match");
            int feed = Vrs.ReadConfig(30003, vrsFile).ReceiverId;
            var vrsXml = XDocument.Load(vrsFile);
            var receiver = vrsXml.Descendants("Receiver").Single();
            receiver.Element("Name").Value = "My Airspy"; receiver.Element("Address").Value = "localhost";
            vrsXml.Save(vrsFile);
            Check(Vrs.ReadConfig(30003, vrsFile).Matches, "custom name and localhost accepted");
            var other = new XElement(receiver); other.Element("UniqueId").Value = "44"; other.Element("Name").Value = "Second Airspy";
            receiver.Parent.Add(other); vrsXml.Save(vrsFile);
            var multiple = Vrs.ReadConfig(30003, vrsFile);
            Check(!multiple.Matches && multiple.Candidates.Count == 2 && multiple.ReceiverId == 0, "ambiguous receiver requires selection");
            Check(Vrs.ReadConfig(30003, vrsFile, 44).Matches && Vrs.ReadConfig(30003, vrsFile, 44).ReceiverId == 44, "explicit receiver selection");
            File.WriteAllText(vrsFile, "broken XML");
            Check(!Vrs.ReadConfig(30003, vrsFile, 44).Matches && Vrs.ReadConfig(30003, vrsFile, 44).ReceiverId == 44, "unreadable config preserves chosen source");
            var online = Reply("{\"totalAc\":0,\"srcFeed\":" + feed + ",\"acList\":[]}", feed, false);
            Check(online.Online && online.ReceiverMatches && online.Count == 0, "zero aircraft is online: " + online.Message + " expected " + feed + " actual " + online.Feed);
            var wrong = Reply("{\"totalAc\":2,\"srcFeed\":999,\"acList\":[]}", feed, false);
            Check(wrong.Online && !wrong.ReceiverMatches, "wrong feed");
            var unknown = Reply("{\"totalAc\":2,\"acList\":[]}", feed, false);
            Check(unknown.Online && !unknown.ReceiverMatches && unknown.Count == 2, "missing feed does not hide online count");
            Check(!Reply("<html>not VRS</html>", feed, false).Online, "invalid webpage");
            Check(!Reply("{}", feed, false).Online, "invalid JSON shape");
            Check(!Reply("{\"totalAc\":2,\"acList\":null}", feed, false).Online, "invalid aircraft array");
            var httpError = Reply("{}", feed, false, "503 Service Unavailable");
            Check(!httpError.Online && httpError.FailureKind == "HTTP 503", "HTTP status diagnostic");
            var timeout = Reply("", feed, true);
            Check(!timeout.Online && !String.IsNullOrEmpty(timeout.Error), "HTTP timeout");
            var offlineListener = new TcpListener(IPAddress.Loopback, 0); offlineListener.Start();
            int offlinePort = ((IPEndPoint)offlineListener.LocalEndpoint).Port; offlineListener.Stop();
            Check(!Vrs.Probe("http://127.0.0.1:" + offlinePort + "/VirtualRadar/", feed).GetAwaiter().GetResult().Online, "offline VRS");
        }
        static VrsStatus Reply(string body, int feed, bool timeout, string statusLine = "200 OK")
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = Task.Factory.StartNew(() => {
                using (var client = listener.AcceptTcpClient())
                using (var stream = client.GetStream()) {
                    stream.ReadTimeout = 5000;
                    var reader = new StreamReader(stream);
                    string line = reader.ReadLine();
                    Check(line.Contains("feed=" + feed), "probe captured feed ID");
                    while (!String.IsNullOrEmpty(reader.ReadLine())) { }
                    if (timeout) { Thread.Sleep(3000); return; }
                    byte[] bytes = Encoding.UTF8.GetBytes(body);
                    byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + statusLine + "\r\nContent-Type: application/json\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
                    stream.Write(header, 0, header.Length); stream.Write(bytes, 0, bytes.Length);
                    Thread.Sleep(200);
                }
            }, TaskCreationOptions.LongRunning);
            try {
                var status = Vrs.Probe("http://127.0.0.1:" + port + "/VirtualRadar/", feed, timeout ? 500 : 6000).GetAwaiter().GetResult();
                Check(server.Wait(5000), "mock responder completion");
                return status;
            } finally { listener.Stop(); }
        }
    }
}
