using Project_Spacey;
using Project_Spacey.Programmer.Core;       
using Project_Spacey.Programmer.Core.Data;      
using Project_Spacey.Programmer.Core.Planner;        
using Project_Spacey.Programmer.Core.Planner.Atlas;
using Project_Spacey.Programmer.Core.Planner.Project_Spacey.Programmer.Core.Planner;
using Project_Spacey.Programmer.Core.Playback;
using Project_Spacey.Programmer.Core.Project_Spacey.Programmer.Core.LibraryImport;

internal class Program
{
    public static async Task Main(string[] args)
    {
        ConsoleUi.Section("Startup");

        Database.Initialize();
        ConsoleUi.Success("Database initialized.");

        using var conn = Database.Open();

        ConsoleUi.KeyValue("DB Path", Path.GetFullPath(Database.DbFile));

        var ffprobePath = @"C:\Users\Mega-PC\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.0.1-full_build\bin\ffprobe.exe";
        var ffmpegPath = @"C:\Users\Mega-PC\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.0.1-full_build\bin\ffmpeg.exe"; // adjust if not on PATH
        var rootPath = @"C:\Users\Mega-PC\Downloads\Anime";
        int channelId = 1;
        var outputUrl = "udp://127.0.0.1:1234"; // FFmpeg output target
        var playerUrl = "udp://@:1234";        // VLC listen URL
        var ffplayPath = @"C:\Users\Mega-PC\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.0.1-full_build\bin\ffplay.exe";

        var dayStart = new TimeOnly(0, 0);
        var dayEnd = new TimeOnly(0, 0);


        ConsoleUi.Section("Library Scan & Import");
        if (Directory.Exists(rootPath))
        {
            var scanner = new LibraryScanner();
            var scan = scanner.Scan(rootPath, ffprobePath);

            ConsoleUi.KeyValue("Series folders", scan.Series.Count.ToString());
            ConsoleUi.KeyValue("Video files", scan.Episodes.Count.ToString());
            foreach (var w in scan.Warnings.Take(10))
                ConsoleUi.Warn(w);

            var importer = new Project_Spacey.Programmer.Core.LibraryImport.LibraryImporter();
            var report = await importer.ImportAsync(conn, scan);

            ConsoleUi.Section("Import Report");
            ConsoleUi.KeyValue("Series added", report.SeriesAdded.ToString());
            ConsoleUi.KeyValue("Episodes inserted", report.EpisodesInserted.ToString());
            ConsoleUi.KeyValue("Episodes updated", report.EpisodesUpdated.ToString());
            ConsoleUi.KeyValue("Episodes skipped", report.EpisodesSkippedNoChange.ToString());
        }
        else
        {
            ConsoleUi.Warn($"rootPath not found, skipping import: {rootPath}");
        }

        var mediaRepo = new MediaRepository();
        var progressRepo = new ProgressRepository();
        var scheduleRepo = new ScheduleRepository();
        var seriesRepo = new SeriesRepository();

        var allMedia = await mediaRepo.LoadActiveMediaAsync(conn);
        var seriesTitles = await seriesRepo.LoadTitlesAsync(conn);

        if (allMedia.Count == 0)
        {
            ConsoleUi.Error("No active media found. Add/import episodes first.");
            return;
        }

        var mediaById = allMedia.ToDictionary(m => m.MediaId);
        var catalog = new MediaCatalogue(allMedia);

        var progressRows = await progressRepo.LoadAsync(conn);
        var progress = new ProgressStore();
        progress.LoadFrom(progressRows);

        var plan = new SchedulePlan
        {
            ChannelId = channelId,
            DayStart = dayStart,
            DayEnd = dayEnd,
        };

        var seriesIdsWithMedia = allMedia
            .GroupBy(m => m.SeriesId)
            .Select(g => g.Key)
            .ToList();


        plan.AddMixBlock(
            name: "Prime Time",
            startHHmm: "21:00",
            endHHmm: "23:00",
            maxConsecutiveSameSeries: 2,
            avoidSameSeriesWithinMinutes: 20,
            pool: seriesIdsWithMedia.Select(sid => (sid, 10)).ToArray()
        );

        plan.AddMixBlock(
            name: "Daytime",
            startHHmm: "10:00",
            endHHmm: "18:00",
            maxConsecutiveSameSeries: 3,
            avoidSameSeriesWithinMinutes: 10,
            pool: seriesIdsWithMedia.Select(sid => (sid, 8)).ToArray()
        );

        plan.AddMixBlock(
            name: "Late Night",
            startHHmm: "23:00",
            endHHmm: "01:00",
            maxConsecutiveSameSeries: 4,
            avoidSameSeriesWithinMinutes: 5,
            pool: seriesIdsWithMedia.Select(sid => (sid, 6)).ToArray()
        );

        var compiler = new ScheduleCompiler();
        var day = DateOnly.FromDateTime(DateTime.Now);

        // Prefer reusing an existing schedule file for the day
        var schedulePath = ScheduleFileStore.GetDefaultPath(channelId, day);
        ScheduleCompiler.Result result;

        if (ScheduleFileStore.TryLoad(schedulePath, out var loaded))
        {
            ConsoleUi.Success($"Loaded schedule from file: {schedulePath}");
            result = new ScheduleCompiler.Result(loaded.windowStart, loaded.windowEnd, loaded.items);
        }
        else
        {
            ConsoleUi.Info("No saved schedule found. Compiling a new schedule...");
            result = compiler.Compile(day, plan, catalog, progress);
            ScheduleFileStore.Save(schedulePath, result.WindowStart, result.WindowEnd, result.Items);
            ConsoleUi.Success($"Saved schedule to file: {schedulePath}");
        }

        await scheduleRepo.SaveDayAsync(conn, channelId, day, result.Items);
        await progressRepo.SaveAsync(conn, progress.Dump());

        ConsoleUi.Section($"Schedule {day:yyyy-MM-dd}");
        ConsoleUi.KeyValue("Items", result.Items.Count.ToString());
        var widths = new[] { 11, 12, 28, 6, 18 };
        ConsoleUi.TableHeader(new[] { "Time", "Series", "Title", "Type", "Reason" }, widths);

        foreach (var it in result.Items.OrderBy(x => x.StartTime))
        {
            if (!mediaById.TryGetValue(it.MediaId, out var media))
            {
                ConsoleUi.Warn($"{it.StartTime:HH:mm}-{it.EndTime:HH:mm} | MediaId={it.MediaId} (missing)");
                continue;
            }

            seriesTitles.TryGetValue(it.SeriesId, out var seriesName);
            seriesName ??= $"Series {it.SeriesId}";

            ConsoleUi.TableRowColored(
                new[]
                {
                    $"{it.StartTime:HH:mm}-{it.EndTime:HH:mm}",
                    seriesName,
                    media.Title,
                    it.ItemType,
                    it.Reason ?? string.Empty
                },
                widths,
                new[]
                {
                    ConsoleColor.Cyan,   // time
                    ConsoleColor.Gray,   // series
                    ConsoleColor.Green,  // title
                    ConsoleColor.Gray,   // type
                    ConsoleColor.DarkYellow // reason
                },
                ConsoleColor.DarkGray,
                ConsoleColor.Gray);
        }

        // 10) Stream schedule in real-time via FFmpeg
        ConsoleUi.Section("Streaming");
        ConsoleUi.KeyValue("Output", outputUrl);
        ConsoleUi.Info("Streaming items in schedule order...");

        var playback = new PlaybackService();

        // Auto-launch ffplay to monitor the stream
        if (File.Exists(ffplayPath))
        {
            ConsoleUi.Info($"Launching ffplay: {ffplayPath}");
            playback.StartDebugPlayer(ffplayPath, "udp://127.0.0.1:1234", msg => ConsoleUi.Info(msg));
        }
        else
        {
            ConsoleUi.Warn($"ffplay not found at {ffplayPath}");
        }
        await playback.StreamScheduleAsync(
            result.Items,
            mediaById,
            ffmpegPath,
            outputUrl,
            msg => ConsoleUi.Info(msg));
    }
}
