using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace AirspyPanel
{
    static class Tests
    {
        static void Assert(bool ok, string text) { if (!ok) throw new Exception(text); }
        static byte[] Encode(byte type, byte[] data)
        {
            var bytes = new List<byte>(); bytes.Add(0x1a); bytes.Add(type);
            foreach (var b in data) { bytes.Add(b); if (b == 0x1a) bytes.Add(b); }
            return bytes.ToArray();
        }
        static byte[] Frame(string hex, byte level)
        {
            var bytes = new byte[7 + hex.Length / 2]; bytes[6] = level;
            for (int i = 0; i < hex.Length / 2; i++) bytes[i + 7] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
        public static int Run()
        {
            string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test.txt");
            try {
                string customFolder = Path.Combine(Path.GetTempPath(), "Airspy test", "中文路径");
                Assert(InstallationPaths.DecoderFolder(Path.Combine(customFolder, "airspy_adsb.exe")) == customFolder, "decoder path with spaces and Unicode");
                bool wrongExe = false;
                try { InstallationPaths.DecoderFolder(Path.Combine(customFolder, "other.exe")); }
                catch (ArgumentException) { wrongExe = true; }
                Assert(wrongExe, "wrong decoder executable accepted");
                var settings = new Settings();
                Assert(Commands.Build(settings) == "-v -l 30005:beast -R rms -g auto -e 4", "default args");
                settings.Port = 99999; bool invalid = false;
                try { Commands.Build(settings); } catch (ArgumentException) { invalid = true; }
                Assert(invalid, "invalid port accepted"); settings.Port = 30003;
                settings.Packing = true; settings.Gain = "21";
                settings.DecoderFolder = customFolder;
                settings.VrsPath = Path.Combine(customFolder, "VirtualRadar.exe");
                using (var ms = new MemoryStream()) {
                    var serializer = new XmlSerializer(typeof(Settings)); serializer.Serialize(ms, settings); ms.Position = 0;
                    var loaded = (Settings)serializer.Deserialize(ms);
                    Assert(loaded.Packing && loaded.Port == 30003 && loaded.Gain == "21", "settings roundtrip");
                    Assert(loaded.DecoderFolder == customFolder && loaded.VrsPath == settings.VrsPath, "custom paths roundtrip");
                }
                var messages = new List<string>(); int exit = -1; bool stopped = false;
                string exe = Assembly.GetExecutingAssembly().Location;
                using (var decoder = new Decoder()) {
                    decoder.Line += line => { lock (messages) messages.Add(line); };
                    decoder.Ended += (code, stop) => { exit = code; stopped = stop; };
                    decoder.Start(exe, "--test-worker");
                    Assert(decoder.Wait(5000) && exit == 7 && !stopped, "exit propagation");
                    Assert(messages.Contains("first") && messages.Contains("second") && messages.Contains("stderr-message") && messages.Contains("partial-final"), "stdout stderr CR LF");
                    decoder.Start(exe, "--test-wait"); decoder.Stop();
                    Assert(decoder.Wait(5000) && stopped && !decoder.Running, "stop/restart");
                }
                byte[] frame = Frame("8D4840D6202CC371C32CE0576098", 26);
                byte[] encoded = Encode(0x33, frame);
                var parsed = new List<byte[]>();
                var parser = new BeastParser { Frame = (t, f) => { Assert(t == 0x33, "type"); parsed.Add(f); } };
                foreach (byte b in encoded) parser.Feed(new[] { b }, 1);
                Assert(parsed.Count == 1 && parsed[0].SequenceEqual(frame), "split escaped frame");
                parser.Feed(new byte[] { 0x1a, 0x33, 0, 0, 0x1a, 0x32, 0, 0 }, 8);
                parser.Feed(encoded, encoded.Length);
                Assert(parsed.Count == 2, "resync after truncated frame");
                var now = DateTime.UtcNow;
                var metrics = new Metrics(); metrics.Add(0x33, frame, now);
                byte[] f2 = Frame("8D4840D6202CC371C32CE0576098", 255); metrics.Add(0x33, f2, now);
                var snap = metrics.Get(now);
                Assert(snap.Planes.Count == 1 && snap.Total == 2, "aircraft dedup");
                Assert(snap.Planes[0].Callsign == "KLM1023", "callsign decoding");
                Assert(Math.Abs(snap.Peak - 100) < .001 && Math.Abs(snap.Mean - (26 + 255) / 2.0 / 255 * 100) < .001, "meters");
                metrics.Add(0x33, Frame("8D40621D58C382D690C8AC2863A7", 180), now);
                Assert(metrics.Get(now).Planes.First(p => p.Icao == "40621D").Altitude.Replace(",", "") == "38000 ft", "altitude");
                metrics.Add(0x32, Frame("20010203040506", 100), now);
                Assert(metrics.Get(now).Planes.Count == 2, "DF4 must not generate ICAO");
                Assert(!metrics.Get(now.AddSeconds(4)).HasSignal, "signal stale timeout");
                Assert(metrics.Get(now.AddSeconds(61)).Planes.Count == 0, "aircraft timeout");
                var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                using (var monitor = new BeastMonitor()) {
                    monitor.Start(port);
                    var accept = listener.AcceptTcpClientAsync(); Assert(accept.Wait(4000), "monitor TCP connect");
                    using (var client = accept.Result) {
                        client.GetStream().Write(encoded, 0, encoded.Length);
                        var until = DateTime.UtcNow.AddSeconds(3);
                        while (monitor.Get().Total == 0 && DateTime.UtcNow < until) Thread.Sleep(25);
                        Assert(monitor.Get().Connected && monitor.Get().Planes.Count == 1, "monitor live TCP");
                    }
                    var reconnect = listener.AcceptTcpClientAsync(); Assert(reconnect.Wait(5000), "monitor reconnect");
                    using (var again = reconnect.Result) {
                        again.GetStream().Write(encoded, 0, encoded.Length);
                        var until = DateTime.UtcNow.AddSeconds(3);
                        while (monitor.Get().Total < 2 && DateTime.UtcNow < until) Thread.Sleep(25);
                        Assert(monitor.Get().Total == 2, "reconnected data");
                    }
                    monitor.Stop(); Assert(!monitor.Get().Connected, "monitor stop");
                }
                listener.Stop();
                string temporary = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-vrs.xml");
                new XDocument(new XElement("Configuration", new XElement("DataVersion", 12),
                    new XElement("Receivers", new XElement("Receiver", new XElement("UniqueId", 9),
                    new XElement("Name", "Existing"), new XElement("Port", 2222))),
                    new XElement("OtherSetting", "keep"))).Save(temporary);
                string backup = Vrs.Prepare(30003, temporary); var config = XDocument.Load(temporary);
                Assert(config.Root.Element("OtherSetting").Value == "keep" && File.Exists(backup), "VRS preserve backup");
                Assert(config.Descendants("Receiver").Count() == 2 && config.Descendants("DataSource").Single().Value == "Beast", "VRS data format");
                Vrs.Prepare(31005, temporary); config = XDocument.Load(temporary);
                Assert(config.Descendants("Receiver").Count() == 2 && config.Descendants("Receiver").Last().Element("Port").Value == "31005", "VRS idempotent");
                Assert(Vrs.ApiUri("http://127.0.0.1:8080/VirtualRadar/desktop.html").AbsolutePath == "/VirtualRadar/AircraftList.json", "VRS URL");
                var web = new TcpListener(IPAddress.Loopback, 0); web.Start();
                int webPort = ((IPEndPoint)web.LocalEndpoint).Port;
                string webTrace = "listening " + DateTime.UtcNow.ToString("O");
                var responder = Task.Factory.StartNew(() => {
                    using (var connection = web.AcceptTcpClient())
                    using (var stream = connection.GetStream()) {
                        webTrace += " accepted " + DateTime.UtcNow.ToString("O");
                        var reader = new StreamReader(stream);
                        string requestLine = reader.ReadLine();
                        webTrace += " request " + requestLine + " " + DateTime.UtcNow.ToString("O");
                        Assert(requestLine.Contains("feed=" + 10), "feed query");
                        while (!String.IsNullOrEmpty(reader.ReadLine())) { }
                        string body = "{\"totalAc\":3,\"srcFeed\":" + 10 + ",\"acList\":[]}";
                        byte[] response = System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n" + body);
                        stream.Write(response, 0, response.Length);
                        webTrace += " replied " + DateTime.UtcNow.ToString("O");
                        Thread.Sleep(200); // Allow local HTTP inspection software to drain the response before FIN.
                    }
                }, TaskCreationOptions.LongRunning);
                var probe = Vrs.Probe("http://127.0.0.1:" + webPort + "/VirtualRadar/", 10).GetAwaiter().GetResult();
                Assert(responder.Wait(3000) && probe.Online && probe.ReceiverMatches && probe.Count == 3, "VRS HTTP API and feed: " + probe.Message + " " + probe.Error + " " + webTrace);
                web.Stop();
                UpgradeTests.Run();
                File.WriteAllText(report, "PASS\nArguments and invalid input; settings roundtrip; child stdout/stderr and exit/stop/restart;\nBeast split/escaped frames and resync; aircraft dedup, callsign, altitude, DF filtering;\nmean/peak signals, stale signal/aircraft expiry; loopback TCP monitoring and reconnect;\nVRS config backup/preservation/idempotence, HTTP API and feed selection.\nUpgrade checks: legacy/theme/window settings, discovery candidates/cancellation/budget, VRS zero-count/wrong-feed/invalid-response/timeout/offline.\nNo synthetic data was sent to the real VRS instance.\n");
                return 0;
            } catch (Exception ex) { File.WriteAllText(report, "FAIL\n" + ex); return 1; }
        }
    }
}
