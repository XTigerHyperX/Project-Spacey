using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Project_Spacey.Programmer.Core
{
    public static class Ffprobe
    {
        public static async Task<int> GetDurationSecondsAsync(string filepath, CancellationToken cancellationToken = default)
        {
            var args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filepath}\"";
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start FFprobe.");
            var outputTask = p.StandardOutput.ReadToEndAsync();
            var errorTask = p.StandardError.ReadToEndAsync();

            await p.WaitForExitAsync(cancellationToken);
            var output = (await outputTask).Trim();
            var error = (await errorTask).Trim();

            if (p.ExitCode != 0)
            {
                throw new InvalidOperationException($"Ffprobe failed for '{filepath}'. Error: {error}");
            }
            if (!double.TryParse(output, NumberStyles.Float , CultureInfo.InvariantCulture , out var seconds))
            {
                throw new InvalidOperationException($"Could not parse ffprobe duration {output} for {filepath}.");
            }
            return Math.Max(1, (int)Math.Ceiling(seconds));
        }
    }
}
