using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace DSHDesktop;

/// <summary>
/// dsh web 服务管理：检测、启动、按进程树停止。
/// 停止时只终止自己启动的进程树（Job Object），不再扫描所有 node.exe。
/// </summary>
public sealed class DshService : IDisposable
{
    public const int Port = 3080;
    public const string Url = "http://127.0.0.1:3080";

    private static readonly string LogDir = Path.Combine(Path.GetTempPath(), "dsh-desktop");
    private static readonly string LogFile = Path.Combine(LogDir, "dsh-web.log");
    private static readonly string ErrFile = Path.Combine(LogDir, "dsh-web.err.log");

    private IntPtr _job;
    private Process? _cmd;

    public static string LogPath => LogFile;
    public static string ErrPath => ErrFile;
    public static string LogDirectory => LogDir;

    /// <summary>本实例是否亲手启动了后端（决定"停止"是否由我们负责）。</summary>
    public bool OwnsBackend => _job != IntPtr.Zero || _cmd != null;

    // ── Job Object：把整个进程树关进作业，停止时一键终结 ──────
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateJobObject(IntPtr hJob, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    // ── 启动结果 ─────────────────────────────────────────────
    public enum ResultKind { Ok, AlreadyRunning, PortBusy, NotInstalled, Failed }

    public sealed record StartResult(ResultKind Kind, string? Detail = null)
    {
        public static StartResult Ok => new(ResultKind.Ok);
        public static StartResult AlreadyRunning => new(ResultKind.AlreadyRunning);
        public static StartResult PortBusy(string detail) => new(ResultKind.PortBusy, detail);
        public static StartResult NotInstalled => new(ResultKind.NotInstalled);
        public static StartResult Failed(string detail) => new(ResultKind.Failed, detail);
    }

    /// <summary>端口 3080 是否被监听。</summary>
    public static bool IsPortOpen()
    {
        try
        {
            using var client = new TcpClient();
            var ar = client.BeginConnect("127.0.0.1", Port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(500)) return false;
            client.EndConnect(ar);
            return client.Connected;
        }
        catch { return false; }
    }

    /// <summary>监听 3080 的进程描述（netstat -ano 找 PID → WMI 查命令行）。</summary>
    public static string? GetPortOwnerDescription()
    {
        try
        {
            var psi = new ProcessStartInfo("netstat.exe", "-ano")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.ASCII
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            foreach (string line in output.Split('\n'))
            {
                string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5 && parts[0] == "TCP" &&
                    parts[1].Contains($":{Port}") && parts[3] == "LISTENING")
                {
                    string pid = parts[4];
                    string cl = GetProcessCommandLine(pid) ?? "?";
                    return $"PID {pid}：{cl}";
                }
            }
        }
        catch { }
        return null;
    }

    private static string? GetProcessCommandLine(string pid)
    {
        if (!int.TryParse(pid, out int pidInt)) return null;
        try
        {
            using var proc = Process.GetProcessById(pidInt);
            return ReadCommandLine(proc);
        }
        catch { return null; }
    }

