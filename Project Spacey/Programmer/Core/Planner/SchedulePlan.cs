namespace Project_Spacey.Programmer.Core.Planner
{
    public sealed class SchedulePlan
    {
        public int ChannelId { get; init; } = 1;
        public TimeOnly DayStart { get; init; } = new(8, 0);
        public TimeOnly DayEnd { get; init; } = new(1, 0); // next day if < start

        public List<AnchorRule> Anchors { get; } = new();
        public List<BlockSpec> Blocks { get; } = new();
        public List<QuotaRule> Quotas { get; } = new();

        public SchedulePlan AddAnchor(string atHHmm, long seriesId)
        {
            Anchors.Add(new AnchorRule(TimeOnly.ParseExact(atHHmm, "HH:mm"), seriesId));
            return this;
        }

        public SchedulePlan AddMixBlock(string name, string startHHmm, string endHHmm,
            int maxConsecutiveSameSeries,
            int avoidSameSeriesWithinMinutes,
            params (long seriesId, int weight)[] pool)
        {
            var block = new BlockSpec(
                Name: name,
                Start: TimeOnly.ParseExact(startHHmm, "HH:mm"),
                End: TimeOnly.ParseExact(endHHmm, "HH:mm"),
                MaxConsecutiveSameSeries: maxConsecutiveSameSeries,
                AvoidSameSeriesWithinMinutes: avoidSameSeriesWithinMinutes
            );

            foreach (var (sid, w) in pool)
                block.MixPoolWeights[sid] = Math.Max(1, w);

            Blocks.Add(block);
            return this;
        }

        public SchedulePlan AddQuota(long seriesId, int countPerDay,
            string? preferredStartHHmm = null, string? preferredEndHHmm = null,
            int minGapMinutes = 0)
        {
            Quotas.Add(new QuotaRule(
                SeriesId: seriesId,
                CountPerDay: countPerDay,
                PreferredStart: preferredStartHHmm is null ? null : TimeOnly.ParseExact(preferredStartHHmm, "HH:mm"),
                PreferredEnd: preferredEndHHmm is null ? null : TimeOnly.ParseExact(preferredEndHHmm, "HH:mm"),
                MinGapMinutes: minGapMinutes
            ));
            return this;
        }
    }

    public sealed record AnchorRule(TimeOnly At, long SeriesId);

    public sealed class BlockSpec
    {
        public string Name { get; }
        public TimeOnly Start { get; }
        public TimeOnly End { get; }
        public Dictionary<long, int> MixPoolWeights { get; } = new();
        public int MaxConsecutiveSameSeries { get; }
        public int AvoidSameSeriesWithinMinutes { get; }

        public BlockSpec(string Name, TimeOnly Start, TimeOnly End, int MaxConsecutiveSameSeries, int AvoidSameSeriesWithinMinutes)
        {
            this.Name = Name;
            this.Start = Start;
            this.End = End;
            this.MaxConsecutiveSameSeries = MaxConsecutiveSameSeries;
            this.AvoidSameSeriesWithinMinutes = AvoidSameSeriesWithinMinutes;
        }
    }

    public sealed record QuotaRule(
        long SeriesId,
        int CountPerDay,
        TimeOnly? PreferredStart,
        TimeOnly? PreferredEnd,
        int MinGapMinutes
    );

}
