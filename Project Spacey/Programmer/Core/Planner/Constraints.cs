using System;
using System.Collections.Generic;
using System.Text;

namespace Project_Spacey.Programmer.Core.Planner
{
    public sealed class ConstraintState
    {
        private readonly Dictionary<long, DateTime> _lastSeriesEnd = new();
        public long? LastSeriesId { get; private set; }
        public int ConsecutiveSameSeries { get; private set; }

        public bool CanUseSeries(long seriesId, DateTime start, int avoidWithinMinutes)
        {
            if (avoidWithinMinutes <= 0) return true;
            if (!_lastSeriesEnd.TryGetValue(seriesId, out var lastEnd)) return true;
            return (start - lastEnd) >= TimeSpan.FromMinutes(avoidWithinMinutes);
        }

        public DateTime GetEarliestAllowedStart(long seriesId, DateTime desiredStart, int avoidWithinMinutes)
        {
            if (avoidWithinMinutes <= 0) return desiredStart;
            if (!_lastSeriesEnd.TryGetValue(seriesId, out var lastEnd)) return desiredStart;

            var minStart = lastEnd.AddMinutes(avoidWithinMinutes);
            return minStart > desiredStart ? minStart : desiredStart;
        }

        public bool CanUseConsecutive(long seriesId, int maxConsecutive)
        {
            if (maxConsecutive <= 0) return true;
            if (LastSeriesId == seriesId && ConsecutiveSameSeries >= maxConsecutive) return false;
            return true;
        }

        public void OnPlaced(long seriesId, DateTime end)
        {
            if (LastSeriesId == seriesId) ConsecutiveSameSeries++;
            else { LastSeriesId = seriesId; ConsecutiveSameSeries = 1; }

            _lastSeriesEnd[seriesId] = end;
        }
    }

}
