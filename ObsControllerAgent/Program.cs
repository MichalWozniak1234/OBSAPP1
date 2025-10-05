using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Text;

internal static class Program
{
    // --- WinAPI do pracy z oknem ---
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    private static int Main(string[] args)
    {
        try
        {
            Log("=== ObsControllerAgent start ===");
            Log("Args: " + string.Join(" ", args));

            // 1) Parsowanie URL-a protokołu
            var uriArg = args.Length > 0 ? args[0] : "obscontroller://start";
            var uri = new Uri(uriArg);
            var action = uri.AbsolutePath.Trim('/').ToLowerInvariant(); // "start"
            var q = ParseQuery(uri.Query);

            // 2) Znajdź OBS
            var obsPath = TryFindObsPath();
            if (string.IsNullOrEmpty(obsPath) || !File.Exists(obsPath))
            {
                Log("ERROR: Nie znaleziono obs64.exe");
                MessageBox("Nie znaleziono pliku:\n" + (obsPath ?? "(brak)") +
                           "\n\nZainstaluj OBS w domyślnej lokalizacji lub zaktualizuj ścieżkę w agencie.",
                           "ObsControllerAgent");
                return 2;
            }
            Log("OBS: " + obsPath);

            // 3) Wyłącz chowanie do traya w konfiguracji OBS (global.ini)
            DisableObsTray();

            // 4) Zbuduj argumenty
            var argsList = new List<string>();
            if (q.TryGetValue("profile", out var profile) && !string.IsNullOrWhiteSpace(profile))
                argsList.Add($"--profile \"{profile}\"");
            if (q.TryGetValue("collection", out var collection) && !string.IsNullOrWhiteSpace(collection))
                argsList.Add($"--collection \"{collection}\"");
            if (q.TryGetValue("scene", out var scene) && !string.IsNullOrWhiteSpace(scene))
                argsList.Add($"--scene \"{scene}\"");

            var startStream = q.TryGetValue("stream", out var v) && v == "1";
            if (action == "start" && startStream) argsList.Add("--startstreaming");
            // Uwaga: celowo NIE dodajemy --minimize-to-tray

            // 5) Uruchom / przywróć OBS
            var existing = Process.GetProcessesByName("obs64").FirstOrDefault();
            if (existing == null)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = obsPath,
                    Arguments = string.Join(" ", argsList),
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(obsPath)!,
                    WindowStyle = ProcessWindowStyle.Normal
                };
                Log($"Start: \"{psi.FileName}\" {psi.Arguments}");
                var p = Process.Start(psi);

                if (p != null)
                {
                    // Daj OBS chwilę na utworzenie okna i przywróć na wierzch
                    p.WaitForInputIdle(5000);
                    BringToFront(p);
                }
            }
            else
            {
                Log("OBS już działa – przywracam okno.");
                BringToFront(existing);
                // Jeśli chcesz wymuszać StartStream na działającym OBS,
                // najlepiej użyć OBS WebSocket (nie robimy tego tu).
            }

            Log("OK");
            return 0;
        }
        catch (Exception ex)
        {
            Log("EXCEPTION: " + ex);
            MessageBox(ex.ToString(), "ObsControllerAgent – błąd");
            return 1;
        }
    }

    // --- Helpers ---

    private static void BringToFront(Process p)
    {
        // Próba zdobycia uchwytu okna (OBS czasem tworzy je z opóźnieniem)
        for (int i = 0; i < 30; i++)
        {
            p.Refresh();
            if (p.MainWindowHandle != IntPtr.Zero) break;
            Thread.Sleep(150);
        }

        var h = p.MainWindowHandle;
        if (h != IntPtr.Zero)
        {
            ShowWindow(h, SW_RESTORE);
            ShowWindow(h, SW_SHOW);
            SetForegroundWindow(h);
        }
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return dict;
        var q = query[0] == '?' ? query.Substring(1) : query;
        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0] ?? "");
            var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            if (!string.IsNullOrWhiteSpace(key)) dict[key] = val;
        }
        return dict;
    }

    private static string? TryFindObsPath()
    {
        // Typowe lokalizacje
        var candidates = new[]
        {
            @"C:\Program Files\obs-studio\bin\64bit\obs64.exe",
            @"C:\Program Files (x86)\obs-studio\bin\64bit\obs64.exe"
        };
        var found = candidates.FirstOrDefault(File.Exists);
        if (!string.IsNullOrEmpty(found)) return found;

        // Próba z rejestru (InstallPath)
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\OBS Studio");
            var install = key?.GetValue("InstallPath") as string;
            if (!string.IsNullOrEmpty(install))
            {
                var p = Path.Combine(install, "bin", "64bit", "obs64.exe");
                if (File.Exists(p)) return p;
            }
        }
        catch { /* ignore */ }

        return candidates[0]; // domyślna ścieżka (sprawdzana wyżej)
    }

    private static void DisableObsTray()
    {
        try
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "obs-studio",
                "global.ini"
            );
            if (!File.Exists(configPath)) { Log("global.ini not found"); return; }

            var lines = File.ReadAllLines(configPath).ToList();
            bool changed = false;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith("EnableSystemTray=", StringComparison.OrdinalIgnoreCase))
                {
                    if (!lines[i].EndsWith("false", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = "EnableSystemTray=false";
                        changed = true;
                    }
                }
                if (lines[i].StartsWith("MinimizeToSystemTray=", StringComparison.OrdinalIgnoreCase))
                {
                    if (!lines[i].EndsWith("false", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = "MinimizeToSystemTray=false";
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                File.WriteAllLines(configPath, lines);
                Log("global.ini updated (tray disabled).");
                Thread.Sleep(100);
            }
        }
        catch (Exception ex)
        {
            Log("DisableObsTray error: " + ex.Message);
        }
    }

    private static void Log(string line)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ObsController");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "agent.log");
            File.AppendAllText(path, DateTime.Now.ToString("s") + " " + line + Environment.NewLine, Encoding.UTF8);
        }
        catch { /* ignore logging failures */ }
    }

    private static void MessageBox(string text, string title)
    {
        try
        {
            // prosta informacja użytkownikowi (bez zależności od WinForms/WPF)
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoLogo -NoProfile -WindowStyle Hidden -Command \"Add-Type -AssemblyName PresentationFramework;[System.Windows.MessageBox]::Show('{EscapePS(text)}','{EscapePS(title)}')\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { /* ignore */ }
    }

    private static string EscapePS(string s) => s.Replace("'", "''");
}
