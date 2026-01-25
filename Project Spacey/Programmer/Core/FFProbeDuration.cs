using System;
using System.Collections.Generic;
using System.Text;

namespace Project_Spacey.Programmer.Core
{
    using System.Diagnostics;
    using System.Globalization;

    namespace Project_Spacey.Programmer.Core.LibraryImport
    {
        public static class FfprobeDuration
        {
            /// <summary>
            /// Returns duration in seconds using ffprobe. Returns 0 if unknown.
            /// </summary>
            public static int GetDurationSeconds(string ffprobePath, string mediaPath)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = ffprobePath,
                        Arguments =
                            $"-v error -show_entries format=duration " +
                            $"-of default=noprint_wrappers=1:nokey=1 \"{mediaPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var p = Process.Start(psi);
                    if (p == null) return 0;

                    string output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(5000);

                    // ffprobe outputs seconds as floating point
                    if (double.TryParse(output, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                        return Math.Max(0, (int)Math.Round(seconds));

                    return 0;
                }
                catch
                {
                    return 0;
                }
            }
        }
    }

}
