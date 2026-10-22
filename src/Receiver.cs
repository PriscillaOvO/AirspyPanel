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
    sealed class Decoder : IReceiverBackend
    {
        Process process;
        Task completion;
        volatile bool stopping;
        public event Action<string> Line;
        public event Action<int, bool> Ended;
        public bool Running { get { try { return process != null && !process.HasExited; } catch { return false; } } }
        public int Id { get { return Running ? process.Id : 0; } }
        public TimeSpan Cpu { get { try { return Running ? process.TotalProcessorTime : TimeSpan.Zero; } catch { return TimeSpan.Zero; } } }
        void Emit(string text) { var h = Line; if (h != null) h(text); }
        void Pump(StreamReader stream)
        {
            var line = new StringBuilder();
            char[] buf = new char[512];
            int count;
            try
            {
                while ((count = stream.Read(buf, 0, buf.Length)) > 0)
                    for (int i = 0; i < count; i++)
                    {
                        if (buf[i] == '\r' || buf[i] == '\n')
                        {
                            if (line.Length > 0) { Emit(line.ToString()); line.Clear(); }
                        }
                        else { line.Append(buf[i]); if (line.Length >= 8192) { Emit(line.ToString()); line.Clear(); } }
                    }
                if (line.Length > 0) Emit(line.ToString());
            }
            catch (IOException ex) { Emit(L.Source("s218") + ex.Message); }
            catch (ObjectDisposedException) { }
        }
        public void Start(string executable, string arguments)
        {
            if (Running || (completion != null && !completion.IsCompleted)) throw new InvalidOperationException(L.Source("s200"));
            if (process != null) process.Dispose();
            stopping = false;
            process = new Process();
            process.StartInfo = new ProcessStartInfo(executable, arguments) {
                WorkingDirectory = Path.GetDirectoryName(executable), UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            if (!process.Start()) throw new InvalidOperationException(L.Source("s203"));
            var current = process;
            var stdout = Task.Factory.StartNew(() => Pump(current.StandardOutput), TaskCreationOptions.LongRunning);
            var stderr = Task.Factory.StartNew(() => Pump(current.StandardError), TaskCreationOptions.LongRunning);
            completion = Task.Factory.StartNew(() => {
                current.WaitForExit();
                Task.WaitAll(stdout, stderr);
                var h = Ended;
                if (h != null) h(current.ExitCode, stopping);
            }, TaskCreationOptions.LongRunning);
        }
        public void Stop()
        {
            if (!Running) return;
            stopping = true;
            // Only the exact process started by this controller is stopped.
            // The decoder has no stdin shutdown command and is not recording files.
            try { process.Kill(); } catch (InvalidOperationException) { }
        }
        public bool Wait(int milliseconds) { return completion == null || completion.Wait(milliseconds); }
        public void Dispose() { Stop(); if (Wait(3000) && process != null) process.Dispose(); }
    }

}
