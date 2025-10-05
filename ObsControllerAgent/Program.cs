using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

static class Program
{
    // ----- Windows bring-to-front -----
    const int SW_RESTORE = 9;
    const int SW_SHOW = 5;

    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);

    static int Main(string[] args)
    {
        try
        {
            Log("=== ObsControllerAgent START ===");
            Log("Args: " + string.Join(" | ", args.Select(a => $"[{a}]")));

            // 1) Parsowanie komendy
            var cmd = ParseCommand(args);
            Log($"Parsed -> stream:{cmd.StartStream} profile:{cmd.Profile} collection:{cmd.Collection} scene:{cmd.Scene}");

            // 2) Znajdź OBS
            var resolved = ResolveObsPath(out var obsExe, out var workDir, out var why);
            Log($"ResolveObsPath -> {resolved}, exe:'{obsExe}', wd:'{workDir}', note:'{why}'");

            if (!resolved || string.IsNullOrWhiteSpace(obsExe) || !File.Exists(obsExe))
            {
                Log("OBS not found. Hints: " + why);
                Message("Nie znaleziono OBS. Sprawdź agent.log i ustaw 'obs-path.txt' lub zmienną środowiskową OBS_PATH.");
                return 2;
            }

            // 3) Jeśli OBS już działa – pokaż okno i wyjdź
            if (IsObsRunning(out var existing))
            {
                Log("OBS already running -> bring to front only.");
                BringToFront(existing!);
                return 0;
            }

            // 4) Zbuduj argumenty uruchomienia
            var argsText = BuildObsArgs(cmd);
            Log($"Starting: \"{obsExe}\" {argsText}");

            var psi = new ProcessStartInfo
            {
                FileName = obsExe,
                Arguments = argsText,
                UseShellExecute = true, // ważne na Windows
                WorkingDirectory = workDir ?? Path.GetDirectoryName(obsExe)!,
                WindowStyle = ProcessWindowStyle.Normal,
            };

            var p = Process.Start(psi);
            if (p == null)
            {
                Log("Process.Start returned null");
                return 3;
            }

            // Poczekaj aż okno będzie dostępne i wynieś na wierzch
            try
            {
                p.WaitForInputIdle(5000);
                BringToFront(p);
            }
            catch { /* best-effort */ }

            Log("OBS started OK.");
            return 0;
        }
        catch (Exception ex)
        {
            Log("FATAL: " + ex);
            Message("Wystąpił błąd. Zobacz agent.log w %APPDATA%\\ObsControllerAgent\\");
            return 1;
        }
        finally
        {
            Log("=== ObsControllerAgent EXIT ===");
        }
    }

    // ---------- Model ----------
    sealed class ObsCommand
    {
        public bool StartStream { get; set; }
        public string? Profile { get; set; }
        public string? Collection { get; set; }
        public string? Scene { get; set; }
    }

    // ---------- Parser ----------
    static ObsCommand ParseCommand(string[] args)
    {
        var cmd = new ObsCommand();
        var raw = args.FirstOrDefault() ?? "obscontroller://start";

        try
        {
            raw = raw.Trim().Trim('"');

            if (!raw.StartsWith("obscontroller:", StringComparison.OrdinalIgnoreCase))
            {
                // pozwól też na "start?stream=1"
                if (!raw.StartsWith("start", StringComparison.OrdinalIgnoreCase))
                    raw = "obscontroller://start" + (raw.StartsWith("?") ? raw : "");
                else
                    raw = "obscontroller://" + raw;
            }

            var uri = new Uri(raw);
            var q = ParseQuery(uri.Query);

            cmd.StartStream = q.TryGetValue("stream", out var v) && v == "1";
            if (q.TryGetValue("profile", out var prof)) cmd.Profile = prof;
            if (q.TryGetValue("collection", out var coll)) cmd.Collection = coll;
            if (q.TryGetValue("scene", out var sc)) cmd.Scene = sc;
        }
        catch (Exception ex)
        {
            Log("ParseCommand error: " + ex);
        }
        return cmd;
    }

    static Dictionary<string, string> ParseQuery(string query)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return d;
        var q = query[0] == '?' ? query[1..] : query;
        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            var k = Uri.UnescapeDataString(kv[0] ?? "");
            var v = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            if (!string.IsNullOrWhiteSpace(k)) d[k] = v;
        }
        return d;
    }

    static string BuildObsArgs(ObsCommand cmd)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(cmd.Profile)) list.Add($"--profile \"{cmd.Profile}\"");
        if (!string.IsNullOrWhiteSpace(cmd.Collection)) list.Add($"--collection \"{cmd.Collection}\"");
        if (!string.IsNullOrWhiteSpace(cmd.Scene)) list.Add($"--scene \"{cmd.Scene}\"");
        if (cmd.StartStream) list.Add("--startstreaming");
        return string.Join(' ', list);
    }

    // ---------- Wykrywanie OBS ----------
    static bool ResolveObsPath(out string? exe, out string? wd, out string note)
    {
        exe = null; wd = null; note = "";

        // 1) ENV nadpisuje wszystko
        var env = Environment.GetEnvironmentVariable("OBS_PATH");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        {
            exe = env; wd = Path.GetDirectoryName(env);
            note = "from ENV OBS_PATH";
            return true;
        }

        // 2) plik obs-path.txt obok agenta (można w nim wpisać pełną ścieżkę do obs64.exe)
        try
        {
            var here = AppContext.BaseDirectory;
            var hint = Path.Combine(here, "obs-path.txt");
            if (File.Exists(hint))
            {
                var p = File.ReadAllText(hint).Trim().Trim('"');
                if (File.Exists(p))
                {
                    exe = p; wd = Path.GetDirectoryName(p);
                    note = "from obs-path.txt";
                    return true;
                }
                note += $"obs-path.txt wskazuje nieistniejącą ścieżkę: {p}. ";
            }

            // 3) portable obok agenta
            var portable = Path.Combine(here, "obs-studio", "bin", "64bit", "obs64.exe");
            if (File.Exists(portable))
            {
                exe = portable; wd = Path.GetDirectoryName(portable);
                note = "portable next to agent";
                return true;
            }
        }
        catch (Exception ex) { note += "hint/portable ex: " + ex.Message + ". "; }

        // 4) standardowe lokalizacje
        string Combine(params string[] parts) => Path.Combine(parts);

        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var candidates = new[]
        {
            Combine(pf,  "obs-studio","bin","64bit","obs64.exe"),
            Combine(pf86,"obs-studio","bin","64bit","obs64.exe"),
            // czasem ktoś instaluje w niestandardowym folderze pod Program Files
            Combine(pf,  "OBS Studio","bin","64bit","obs64.exe"),
            Combine(pf86,"OBS Studio","bin","64bit","obs64.exe"),
        };

        foreach (var c in candidates)
        {
            try
            {
                if (File.Exists(c))
                {
                    exe = c; wd = Path.GetDirectoryName(c);
                    note += $"found at {c}";
                    return true;
                }
            }
            catch (Exception ex) { note += $"check {c} ex:{ex.Message}; "; }
        }

        return false;
    }

    // ---------- Running/Front ----------
    static bool IsObsRunning(out Process? p)
    {
        p = Process.GetProcessesByName("obs64").FirstOrDefault();
        return p != null;
    }

    static void BringToFront(Process p)
    {
        try
        {
            for (int i = 0; i < 25; i++)
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

    // ---------- Log & UI ----------
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
        catch { }
    }

    static void Message(string text)
    {
        try
        {
            // proste info bez zależności – Notepad
            var tmp = Path.Combine(Path.GetTempPath(), "ObsControllerAgent.msg.txt");
            File.WriteAllText(tmp, text + Environment.NewLine + "Szczegóły: %APPDATA%\\ObsControllerAgent\\agent.log");
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{tmp}\"") { UseShellExecute = true });
        }
        catch { /* nie blokuj */ }
    }
}
