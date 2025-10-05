using System.Diagnostics;
using Microsoft.Extensions.Options;
using OBSAPP1.Models;

namespace OBSAPP1.Services
{
    public class ObsService
    {
        private readonly ObsOptions _options;

        public ObsService(IOptions<ObsOptions> options)
        {
            _options = options.Value;
        }

        public bool IsRunning()
        {
            return Process.GetProcessesByName("obs64").Any();
        }

        public void Start()
        {
            if (IsRunning()) return;

            var psi = new ProcessStartInfo
            {
                FileName = _options.Path,
                Arguments = _options.Args,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(_options.Path)!
            };

            Process.Start(psi);
        }

        public void StopIfRunning()
        {
            foreach (var p in Process.GetProcessesByName("obs64"))
            {
                try { p.CloseMainWindow(); } catch { }
            }
        }
    }
}
