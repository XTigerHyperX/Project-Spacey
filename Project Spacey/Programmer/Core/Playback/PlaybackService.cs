using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Project_Spacey.Programmer.Core.Planner;

namespace Project_Spacey.Programmer.Core.Playback
{
    internal sealed class PlaybackService
    {
        // Default if caller doesn't provide one
        public const string DefaultOutputUrl = "udp://127.0.0.1:1234?pkt_size=1316&overrun_nonfatal=1&fifo_size=5000000";

        /// <summary>
        /// Remux (no re-encode). Usually the smoothest if the MP4 already contains H264/AAC.
        /// </summary>
        public static string BuildFfmpegArgs_Remux(string inputPath, string outputUrl)
        {
            // For MP4 -> MPEG-TS, we often need H264 bitstream filter.
            // If the input isn't H264, ffmpeg will fail; we catch and fallback to transcode.
            return new StringBuilder()
                .Append("-hide_banner -loglevel warning ")
                .Append("-re ")
                .Append($"-i \"{inputPath}\" ")
                // map first video, first audio if present; ignore missing audio
                .Append("-map 0:v:0 -map 0:a:0? ")
                // copy streams
                .Append("-c copy ")
                // MP4 (AVCC) -> TS (AnnexB) for H264
                .Append("-bsf:v h264_mp4toannexb ")
                // low-latency TS muxing
                .Append("-f mpegts -muxdelay 0 -muxpreload 0 ")
                .Append($"\"{outputUrl}\"")
                .ToString();
        }

        private static string BuildFfmpegArgs_Transcode(string inputPath, string outputUrl, TimeSpan startOffset)
        {
            // For live schedule alignment: if we're late, seek into the file so the visible content matches the timeline.
            // -ss before -i = fast seek; good enough for MP4.
            var ss = startOffset <= TimeSpan.Zero
                ? string.Empty
                : $"-ss {startOffset.TotalSeconds:0.###} ";

            return new StringBuilder()
                .Append("-hide_banner -loglevel warning ")
                .Append("-re ")
                .Append("-fflags +genpts ")
                .Append(ss)
                .Append($"-i \"{inputPath}\" ")

                .Append("-map 0:v:0 -map 0:a:0? ")
                .Append("-fps_mode cfr -r 30 ")

                .Append("-c:v libx264 -preset ultrafast -tune zerolatency -pix_fmt yuv420p ")
                .Append("-bf 0 ")
                .Append("-g 30 -keyint_min 30 -sc_threshold 0 ")
                .Append("-x264-params \"repeat-headers=1\" ")

                .Append("-b:v 2500k -maxrate 2500k -bufsize 1000k ")
                .Append("-c:a aac -b:a 128k -ar 48000 ")

                .Append("-f mpegts -mpegts_flags +resend_headers+pat_pmt_at_frames ")
                .Append("-muxdelay 0 -muxpreload 0 ")
                .Append($"\"{outputUrl}\"")
                .ToString();
        }

        /// <summary>
        /// Transcode fallback. More CPU, but works for almost any input.
        /// Designed to be stable (less stutter), still low-latency.
        /// </summary>
        public static string BuildFfmpegArgs_Transcode(string inputPath, string outputUrl)
        {
            return new StringBuilder()
                .Append("-hide_banner -loglevel warning ")
                .Append("-re ")
                .Append("-fflags +genpts ")
                .Append($"-i \"{inputPath}\" ")

                .Append("-map 0:v:0 -map 0:a:0? ")

                .Append("-fps_mode cfr -r 30 ")

                .Append("-c:v libx264 -preset ultrafast -tune zerolatency -pix_fmt yuv420p ")
                .Append("-bf 0 ")
                .Append("-g 30 -keyint_min 30 -sc_threshold 0 ")
                .Append("-x264-params \"repeat-headers=1\" ")

                .Append("-b:v 2500k -maxrate 2500k -bufsize 1000k ")

                .Append("-c:a aac -b:a 128k -ar 48000 ")

                .Append("-f mpegts -mpegts_flags +resend_headers+pat_pmt_at_frames ")
                .Append("-muxdelay 0 -muxpreload 0 ")

                .Append($"\"{outputUrl}\"")
                .ToString();
        }


        private static ProcessStartInfo CreatePsi(string ffmpegPath, string args)
        {
            return new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        private static async Task<int> RunFfmpegAsync(
            string ffmpegPath,
            string args,
            TimeSpan maxDuration,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            using var process = Process.Start(CreatePsi(ffmpegPath, args));
            if (process is null)
                throw new InvalidOperationException("Failed to start ffmpeg process.");

            var startedAt = DateTime.UtcNow;

            var stderrTask = Task.Run(async () =>
            {
                try
                {
                    while (!process.HasExited && !cancellationToken.IsCancellationRequested)
                    {
                        var line = await process.StandardError.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) break;
                        log?.Invoke("[ffmpeg] " + line);
                    }
                }
                catch (Exception ex)
                {
                    log?.Invoke("[ffmpeg stderr error] " + ex.Message);
                }
            }, cancellationToken);

            var waitTask = process.WaitForExitAsync(cancellationToken);
            var stopTask = Task.Delay(maxDuration, cancellationToken);

            var completed = await Task.WhenAny(waitTask, stopTask).ConfigureAwait(false);

            if (completed == stopTask && !process.HasExited)
            {
                try
                {
                    log?.Invoke("[stream] stopping ffmpeg at block end");
                    process.Kill(entireProcessTree: true);
                }
                catch {}
            }

            await Task.WhenAny(stderrTask, Task.Delay(300, cancellationToken)).ConfigureAwait(false);

            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            }

