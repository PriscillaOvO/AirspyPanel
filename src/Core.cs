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
        public int Port = 30005;
        public string Format = "Beast";
        public string Gain = "auto";
        public string Sample = "自动";
        public int Filter = 4;
        public bool Packing = false;
        public string VrsPath = "";
        public string MapUrl = "http://127.0.0.1/VirtualRadar/";
        public string DecoderFolder = "";
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
                throw new ArgumentException("请选择 airspy_adsb.exe。");
            string path = Path.GetFullPath(executable.Trim());
            if (!String.Equals(Path.GetFileName(path), "airspy_adsb.exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("解码器文件名必须为 airspy_adsb.exe。");
            return Path.GetDirectoryName(path);
        }
    }

    static class Commands
    {
        public static string Build(Settings s)
        {
            if (s.Port < 1024 || s.Port > 65535) throw new ArgumentException("端口必须在 1024 至 65535 之间。");
            if (s.Format != "Beast" && s.Format != "AVR" && s.Format != "ASAVR") throw new ArgumentException("未知的数据格式。");
            int gain;
            if (s.Gain != "auto" && (!Int32.TryParse(s.Gain, out gain) || gain < 0 || gain > 21))
                throw new ArgumentException("增益必须是 auto 或 0–21。");
            if (s.Sample != "自动" && s.Sample != "12" && s.Sample != "20" && s.Sample != "24")
                throw new ArgumentException("未知的采样设置。");
            if (s.Filter < 1 || s.Filter > 60) throw new ArgumentException("过滤强度必须在 1–60 之间。");
            return "-v -l " + s.Port + ":" + s.Format.ToLowerInvariant() +
                " -R rms -g " + s.Gain + " -e " + s.Filter +
                (s.Sample == "自动" ? "" : " -m " + s.Sample) +
                (s.Packing ? " -p" : "");
        }
        public static bool PortInUse(int port)
        {
            foreach (System.Net.IPEndPoint ep in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                if (ep.Port == port) return true;
            return false;
        }
    }

    sealed class Decoder : IDisposable
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
            catch (IOException ex) { Emit("读取输出结束：" + ex.Message); }
            catch (ObjectDisposedException) { }
        }
        public void Start(string executable, string arguments)
        {
            if (Running || (completion != null && !completion.IsCompleted)) throw new InvalidOperationException("解码器仍在运行或退出中。");
            if (process != null) process.Dispose();
            stopping = false;
            process = new Process();
            process.StartInfo = new ProcessStartInfo(executable, arguments) {
                WorkingDirectory = Path.GetDirectoryName(executable), UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            if (!process.Start()) throw new InvalidOperationException("解码器未能启动。");
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
