using System.Diagnostics;

namespace Project_Spacey.Programmer.Core.Playback
{
    internal static class PlayerLauncher
    {
        public static void Launch(string playerPath, string streamUrl, string? extraArgs = null)
        {
            if (string.IsNullOrWhiteSpace(playerPath) || string.IsNullOrWhiteSpace(streamUrl)) return;

            var args = string.IsNullOrWhiteSpace(extraArgs)
                ? streamUrl
                : extraArgs + " " + streamUrl;

            var psi = new ProcessStartInfo
            {
                FileName = playerPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            try
            {
                Process.Start(psi);
            }
            catch
            {
                // Swallow: player missing or launch failed. Caller can log separately.
            }
        }
    }
}
