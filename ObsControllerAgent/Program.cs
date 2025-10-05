using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

static class Program
{
    // ===== Windows bring-to-front =====
    const int SW_RESTORE = 9;
    const int SW_SHOW = 5;

    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);

    static int Main(string[] args)
    {
        try
        {
            Log("=== ObsControllerAgent start ===");
            // Obsługa zarówno bezpośredniego wywołania jak i protokołu:
            // obscontroller://start?stream=1&profile=...&collection=...&scene=...
            var uriArg = args.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(uriArg))
            {
                // fallback – uruchom samo OBS
                return StartObs(new ObsCommand { Action = "start" }) ? 0 : 2;
            }

            if (!TryParseCommand(uriArg, out var cmd))
            {
                Log($"Niepoprawny argument: {uriArg}");
                return 1;
            }

            return StartObs(cmd) ? 0 : 2;
        }
        catch (Exception ex)
        {
            Log("Błąd krytyczny: " + ex);
            return 1;
        }
        finally
        {
            Log("=== ObsControllerAgent exit ===");
        }
    }

    // ===== Model komendy =====
    sealed class ObsCommand
    {
        public string Action { get; set; } = "start";  // "start"
        public bool StartStream { get; set; }
        public string? Profile { get; set; }
        public string? Collection { get; set; }
        public string? Scene { get; set; }
    }

    // ===== Parser URI =====
    static bool TryParseCommand(string arg, out ObsCommand cmd)
    {
        cmd = new ObsCommand();

        try
        {
            // Chrome/Edge mogą przekazać cały URL, ale też bywa cytowany
            arg = arg.Trim().Trim('"');
            if (arg.StartsWith("obscontroller:", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(arg);
                var path = uri.AbsolutePath.Trim('/').ToLowerInvariant();
                var q = ParseQuery(uri.Query);

                cmd.Action = string.IsNullOrEmpty(path) ? "start" : path;
                cmd.StartStream = q.TryGetValue("stream", out var v) && v == "1";
                if (q.TryGetValue("profile", out var prof)) cmd.Profile = prof;
                if (q.TryGetValue("collection", out var coll)) cmd.Collection = coll;
                if (q.TryGetValue("scene", out var scene)) cmd.Scene = scene;

                return true;
            }

            // Gdyby ktoś podał po prostu "start" albo inne prostsze formy
            if (string.Equals(arg, "start", StringComparison.OrdinalIgnoreCase))
            {
                cmd.Action = "start";
                return true;
            }

            // Obsługa zwykłego query stringa (np. uruchomienie bez custom protocol)
            if (arg.StartsWith("start", StringComparison.OrdinalIgnoreCase))
            {
                cmd.Action = "start";
                var idx = arg.IndexOf('?');
                if (idx >= 0)
                {
                    var q = ParseQuery(arg.Substring(idx));
                    cmd.StartStream = q.TryGetValue("stream", out var v) && v == "1";
                    if (q.TryGetValue("profile", out var prof)) cmd.Profile = prof;
                    if (q.TryGetValue("collection", out var coll)) cmd.Collection = coll;
                    if (q.TryGetValue("scene", out var scene)) cmd.Scene = scene;
                }
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Log("TryParseCommand error: " + ex);
            return false;
        }
    }

    static Dictionary<string, string> ParseQuery(string query)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return d;
        var q = query[0] == '?' ? query.Substring(1) : query;
        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            var k = Uri.UnescapeDataString(kv[0] ?? "");
            var v = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            if (!string.IsNullOrWhiteSpace(k)) d[k] = v;
        }
        return d;
    }

    // ===== Uruchamianie OBS =====
    static bool StartObs(ObsCommand cmd)
    {
        try
        {
            var (fileName, workingDir, isShellExec) = ResolveObsPath();
            if (fileName == null)
            {
                Log("Nie znaleziono OBS dla tej platformy.");
                return false;
            }

            var args = BuildObsArgs(cmd);
            Log($"Start OBS: {fileName} {args}");

            // Jeśli już działa – Windows: wynieś na wierzch; mac/Linux: tylko startstream via args (WebSocket byłby lepszy)
            if (IsObsRunning(out var existing))
            {
                Log("OBS już działa.");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    BringToFront(existing!);
                }
                // Opcjonalnie można mimo to uruchomić drugi proces z --startstreaming -> OBS zignoruje
                // lub dodać WebSocket do sterowania instancją. Zostawiamy najprostszy wariant.
                return true;
            }

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = isShellExec,
                WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
                WindowStyle = ProcessWindowStyle.Normal
            };

            var p = Process.Start(psi);
            if (p == null) return false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                p.WaitForInputIdle(5000);
                BringToFront(p);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log("StartObs error: " + ex);
            return false;
        }
    }

    static string BuildObsArgs(ObsCommand cmd)
    {
        var list = new List<string>();

        // Ustal profil / kolekcję / scenę (jeśli podano)
        if (!string.IsNullOrWhiteSpace(cmd.Profile)) list.Add($"--profile \"{cmd.Profile}\"");
        if (!string.IsNullOrWhiteSpace(cmd.Collection)) list.Add($"--collection \"{cmd.Collection}\"");
        if (!string.IsNullOrWhiteSpace(cmd.Scene)) list.Add($"--scene \"{cmd.Scene}\"");

        if (cmd.Action == "start" && cmd.StartStream)
            list.Add("--startstreaming");

        return string.Join(' ', list);
    }

    static bool IsObsRunning(out Process? proc)
    {
        proc = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            proc = Process.GetProcessesByName("obs64").FirstOrDefault();
            return proc != null;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // na macOS proces to zwykle "obs"
            proc = Process.GetProcessesByName("obs").FirstOrDefault()
                ?? Process.GetProcesses().FirstOrDefault(p => p.ProcessName.Contains("obs", StringComparison.OrdinalIgnoreCase));
            return proc != null;
        }
        // Linux
        proc = Process.GetProcessesByName("obs").FirstOrDefault();
        return proc != null;
    }

    static void BringToFront(Process p)
    {
        try
        {
            for (int i = 0; i < 20; i++)
            {
                p.Refresh();
                if (p.MainWindowHandle != IntPtr.Zero) break;
                System.Threading.Thread.Sleep(150);
            }
            var h = p.MainWindowHandle;
            if (h != IntPtr.Zero)
            {
                ShowWindow(h, SW_RESTORE);
                ShowWindow(h, SW_SHOW);
                SetForegroundWindow(h);
            }
        }
        catch { /* best-effort */ }
    }

    // ===== Lokalizacja OBS per OS =====
    static (string? fileName, string? workingDir, bool useShellExecute) ResolveObsPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),     "obs-studio", "bin", "64bit", "obs64.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio", "bin", "64bit", "obs64.exe")
            };
            var found = candidates.FirstOrDefault(File.Exists);
            if (found != null)
                return (found, Path.GetDirectoryName(found), true);
            return (null, null, true);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Typowa lokalizacja z pakietu .app
            var path = "/Applications/OBS.app/Contents/MacOS/OBS";
            if (File.Exists(path))
                return (path, Path.GetDirectoryName(path), false);

            // Fallback – może jest w PATH
            return ("obs", null, false);
        }
        // Linux
        return ("obs", null, false); // zwykle dostępny w PATH
    }

    // ===== Log do pliku w katalogu użytkownika =====
    static void Log(string line)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ObsControllerAgent");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "agent.log");
            File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}", Encoding.UTF8);
        }
        catch { /* ignore */ }
    }
}