            var ranFor = DateTime.UtcNow - startedAt;
            if (ranFor < TimeSpan.FromSeconds(1))
                log?.Invoke($"[warn] ffmpeg ran very briefly ({ranFor:g}). Args might be invalid.");

            return process.ExitCode;
        }

        /// <summary>
        /// Streams one scheduled item. Tries remux first, then falls back to transcode if remux fails.
        /// </summary>
        public async Task StartStreamingAsync(
    ScheduledItem item,
    MediaItem media,
    string ffmpegPath,
    string? outputUrl,
    Action<string>? log,
    CancellationToken cancellationToken = default)
        {
            outputUrl ??= DefaultOutputUrl;

            var now = DateTimeOffset.Now;
            var startOffset = now > item.StartTime ? (now - item.StartTime) : TimeSpan.Zero;
            var maxDuration = item.EndTime - now;
            if (maxDuration <= TimeSpan.Zero)
                maxDuration = TimeSpan.FromSeconds(Math.Max(1, media.DurationSeconds));

            if (startOffset > TimeSpan.Zero)
                log?.Invoke($"[stream] late by {startOffset:g}; seeking into file");

            log?.Invoke($"[stream] {media.Title} -> {outputUrl} (max {maxDuration:g})");
            log?.Invoke("[stream] mode: TRANSCODE (x264 ultrafast)");

            var args = BuildFfmpegArgs_Transcode(media.FilePath, outputUrl, startOffset);

            var exit = await RunFfmpegAsync(ffmpegPath, args, maxDuration, log, cancellationToken)
                .ConfigureAwait(false);

            if (exit != 0) log?.Invoke($"[error] ffmpeg exited with code {exit}");
            else log?.Invoke("[stream] completed");
        }


        public async Task StreamScheduleAsync(
            IEnumerable<ScheduledItem> schedule,
            IReadOnlyDictionary<long, MediaItem> mediaById,
            string ffmpegPath,
            string? outputUrl,
            Action<string>? log,
            CancellationToken cancellationToken = default)
        {
            outputUrl ??= DefaultOutputUrl;

            foreach (var item in schedule.OrderBy(x => x.StartTime))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!mediaById.TryGetValue(item.MediaId, out var media))
                {
                    log?.Invoke($"[skip] MediaId {item.MediaId} not found");
                    continue;
                }

                var now = DateTime.Now;

                if (item.EndTime <= now)
                {
                    log?.Invoke($"[skip] {media.Title} window ended {item.EndTime:HH:mm:ss}");
                    continue;
                }

                if (item.StartTime > now)
                {
                    var delay = item.StartTime - now;
                    log?.Invoke($"[wait] {delay:g} for {media.Title}");
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                // Stream item
                await StartStreamingAsync(item, media, ffmpegPath, outputUrl, log, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public Task SmokeTestAsync(
            string sampleFilePath,
            string ffmpegPath,
            string? outputUrl,
            Action<string>? log,
            CancellationToken cancellationToken = default)
        {
            outputUrl ??= DefaultOutputUrl;

            log?.Invoke("[smoke] Starting 20s test stream");
            var item = new ScheduledItem(DateTime.Now, DateTime.Now.AddSeconds(20), 0, 0, "Smoke", "Test");
            var media = new MediaItem(0, 0, null, null, Path.GetFileName(sampleFilePath), 20, sampleFilePath, true);

            return StartStreamingAsync(item, media, ffmpegPath, outputUrl, log, cancellationToken);
        }

        /// <summary>
        /// Launch ffplay for local debug viewing. Non-blocking, no redirects.
        /// </summary>
        public void StartDebugPlayer(string ffplayPath, string streamUrl, Action<string>? log)
        {
            if (string.IsNullOrWhiteSpace(ffplayPath) || string.IsNullOrWhiteSpace(streamUrl))
            {
                log?.Invoke("[ffplay] missing path or url");
                return;
            }

            // IMPORTANT:
            // - remove -an (you want audio)
            // - quote the URL
            // - add -sync ext to reduce clock weirdness with live streams
            var args = $"-fflags nobuffer -flags low_delay -framedrop -sync ext \"{streamUrl}\"";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffplayPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = false,
                };

                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                proc.Exited += (_, __) => log?.Invoke("[ffplay] exited");

                if (proc.Start()) log?.Invoke("[ffplay] started");
                else log?.Invoke("[ffplay] failed to start");
            }
            catch (Exception ex)
            {
                log?.Invoke("[ffplay] error: " + ex.Message);
            }
        }

    }
}
