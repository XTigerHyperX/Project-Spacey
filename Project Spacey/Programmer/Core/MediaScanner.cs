using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;

namespace Project_Spacey.Programmer.Core
{
    public sealed class MediaScanner
    {
        private static readonly string[] videoExtensions = [
            // Gotta fill this shit with all video extensions later

            ".mp4" , ".mkv" , ".mov" , ".avi" , ".m4v" , ".webm"
            ];

        public async Task <long>EnsureSeriesAsync(SqliteConnection connection , string title , string type)
        {
            var existing = await connection.ExecuteScalarAsync<long?>(
                "SELECT SeriesId FROM Series WHERE Title = @Title AND Type = @Type LIMIT 1;",
                new {Title = title , Type = type}
                );
            if (existing is not null)
                return existing.Value;

            return await connection.ExecuteScalarAsync<long>(
                "INSERT INTO Series (Title, Type) VALUES (@Title, @Type); SELECT last_insert_rowid();",
                new { Title = title, Type = type }
                );
        }
        public async Task ScanSeriesFolderAsync(
            SqliteConnection connection,
            long seriesId,
            string folderPath,
            CancellationToken ct = default)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => videoExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f , StringComparer.OrdinalIgnoreCase)
                .ToList();

            Console.WriteLine($"Found {files.Count} media files in '{folderPath}'.");

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                var exists = await connection.ExecuteScalarAsync<long?>(
                    "SELECT MediaId FROM MediaItem WHERE FilePath = @FilePath LIMIT 1;",
                    new { FilePath = file }
                    );
                if (exists is not null)
                    {
                    Console.WriteLine($"Skipping existing media file: {Path.GetFileName(file)}");
                    continue;
                }
                int durationSeconds;
                try
                {
                    durationSeconds = await Ffprobe.GetDurationSecondsAsync(file, ct);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FAIL] Ffprobe : '{Path.GetFileName(file)}': {ex.Message}");
                    continue;
                }
                var title = Path.GetFileNameWithoutExtension(file);
                await connection.ExecuteAsync(@"INSERT INTO MediaItem (SeriesId , SeasonNumber, EpisodeNumber, Title, DurationSeconds, FilePath)
                    VALUES (@SeriesId, NULL, NULL, @Title, @DurationSeconds, @FilePath);",
                    new
                    {
                        SeriesId = seriesId,
                        Title = title,
                        DurationSeconds = durationSeconds,
                        FilePath = file
                    });
                Console.WriteLine($"[ADD] Added media file: '{title}' ({durationSeconds} seconds)");

            }
        }
    }
}
