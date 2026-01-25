using Dapper;
using Microsoft.Data.Sqlite;
using Project_Spacey.Programmer.Core.Planner;

public sealed class MediaRepository
{
    public async Task<List<MediaItem>> LoadActiveMediaAsync(SqliteConnection conn)
    {
        var rows = await conn.QueryAsync<MediaItemRow>(@"
SELECT MediaId, SeriesId, SeasonNumber, EpisodeNumber, Title, DurationSeconds, FilePath, IsActive
FROM MediaItem
WHERE IsActive = 1;");

        return rows.Select(r => new MediaItem(
            r.MediaId,
            r.SeriesId,
            r.SeasonNumber.HasValue ? (int)r.SeasonNumber.Value : null,
            r.EpisodeNumber.HasValue ? (int)r.EpisodeNumber.Value : null,
            r.Title,
            checked((int)r.DurationSeconds),
            r.FilePath,
            r.IsActive == 1
        )).ToList();
    }

}
