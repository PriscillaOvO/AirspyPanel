using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AirspyPanel
{
    interface IReceiverBackend : IDisposable
    {
        event Action<string> Line;
        event Action<int, bool> Ended;
        bool Running { get; }
        int Id { get; }
        TimeSpan Cpu { get; }
        void Start(string executable, string arguments);
        void Stop();
        bool Wait(int milliseconds);
    }
    interface IMapService
    {
        Task<VrsStatus> Probe(string url, int receiverId);
        VrsConfiguration ReadConfiguration(int port, int receiverId);
        string Configure(int port);
        Task Close();
        bool IsRunning { get; }
        void Start(string path);
    }
    sealed class VrsService : IMapService
    {
        public Task<VrsStatus> Probe(string url, int receiverId) { return Vrs.Probe(url, receiverId); }
        public VrsConfiguration ReadConfiguration(int port, int receiverId) { return Vrs.ReadConfig(port, Vrs.ConfigPath, receiverId); }
        public string Configure(int port) { return Vrs.Prepare(port, Vrs.ConfigPath); }
        public bool IsRunning { get { using (var list = new ProcessList("VirtualRadar")) return list.Items.Length > 0; } }
        public void Start(string path) { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path) }); }
        public async Task Close()
        {
            using (var list = new ProcessList("VirtualRadar")) {
                foreach (var process in list.Items) {
                    if (process.HasExited) continue;
                    if (!process.CloseMainWindow()) {
                        if (process.HasExited) continue;
                        throw new InvalidOperationException(L.Source("s037"));
                    }
                    if (!await Task.Run(() => process.WaitForExit(6000))) throw new TimeoutException(L.Source("s033"));
                }
            }
        }
    }
    sealed class ProcessList : IDisposable
    {
        public readonly Process[] Items;
        public ProcessList(string name) { Items = Process.GetProcessesByName(name); }
        public void Dispose() { foreach (var process in Items) process.Dispose(); }
    }
}
