using System;
using System.Collections.Generic;
using System.Text;

namespace Project_Spacey.Programmer.Core.Planner
{
    internal class Domain
    {
        
    }

    public sealed record ScheduledItem(
            DateTime StartTime,
            DateTime EndTime,
            long MediaId,
            long SeriesId,
            string ItemType,
            string Reason
            );

    public sealed record MediaItem(
    long MediaId,
    long SeriesId,
    int? SeasonNumber,
    int? EpisodeNumber,
    string Title,
    int DurationSeconds,
    string FilePath,
    bool IsActive
);

    public sealed class MediaItemRow
    {
        public long MediaId { get; set; }
        public long SeriesId { get; set; }
        public long? SeasonNumber { get; set; }
        public long? EpisodeNumber { get; set; }
        public string Title { get; set; } = "";
        public long DurationSeconds { get; set; }
        public string FilePath { get; set; } = "";
        public long IsActive { get; set; }
    }

}
