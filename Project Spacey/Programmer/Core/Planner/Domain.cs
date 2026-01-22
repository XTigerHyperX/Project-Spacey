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
        string Title,
        int DurationSeconds,
        string FilePath,
        bool IsActive
        );
}
