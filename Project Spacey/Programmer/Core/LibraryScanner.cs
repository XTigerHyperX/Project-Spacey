using System;
using System.Collections.Generic;
using System.Text;

namespace Project_Spacey.Programmer.Core
{
    using global::Project_Spacey.Programmer.Core.Data.Project_Spacey.Programmer.Core.LibraryImport;
    using System.IO;

    namespace Project_Spacey.Programmer.Core.LibraryImport
    {
        public sealed class LibraryScanner
        {
            private static readonly HashSet<string> VideoExts = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v" };

            public ScanResult Scan(string rootPath, string ffprobePath)
            {
                if (!Directory.Exists(rootPath))
                    throw new DirectoryNotFoundException($"Root folder not found: {rootPath}");

                if (!File.Exists(ffprobePath))
                    throw new FileNotFoundException($"ffprobe not found: {ffprobePath}");

                var series = new List<SeriesImport>();
                var episodes = new List<EpisodeImport>();
                var warnings = new List<string>();

                // Each immediate subfolder is a series
                var seriesFolders = Directory.GetDirectories(rootPath);

                foreach (var folder in seriesFolders)
                {
                    var seriesTitle = new DirectoryInfo(folder).Name.Trim();
                    if (string.IsNullOrWhiteSpace(seriesTitle))
                        continue;

                    series.Add(new SeriesImport(seriesTitle, folder));

                    // Scan all files recursively inside series folder
                    var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                        .Where(f => VideoExts.Contains(Path.GetExtension(f)));

                    foreach (var file in files)
                    {
                        var fileName = Path.GetFileName(file);

                        var (season, episode) = EpisodeNameParser.ParseSeasonEpisode(fileName);

                        var duration = FfprobeDuration.GetDurationSeconds(ffprobePath, file);
                        if (duration <= 0)
                            warnings.Add($"Duration unknown for: {file}");

                        if (season is null && episode is null)
                            warnings.Add($"Could not parse episode number: {fileName}");

                        episodes.Add(new EpisodeImport(
                            SeriesTitle: seriesTitle,
                            FilePath: Path.GetFullPath(file),
                            FileName: fileName,
                            SeasonNumber: season,
                            EpisodeNumber: episode,
                            DurationSeconds: duration
                        ));
                    }
                }

                return new ScanResult(series, episodes, warnings);
            }
        }
    }

}