    // ── PEB 读取进程命令行（零依赖，替代 System.Management/WMI） ──
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr Reserved3;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr hProcess, int infoClass,
        IntPtr info, uint length, out uint returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr baseAddress,
        IntPtr buffer, int size, out IntPtr bytesRead);

    private const int ProcessBasicInformationClass = 0;

    private static string? ReadCommandLine(Process proc)
    {
        int ptrSize = IntPtr.Size;
        // x64: PEB.ProcessParameters @ 0x20, RTL_USER_PROCESS_PARAMETERS.CommandLine @ 0x70
        // x86: PEB.ProcessParameters @ 0x10, RTL_USER_PROCESS_PARAMETERS.CommandLine @ 0x40
        int paramsOffset = ptrSize == 8 ? 0x20 : 0x10;
        int cmdOffset = ptrSize == 8 ? 0x70 : 0x40;

        var pbi = new ProcessBasicInformation();
        IntPtr pbiBuf = Marshal.AllocHGlobal(Marshal.SizeOf(pbi));
        try
        {
            uint retLen;
            if (NtQueryInformationProcess(proc.Handle, ProcessBasicInformationClass, pbiBuf,
                    (uint)Marshal.SizeOf(pbi), out retLen) != 0)
                return null;
            pbi = Marshal.PtrToStructure<ProcessBasicInformation>(pbiBuf);

            IntPtr pParams = ReadPtr(proc.Handle, pbi.PebBaseAddress + paramsOffset);
            if (pParams == IntPtr.Zero) return null;

            IntPtr pCmd = pParams + cmdOffset;
            IntPtr cmdBuf = Marshal.AllocHGlobal(16); // UNICODE_STRING: Length(2)+MaximumLength(2)+pad(4)+Buffer(8)
            try
            {
                IntPtr read;
                if (!ReadProcessMemory(proc.Handle, pCmd, cmdBuf, 16, out read) || read.ToInt64() < 16)
                    return null;
                ushort len = (ushort)Marshal.ReadInt16(cmdBuf, 0);
                if (len <= 0 || len > 65534) return null;
                IntPtr buffer = Marshal.ReadIntPtr(cmdBuf, 8);

                IntPtr data = Marshal.AllocHGlobal(len);
                try
                {
                    if (!ReadProcessMemory(proc.Handle, buffer, data, len, out read) || read.ToInt64() < len)
                        return null;
                    return Marshal.PtrToStringUni(data, len / 2);
                }
                finally { Marshal.FreeHGlobal(data); }
            }
            finally { Marshal.FreeHGlobal(cmdBuf); }
        }
        finally { Marshal.FreeHGlobal(pbiBuf); }
    }

    private static IntPtr ReadPtr(IntPtr hProcess, IntPtr address)
    {
        IntPtr buf = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            IntPtr read;
            if (ReadProcessMemory(hProcess, address, buf, IntPtr.Size, out read) && read.ToInt64() == IntPtr.Size)
                return Marshal.ReadIntPtr(buf);
        }
        catch { }
        finally { Marshal.FreeHGlobal(buf); }
        return IntPtr.Zero;
    }

    /// <summary>dsh 是否已安装（遍历 PATH 找 dsh.cmd / dsh.exe / dsh.ps1）。</summary>
    public static bool IsDshInstalled()
    {
        string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                foreach (string name in new[] { "dsh.cmd", "dsh.exe", "dsh.bat", "dsh.ps1", "dsh" })
                {
                    if (File.Exists(Path.Combine(dir.Trim(), name))) return true;
                }
            }
            catch { }
        }
        return false;
    }

    /// <summary>
    /// 启动 dsh web 并等待端口就绪。已在运行（且监听方是 dsh）时直接复用。
    /// </summary>
    public async Task<StartResult> StartAsync(CancellationToken ct = default)
    {
        if (IsPortOpen())
        {
            string? owner = GetPortOwnerDescription();
            if (owner != null && owner.Contains("dsh", StringComparison.OrdinalIgnoreCase))
                return StartResult.AlreadyRunning;
            return StartResult.PortBusy(owner ?? "未知进程");
        }

        if (!IsDshInstalled())
            return StartResult.NotInstalled;

        try { Directory.CreateDirectory(LogDir); } catch { }

        // 创建 Job Object，把 cmd 及其整个后代关进去
        _job = CreateJobObject(IntPtr.Zero, null);
        if (_job == IntPtr.Zero) _job = IntPtr.Zero; // 创建失败则降级为只杀 cmd 本身

        string args = $"/c dsh web > \"{LogFile}\" 2> \"{ErrFile}\"";
        var psi = new ProcessStartInfo("cmd.exe", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        try { _cmd = Process.Start(psi); }
        catch (Exception ex) { return StartResult.Failed("无法启动 dsh：" + ex.Message); }

        if (_cmd != null && _job != IntPtr.Zero)
        {
            try { AssignProcessToJobObject(_job, _cmd.Handle); }
            catch { }
        }

        // 等待端口就绪（最多 60 秒）
        for (int i = 0; i < 120; i++)
        {
            await Task.Delay(500, ct);
            if (IsPortOpen()) return StartResult.Ok;
            try { if (_cmd != null && _cmd.HasExited) break; } catch { break; }
        }

        if (IsPortOpen()) return StartResult.Ok;
        return StartResult.Failed(ReadTail(ErrFile, 600));
    }

    /// <summary>终止自己启动的进程树。未启动或复用他人后端时什么都不做。</summary>
    public void Stop()
    {
        if (_job != IntPtr.Zero)
        {
            try { TerminateJobObject(_job, 1); } catch { }
            try { CloseHandle(_job); } catch { }
            _job = IntPtr.Zero;
            _cmd = null;
            return;
        }
        if (_cmd != null)
        {
            try { _cmd.Kill(entireProcessTree: true); } catch { }
            _cmd = null;
        }
    }

    /// <summary>读取文件尾部（UTF-8 安全，不截断多字节字符）。</summary>
    public static string ReadTail(string path, int maxChars)
    {
        try
        {
            if (!File.Exists(path)) return "";
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length <= maxChars * 4)
            {
                using var reader = new StreamReader(fs, System.Text.Encoding.UTF8);
                return reader.ReadToEnd();
            }
            int maxBytes = maxChars * 4;
            fs.Seek(-maxBytes, SeekOrigin.End);
            var buf = new byte[maxBytes];
            int read = 0;
            while (read < maxBytes)
            {
                int n = fs.Read(buf, read, maxBytes - read);
                if (n <= 0) break;
                read += n;
            }
            int start = 0;
            while (start < read && (buf[start] & 0xC0) == 0x80) start++;
            string text = System.Text.Encoding.UTF8.GetString(buf, start, read - start);
            if (text.Length > maxChars) text = text[^maxChars..];
            return text;
        }
        catch { return ""; }
    }

    public void Dispose()
    {
        if (_job != IntPtr.Zero) { try { CloseHandle(_job); } catch { } _job = IntPtr.Zero; }
        _cmd?.Dispose();
        _cmd = null;
    }
}
