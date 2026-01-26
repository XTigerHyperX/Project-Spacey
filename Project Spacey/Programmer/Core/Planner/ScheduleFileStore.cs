using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Project_Spacey.Programmer.Core.Planner
{
    internal static class ScheduleFileStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public static string GetDefaultPath(int channelId, DateOnly day)
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "schedule-cache");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"channel-{channelId}-{day:yyyy-MM-dd}.json");
        }

        public static void Save(string filePath, DateTime windowStart, DateTime windowEnd, IEnumerable<ScheduledItem> items)
        {
            var dto = new ScheduleFileDto(
                WindowStart: windowStart,
                WindowEnd: windowEnd,
                Items: items.OrderBy(x => x.StartTime)
                    .Select(x => new ScheduledItemDto(x.StartTime, x.EndTime, x.MediaId, x.SeriesId, x.ItemType, x.Reason))
                    .ToList()
            );

            var json = JsonSerializer.Serialize(dto, JsonOptions);
            File.WriteAllText(filePath, json);
        }

        public static bool TryLoad(string filePath, out (DateTime windowStart, DateTime windowEnd, List<ScheduledItem> items) schedule)
        {
            schedule = default;
            if (!File.Exists(filePath)) return false;

            var json = File.ReadAllText(filePath);
            var dto = JsonSerializer.Deserialize<ScheduleFileDto>(json, JsonOptions);
            if (dto is null) return false;

            var items = dto.Items
                .Select(x => new ScheduledItem(x.StartTime, x.EndTime, x.MediaId, x.SeriesId, x.ItemType ?? "", x.Reason ?? ""))
                .OrderBy(x => x.StartTime)
                .ToList();

            schedule = (dto.WindowStart, dto.WindowEnd, items);
            return true;
        }

        private sealed record ScheduleFileDto(DateTime WindowStart, DateTime WindowEnd, List<ScheduledItemDto> Items);

        private sealed record ScheduledItemDto(
            DateTime StartTime,
            DateTime EndTime,
            long MediaId,
            long SeriesId,
            string ItemType,
            string Reason);
    }
}
