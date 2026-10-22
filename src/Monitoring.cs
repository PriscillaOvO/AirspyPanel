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
    sealed class BeastParser
    {
        readonly byte[] frame = new byte[21];
        int count, length;
        byte type;
        bool escape;
        public Action<byte, byte[]> Frame;
        void Start(byte value)
        {
            type = value; count = 0;
            length = value == 0x31 ? 9 : value == 0x32 ? 14 : value == 0x33 ? 21 : 0;
        }
        void Data(byte value)
        {
            if (length == 0) return;
            frame[count++] = value;
            if (count == length)
            {
                byte[] copy = new byte[length]; Array.Copy(frame, copy, length);
                length = count = 0;
                if (Frame != null) Frame(type, copy);
            }
        }
        public void Feed(byte[] buffer, int n)
        {
            for (int i = 0; i < n; i++)
            {
                byte b = buffer[i];
                if (escape)
                {
                    escape = false;
                    if (b == 0x1a && length > 0) Data(b);
                    else Start(b);
                }
                else if (b == 0x1a) escape = true;
                else Data(b);
            }
        }
    }
    sealed class Plane
    {
        public string Icao { get; set; }
        public string Callsign { get; set; }
        public string Altitude { get; set; }
        public string Signal { get; set; }
        public string LastSeen { get; set; }
        public long Messages { get; set; }
        public DateTime Seen;
        public int Level;
        public Plane Clone(DateTime now)
        {
            return new Plane { Icao = Icao, Callsign = Callsign, Altitude = Altitude,
                Signal = (Level * 100.0 / 255).ToString("0.0") + "%",
                LastSeen = Math.Max(0, (int)(now - Seen).TotalSeconds) + L.Get("s018"),
                Messages = Messages, Seen = Seen, Level = Level };
        }
    }
    sealed class Snapshot
    {
        public List<Plane> Planes = new List<Plane>();
        public long Total;
        public double Rate, Mean, Peak;
        public bool HasSignal, Connected;
        public string State;
    }
    sealed class Metrics
    {
        sealed class Sample { public DateTime At; public int Signal; }
        readonly Dictionary<string, Plane> planes = new Dictionary<string, Plane>();
        readonly Queue<Sample> samples = new Queue<Sample>();
        readonly object gate = new object();
        long total;
        DateTime begin = DateTime.UtcNow;
        public void Clear() { lock (gate) { planes.Clear(); samples.Clear(); total = 0; begin = DateTime.UtcNow; } }
        public void Add(byte type, byte[] f, DateTime now)
        {
            if (f.Length != (type == 0x31 ? 9 : type == 0x32 ? 14 : type == 0x33 ? 21 : -1)) return;
            lock (gate)
            {
                total++;
                samples.Enqueue(new Sample { At = now, Signal = f[6] });
                Prune(now);
                if (type == 0x31) return;
                int df = f[7] >> 3;
                // Only explicit ICAO fields in DF11 / DF17; parity-overlaid replies
                // and non-ICAO DF18 addresses must not create fictitious aircraft.
                if (df != 11 && df != 17) return;
                if (df == 17 && type != 0x33) return;
                string icao = f[8].ToString("X2") + f[9].ToString("X2") + f[10].ToString("X2");
                if (icao == "000000" || icao == "FFFFFF") return;
                Plane p;
                if (!planes.TryGetValue(icao, out p)) { p = new Plane { Icao = icao, Callsign = "—", Altitude = "—" }; planes[icao] = p; }
                p.Seen = now; p.Level = f[6]; p.Messages++;
                if (df == 17)
                {
                    int tc = f[11] >> 3;
                    if (tc >= 1 && tc <= 4)
                    {
                        string chars = "#ABCDEFGHIJKLMNOPQRSTUVWXYZ#####_###############0123456789######";
                        var cs = new StringBuilder();
                        for (int c = 0; c < 8; c++)
                        {
                            int val = 0;
                            for (int j = 0; j < 6; j++) {
                                int bit = c * 6 + j;
                                val = (val << 1) | ((f[12 + bit / 8] >> (7 - bit % 8)) & 1);
                            }
                            char ch = chars[val]; cs.Append(ch == '_' || ch == '#' ? ' ' : ch);
                        }
                        string call = cs.ToString().Trim(); if (call.Length > 0) p.Callsign = call;
                    }
                    if (tc >= 9 && tc <= 18)
                    {
                        int altitude = (f[12] << 4) | (f[13] >> 4);
                        if ((altitude & 0x10) != 0) {
                            int n = ((altitude & 0xfe0) >> 1) | (altitude & 0xf);
                            p.Altitude = (n * 25 - 1000).ToString("N0") + " ft";
                        }
                    }
                }
            }
        }
        void Prune(DateTime now)
        {
            while (samples.Count > 0 && ((now - samples.Peek().At).TotalSeconds > 3 || samples.Count > 100000)) samples.Dequeue();
            foreach (string key in planes.Where(x => (now - x.Value.Seen).TotalSeconds > 60).Select(x => x.Key).ToArray()) planes.Remove(key);
        }
        public Snapshot Get(DateTime now)
        {
            lock (gate)
            {
                Prune(now);
                return new Snapshot {
                    Total = total, Planes = planes.Values.OrderByDescending(p => p.Seen).Select(p => p.Clone(now)).ToList(),
                    HasSignal = samples.Count > 0,
                    Mean = samples.Count == 0 ? 0 : samples.Average(x => x.Signal) / 255.0 * 100,
                    Peak = samples.Count == 0 ? 0 : samples.Max(x => x.Signal) / 255.0 * 100,
                    Rate = samples.Count / Math.Max(1, Math.Min(3, (now - begin).TotalSeconds))
                };
            }
        }
    }
    sealed class BeastMonitor : IDisposable
    {
        public readonly Metrics Metrics = new Metrics();
        CancellationTokenSource cancel;
        TcpClient client;
        readonly object gate = new object();
        Task worker;
        volatile bool connected;
        volatile string state = L.Source("s164");
        public Snapshot Get() { var s = Metrics.Get(DateTime.UtcNow); s.Connected = connected; s.State = state; return s; }
        public void Start(int port)
        {
            Stop(); Metrics.Clear();
            cancel = new CancellationTokenSource(); var token = cancel.Token;
            worker = Task.Run(async () => {
                while (!token.IsCancellationRequested)
                {
                    var current = new TcpClient();
                    lock (gate) client = current;
                    try {
                        state = L.Source("s231") + port;
                        var connection = current.ConnectAsync("127.0.0.1", port);
                        if (await Task.WhenAny(connection, Task.Delay(1500, token)) != connection) { current.Close(); await connection; }
                        await connection;
                        connected = true; state = L.Source("s025");
                        var parser = new BeastParser();
                        parser.Frame = (type, data) => Metrics.Add(type, data, DateTime.UtcNow);
                        byte[] buffer = new byte[16384];
                        using (var stream = current.GetStream()) {
                            int n;
                            while (!token.IsCancellationRequested && (n = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                                parser.Feed(buffer, n);
                        }
                    } catch (Exception) { if (!token.IsCancellationRequested) state = L.Source("s190"); }
                    finally { connected = false; current.Close(); }
                    try { await Task.Delay(1200, token); } catch (OperationCanceledException) { }
                }
            });
        }
        public void Stop()
        {
            if (cancel != null) cancel.Cancel();
            lock (gate) if (client != null) client.Close();
            if (worker != null) try { worker.Wait(2200); } catch (AggregateException) { }
            connected = false; state = L.Source("s164");
        }
        public void Dispose() { Stop(); if (cancel != null) cancel.Dispose(); }
    }
}
