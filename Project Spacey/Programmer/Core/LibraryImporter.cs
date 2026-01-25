using Dapper;
using Microsoft.Data.Sqlite;
using Project_Spacey.Programmer.Core.Data.Project_Spacey.Programmer.Core.LibraryImport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace Project_Spacey.Programmer.Core.LibraryImport
{
    public sealed class LibraryImporter
    {
        public sealed record ImportReport(
            int SeriesAdded,
            int EpisodesInserted,
            int EpisodesUpdated,
            int EpisodesSkippedNoChange,
            int EpisodesWithUnknownDuration,
            int EpisodesUnparsedNumber
        );

        // Tune this if ffprobe duration has small drift between scans
        private const int DurationToleranceSeconds = 3;

        public async Task<ImportReport> ImportAsync(SqliteConnection conn, ScanResult scan)
        {
            if (conn is null) throw new ArgumentNullException(nameof(conn));

            // Ensure schema supports syncing
            await EnsureSchemaAsync(conn);

            using var tx = conn.BeginTransaction();

            // 1) Load existing Series
            var existingSeries = (await conn.QueryAsync<(long SeriesId, string Title)>(
                "SELECT SeriesId, Title FROM Series;",
                transaction: tx)).ToList();

            // If DB has duplicates, keep the smallest id per title (case-insensitive)
            var seriesMap = existingSeries
                .GroupBy(x => x.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Min(x => x.SeriesId), StringComparer.OrdinalIgnoreCase);

            // 2) Upsert Series from scan
            int seriesAdded = 0;
            foreach (var s in scan.Series)
            {
                var title = (s.Title ?? string.Empty).Trim();
                if (title.Length == 0) continue;

                if (seriesMap.ContainsKey(title))
                    continue;

                var newId = await conn.ExecuteScalarAsync<long>(@"
INSERT INTO Series (Title, Type, IsActive)
VALUES (@Title, @Type, 1);
SELECT last_insert_rowid();",
                    new { Title = title, Type = "Anime" },
                    tx);

                seriesMap[title] = newId;
                seriesAdded++;
            }

            // 3) Load existing MediaItem rows for sync (enough fields to match/update)
            var existingMedia = (await conn.QueryAsync<MediaRow>(@"
SELECT MediaId, SeriesId, FilePath, Title, DurationSeconds, FileSizeBytes, IsActive
FROM MediaItem;",
                transaction: tx)).ToList();

            // Index by FilePath for fast exact match
            var byPath = existingMedia
                .Where(x => !string.IsNullOrWhiteSpace(x.FilePath))
                .GroupBy(x => x.FilePath!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Index for rename match: (SeriesId, FileSizeBytes) -> list
            var bySeriesSize = existingMedia
                .Where(x => x.FileSizeBytes.HasValue && x.FileSizeBytes.Value > 0)
                .GroupBy(x => (x.SeriesId, x.FileSizeBytes!.Value))
                .ToDictionary(g => g.Key, g => g.ToList());

            int inserted = 0;
            int updated = 0;
            int skippedNoChange = 0;
            int unknownDur = 0;
            int unparsed = 0;

            foreach (var ep in scan.Episodes)
            {
                var seriesTitle = (ep.SeriesTitle ?? string.Empty).Trim();
                if (seriesTitle.Length == 0) continue;

                if (!seriesMap.TryGetValue(seriesTitle, out var seriesId))
                    continue;

                var fullPath = Path.GetFullPath(ep.FilePath);
                var fileName = ep.FileName ?? Path.GetFileName(fullPath);

                long fileSizeBytes = 0;
                try
                {
                    fileSizeBytes = new FileInfo(fullPath).Length;
                }
                catch
                {
                    // If we cannot read file size, we can still insert/update by path only
                    fileSizeBytes = 0;
                }

                if (ep.DurationSeconds <= 0) unknownDur++;
                if (ep.SeasonNumber is null && ep.EpisodeNumber is null) unparsed++;

                // --- A) Exact match by FilePath ---
                if (byPath.TryGetValue(fullPath, out var existing))
                {
                    var newDuration = ep.DurationSeconds > 0 ? ep.DurationSeconds : existing.DurationSeconds;
                    var newSize = fileSizeBytes > 0 ? fileSizeBytes : existing.FileSizeBytes ?? 0;

                    // Check if anything actually changed
                    bool changed =
                        !string.Equals(existing.Title ?? "", fileName, StringComparison.Ordinal) ||
                        existing.DurationSeconds != newDuration ||
                        (existing.FileSizeBytes ?? 0) != newSize ||
                        existing.IsActive != 1 ||
                        existing.SeriesId != seriesId;

                    if (!changed)
                    {
                        skippedNoChange++;
                        continue;
                    }

                    await conn.ExecuteAsync(@"
UPDATE MediaItem
SET SeriesId=@SeriesId,
    Title=@Title,
    DurationSeconds=@DurationSeconds,
    FileSizeBytes=@FileSizeBytes,
    IsActive=1
WHERE MediaId=@MediaId;",
                        new
                        {
                            SeriesId = seriesId,
                            Title = fileName,
                            DurationSeconds = newDuration,
                            FileSizeBytes = (newSize > 0 ? newSize : (long?)null),
                            MediaId = existing.MediaId
                        },
                        tx);

                    existing.SeriesId = seriesId;
                    existing.Title = fileName;
                    existing.DurationSeconds = newDuration;
                    existing.FileSizeBytes = (newSize > 0 ? newSize : (long?)null);
                    existing.IsActive = 1;

                    updated++;
                    continue;
                }

                // --- B) Rename match (same series + same file size + close duration) ---
                MediaRow? renameMatch = null;
                if (fileSizeBytes > 0 && bySeriesSize.TryGetValue((seriesId, fileSizeBytes), out var candidates))
                {
                    // pick the closest duration match (within tolerance)
                    renameMatch = candidates
                        .OrderBy(c => Math.Abs(c.DurationSeconds - (ep.DurationSeconds > 0 ? ep.DurationSeconds : c.DurationSeconds)))
                        .FirstOrDefault(c =>
                        {
                            if (ep.DurationSeconds <= 0) return true; // duration unknown -> accept first candidate
                            return Math.Abs(c.DurationSeconds - ep.DurationSeconds) <= DurationToleranceSeconds;
                        });
                }

                if (renameMatch is not null)
                {
                    var newDuration = ep.DurationSeconds > 0 ? ep.DurationSeconds : renameMatch.DurationSeconds;

                    await conn.ExecuteAsync(@"
UPDATE MediaItem
SET FilePath=@FilePath,
    Title=@Title,
    DurationSeconds=@DurationSeconds,
    FileSizeBytes=@FileSizeBytes,
    IsActive=1
WHERE MediaId=@MediaId;",
                        new
                        {
                            FilePath = fullPath,
                            Title = fileName,
                            DurationSeconds = newDuration,
                            FileSizeBytes = fileSizeBytes,
                            MediaId = renameMatch.MediaId
                        },
                        tx);

                    // Update in-memory indexes:
                    // remove old path if it existed
                    if (!string.IsNullOrWhiteSpace(renameMatch.FilePath))
                        byPath.Remove(renameMatch.FilePath);

                    renameMatch.FilePath = fullPath;
                    renameMatch.Title = fileName;
                    renameMatch.DurationSeconds = newDuration;
                    renameMatch.FileSizeBytes = fileSizeBytes;
                    renameMatch.IsActive = 1;

                    byPath[fullPath] = renameMatch;

                    updated++;
                    continue;
                }

                // --- C) Insert new episode ---
                var insertDuration = ep.DurationSeconds > 0 ? ep.DurationSeconds : 1; // avoid 0 duration
                await conn.ExecuteAsync(@"
INSERT INTO MediaItem
(SeriesId, SeasonNumber, EpisodeNumber, Title, DurationSeconds, FilePath, FileSizeBytes, IsActive, Priority)
VALUES
(@SeriesId, @SeasonNumber, @EpisodeNumber, @Title, @DurationSeconds, @FilePath, @FileSizeBytes, 1, 0);",
                    new
                    {
                        SeriesId = seriesId,
                        SeasonNumber = ep.SeasonNumber,
                        EpisodeNumber = ep.EpisodeNumber,
                        Title = fileName,
                        DurationSeconds = insertDuration,
                        FilePath = fullPath,
                        FileSizeBytes = (fileSizeBytes > 0 ? fileSizeBytes : (long?)null)
                    },
                    tx);

                inserted++;
            }

            tx.Commit();

            return new ImportReport(
                SeriesAdded: seriesAdded,
                EpisodesInserted: inserted,
                EpisodesUpdated: updated,
                EpisodesSkippedNoChange: skippedNoChange,
                EpisodesWithUnknownDuration: unknownDur,
                EpisodesUnparsedNumber: unparsed
            );
        }

        // ----------------- Schema helpers -----------------

        private static async Task EnsureSchemaAsync(SqliteConnection conn)
        {
            // MediaItem.FileSizeBytes column for rename matching + indexes
            // SQLite doesn't support ALTER TABLE ... IF NOT EXISTS, so we try/catch.

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE UNIQUE INDEX IF NOT EXISTS IX_MediaItem_FilePath
ON MediaItem(FilePath);

CREATE UNIQUE INDEX IF NOT EXISTS UX_Series_Title_NoCase
ON Series(Title COLLATE NOCASE);
";
                await cmd.ExecuteNonQueryAsync();
            }

            try
            {
                await using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE MediaItem ADD COLUMN FileSizeBytes INTEGER;";
                await alter.ExecuteNonQueryAsync();
            }
            catch
            {
                // Column already exists (or table missing, but your DB init creates it)
            }

            await using (var idx = conn.CreateCommand())
            {
                idx.CommandText = @"
CREATE INDEX IF NOT EXISTS IX_MediaItem_Series_Size_Dur
ON MediaItem(SeriesId, FileSizeBytes, DurationSeconds);
";
                await idx.ExecuteNonQueryAsync();
            }
        }

        private sealed class MediaRow
        {
            public long MediaId { get; set; }
            public long SeriesId { get; set; }
            public string? FilePath { get; set; }
            public string? Title { get; set; }
            public int DurationSeconds { get; set; }
            public long? FileSizeBytes { get; set; }
            public int IsActive { get; set; }
        }
    }
}
