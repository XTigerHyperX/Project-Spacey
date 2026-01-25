using Dapper;
using Microsoft.Data.Sqlite;
using Project_Spacey.Programmer.Core.Planner;

public sealed class ScheduleRepository
{
    public async Task SaveDayAsync(SqliteConnection conn, int channelId, DateOnly day, IEnumerable<ScheduledItem> items)
    {
        var dayKey = day.ToString("yyyy-MM-dd");

        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(
            "DELETE FROM ScheduleItem WHERE DayKey = @DayKey AND ChannelId = @ChannelId;",
            new { DayKey = dayKey, ChannelId = channelId }, tx);

        foreach (var it in items.OrderBy(x => x.StartTime))
        {
            await conn.ExecuteAsync(@"
INSERT INTO ScheduleItem (DayKey, ChannelId, StartTime, EndTime, MediaId, ItemType, Reason)
VALUES (@DayKey, @ChannelId, @StartTime, @EndTime, @MediaId, @ItemType, @Reason);",
                new
                {
                    DayKey = dayKey,
                    ChannelId = channelId,
                    StartTime = it.StartTime.ToString("O"),
                    EndTime = it.EndTime.ToString("O"),
                    MediaId = it.MediaId,
                    ItemType = it.ItemType,
                    Reason = it.Reason
                }, tx);
        }

        tx.Commit();
    }
}
