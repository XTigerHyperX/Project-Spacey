using Dapper;
using Microsoft.Data.Sqlite;

public sealed class ProgressRepository
{
    public async Task<List<(int channelId, long seriesId, int nextIndex)>> LoadAsync(SqliteConnection conn)
    {
        var rows = await conn.QueryAsync<ProgressRow>(@"
SELECT ChannelId, SeriesId, NextIndex FROM SeriesProgress;");
        return rows.Select(r => (r.ChannelId, r.SeriesId, r.NextIndex)).ToList();
    }

    public async Task SaveAsync(SqliteConnection conn, IEnumerable<(int channelId, long seriesId, int nextIndex)> rows)
    {
        using var tx = conn.BeginTransaction();

        foreach (var (channelId, seriesId, nextIndex) in rows)
        {
            await conn.ExecuteAsync(@"
INSERT INTO SeriesProgress (SeriesId, ChannelId, NextIndex)
VALUES (@SeriesId, @ChannelId, @NextIndex)
ON CONFLICT(SeriesId, ChannelId) DO UPDATE SET NextIndex = excluded.NextIndex;",
                new { SeriesId = seriesId, ChannelId = channelId, NextIndex = nextIndex },
                tx);
        }

        tx.Commit();
    }

    private sealed class ProgressRow
    {
        public int ChannelId { get; init; }
        public long SeriesId { get; init; }
        public int NextIndex { get; init; }
    }
}
