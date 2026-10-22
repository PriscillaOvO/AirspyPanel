using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace AirspyPanel
{
    sealed class VrsStatus
    {
        public bool Online, ReceiverMatches;
        public int Count, Feed;
        public string Message, Error;
        public string RequestUrl, FailureKind;
    }
    sealed class VrsConfiguration
    {
        public bool Exists, Matches;
        public int ReceiverId;
        public string Message;
        public List<VrsReceiver> Candidates = new List<VrsReceiver>();
    }
    sealed class VrsReceiver
    {
        public int Id;
        public string Name;
        public override string ToString() { return L.Text(Name) + (Id == 0 ? "" : " · ID " + Id); }
    }
    static class Vrs
    {
        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualRadar");
        public static string ConfigPath { get { return Path.Combine(Folder, "Configuration.xml"); } }
        static void Set(XElement parent, string name, object value)
        {
            var child = parent.Element(name); if (child == null) { child = new XElement(name); parent.Add(child); }
            child.Value = Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        public static string Prepare(int port, string path)
        {
            XDocument doc = File.Exists(path) ? XDocument.Load(path) : new XDocument(new XElement("Configuration"));
            var root = doc.Root;
            if (root == null || root.Name.LocalName != "Configuration") throw new InvalidDataException(L.Source("s044"));
            var receivers = root.Element("Receivers");
            if (receivers == null) { receivers = new XElement("Receivers"); root.Add(receivers); }
            var receiver = receivers.Elements("Receiver").FirstOrDefault(x => (string)x.Element("Name") == "Airspy Panel");
            if (receiver == null) {
                int next = receivers.Elements("Receiver").Select(x => (int?)x.Element("UniqueId") ?? 0).DefaultIfEmpty(0).Max() + 1;
                receiver = new XElement("Receiver", new XElement("UniqueId", next), new XElement("Name", "Airspy Panel"));
                receivers.Add(receiver);
            }
            int receiverId = (int?)receiver.Element("UniqueId") ?? 1;
            Set(receiver, "Enabled", "true"); Set(receiver, "DataSource", "Beast");
            Set(receiver, "ConnectionType", "TCP"); Set(receiver, "Address", "127.0.0.1");
            Set(receiver, "Port", port); Set(receiver, "IsPassive", "false");
            Set(receiver, "AutoReconnectAtStartup", "true");
            var maps = root.Element("GoogleMapSettings");
            if (maps == null) { maps = new XElement("GoogleMapSettings"); root.Add(maps); }
            Set(maps, "WebSiteReceiverId", receiverId);
            if (maps.Element("MapProvider") == null) Set(maps, "MapProvider", "Leaflet");
            int v = (int?)root.Element("DataVersion") ?? 0; Set(root, "DataVersion", v + 1);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string backup = "";
            if (File.Exists(path)) { backup = path + ".panel-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"); File.Copy(path, backup); }
            doc.Save(path + ".panel-tmp");
            if (File.Exists(path)) File.Replace(path + ".panel-tmp", path, null); else File.Move(path + ".panel-tmp", path);
            return backup;
        }
        public static VrsConfiguration ReadConfig(int port, string path, int selectedId = 0)
        {
            if (!File.Exists(path)) return new VrsConfiguration { ReceiverId = selectedId, Message = L.Source("s165") };
            try {
                var doc = XDocument.Load(path);
                var all = doc.Descendants("Receiver").ToList();
                var matches = all.Where(x => (int?)x.Element("Port") == port && (string)x.Element("DataSource") == "Beast" &&
                    IsLocal((string)x.Element("Address")) && (string)x.Element("ConnectionType") == "TCP" &&
                    (bool?)x.Element("Enabled") == true && (bool?)x.Element("IsPassive") != true && (int?)x.Element("UniqueId") > 0).ToList();
                var candidates = matches.Select(x => new VrsReceiver { Id = (int)x.Element("UniqueId"), Name = (string)x.Element("Name") ?? L.Source("s135") }).ToList();
                var receiver = selectedId > 0 ? all.FirstOrDefault(x => (int?)x.Element("UniqueId") == selectedId) : matches.Count == 1 ? matches[0] : null;
                if (receiver == null) return new VrsConfiguration { Exists = true, ReceiverId = selectedId, Candidates = candidates,
                    Message = selectedId > 0 ? L.Source("s117") : matches.Count > 1 ? L.Source("s121") : L.Source("s159") };
                int receiverId = (int?)receiver.Element("UniqueId") ?? 0;
                bool match = (int?)receiver.Element("Port") == port && (string)receiver.Element("DataSource") == "Beast" &&
                    IsLocal((string)receiver.Element("Address")) && (string)receiver.Element("ConnectionType") == "TCP" &&
                    (bool?)receiver.Element("Enabled") == true && (bool?)receiver.Element("IsPassive") != true && receiverId > 0;
                return new VrsConfiguration { Exists = true, Matches = match, ReceiverId = receiverId, Candidates = candidates,
                    Message = match ? L.Source("s138") + port + " · Beast" : L.Source("s137") };
            } catch (Exception ex) { return new VrsConfiguration { Exists = true, ReceiverId = selectedId, Message = L.Source("s242") + ex.Message }; }
        }
        static bool IsLocal(string address)
        {
            IPAddress ip;
            return String.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase) ||
                (IPAddress.TryParse(address, out ip) && IPAddress.IsLoopback(ip));
        }
        public static Uri ApiUri(string mapUrl, int receiverId = 0)
        {
            Uri uri;
            if (!Uri.TryCreate(mapUrl, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https")) throw new ArgumentException(L.Source("s089"));
            string path = uri.AbsolutePath;
            int slash = path.LastIndexOf('/');
            if (!path.EndsWith("/")) path = Path.HasExtension(path.Substring(slash + 1)) ? path.Substring(0, slash + 1) : path + "/";
            return new UriBuilder(uri.Scheme, uri.Host, uri.Port, path + "AircraftList.json") {
                Query = receiverId > 0 ? "feed=" + receiverId : ""
            }.Uri;
        }
        public static Task<VrsStatus> Probe(string url, int receiverId = 0, int timeoutMs = 6000)
        {
            // Keep the legacy Framework HTTP stack entirely off the WPF/STA thread.
            return Task.Run(() => ProbeCore(url, receiverId, timeoutMs));
        }
        static VrsStatus ProbeCore(string url, int receiverId, int timeoutMs)
        {
            string endpoint = "";
            string stage = L.Source("s230");
            try {
                endpoint = ApiUri(url, receiverId).AbsoluteUri;
                var request = (HttpWebRequest)WebRequest.Create(endpoint);
                request.Proxy = null; request.Timeout = timeoutMs; request.ReadWriteTimeout = timeoutMs;
                request.KeepAlive = false;
                request.Accept = "application/json"; request.UserAgent = "AirspyPanel/2.1";
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using (var timeout = new CancellationTokenSource(timeoutMs))
                using (timeout.Token.Register(() => request.Abort()))
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    stage = L.Source("s217");
                    string json = reader.ReadToEnd();
                    Dictionary<string, object> data;
                    try { data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json); }
                    catch (Exception ex) { return new VrsStatus { RequestUrl = endpoint, FailureKind = "InvalidResponse", Error = ex.Message, Message = json.IndexOf("AdGuard", StringComparison.OrdinalIgnoreCase) >= 0 ?
                        L.Source("s126") : L.Source("s127") }; }
                    int count;
                    if (data == null || !data.ContainsKey("acList") || !(data["acList"] is IList) || !data.ContainsKey("totalAc") ||
                        !Int32.TryParse(Convert.ToString(data["totalAc"], CultureInfo.InvariantCulture), out count) || count < 0)
                        return new VrsStatus { RequestUrl = endpoint, FailureKind = "InvalidResponse", Message = L.Source("s091"), Error = "Invalid VRS response" };
                    int feed = 0;
                    if (data.ContainsKey("srcFeed")) Int32.TryParse(Convert.ToString(data["srcFeed"], CultureInfo.InvariantCulture), out feed);
                    return new VrsStatus { Online = true, RequestUrl = endpoint, ReceiverMatches = receiverId > 0 && feed == receiverId,
                        Count = count, Feed = feed,
                        Message = receiverId > 0 && feed != receiverId ? L.Source("s031") : L.Source("s042") };
                }
            } catch (WebException ex) {
                var response = ex.Response as HttpWebResponse;
                string kind = response != null ? "HTTP " + (int)response.StatusCode :
                    ex.Status == WebExceptionStatus.Timeout || ex.Status == WebExceptionStatus.RequestCanceled ? L.Source("s220") :
                    ex.Status == WebExceptionStatus.ConnectFailure ? L.Source("s232") : ex.Status.ToString();
                if (response != null) response.Close();
                return new VrsStatus { RequestUrl = endpoint, FailureKind = ex.Status == WebExceptionStatus.Timeout || ex.Status == WebExceptionStatus.RequestCanceled ? "Timeout" : ex.Status == WebExceptionStatus.ConnectFailure ? "ConnectionRefused" : response != null ? "HTTP " + (int)response.StatusCode : ex.Status.ToString(), Error = ex.Message, Message = L.Source("s035") + stage + "：" + kind + L.Source("s052") };
            } catch (Exception ex) { return new VrsStatus { RequestUrl = endpoint, FailureKind = "InvalidResponse", Message = L.Source("s043") + ex.Message, Error = ex.ToString() }; }
        }
    }
}
