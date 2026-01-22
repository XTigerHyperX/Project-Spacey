namespace Project_Spacey.Programmer.Core.Planner
{
    public sealed class ShcedulePlan
    {
        public int ChannelId { get; init; } = 1;
        public TimeOnly DayStart { get; init; } = new TimeOnly(8, 0);
        public TimeOnly DayEnd { get; init; } = new TimeOnly(1, 0);

        // I'm Still unsure about how to go about this tbh

        public List<AnchorRule> Anchors { get; } = new();
        public List<BlockSpecf> Blocks { get; } = new();
        public List<QuotaRule> Quotas { get; } = new();

        public ShcedulePlan AddAnchor (string atHH , long seriesID)
        {
            Anchors.Add(new AnchorRule(TimeOnly.ParseExact(atHH , "HH:mm") , seriesID));
            return this;
        }

        public ShcedulePlan AddMixBlock (string name , string startHH , string endHH, int MaxConsSameSeries,int avoidSameSeriesWithinMinutes,params(long seriesID , int weight)[]pool)
        {
            var block = new BlockSpecf(name , TimeOnly.ParseExact(startHH , "HH:mm") , TimeOnly.ParseExact(endHH , "HH:mm") , MaxConsecutiveSameSeries : MaxConsSameSeries , AvoidSameSeriesWithinMinutes: avoidSameSeriesWithinMinutes);

            foreach (var (sid,w) in pool)
            {
                block.PoolWeight[sid] = Math.Max(1, w);
            }
            Blocks.Add(block);
            return this;
        }

        public ShcedulePlan AddQuota (long seriesID , int countPerDay , string? PrefferedStartHHmm= null , string? preferredEndHHmm = null , int minGapMinutes = 0)
        {
            Quotas.Add(new QuotaRule(
                SeriesId: seriesID,
                CountPerDay: countPerDay,
                PrefferedStart: PrefferedStartHHmm is null ? null : TimeOnly.ParseExact(PrefferedStartHHmm , "HH:mm"),
                PreferredEnd: preferredEndHHmm is null ? null : TimeOnly.ParseExact(preferredEndHHmm , "HH:mm"),
                MinGapMinutes: minGapMinutes
                ));
            return this;
        }

        public sealed class AnchorRule (TimeOnly at , long seriesID);
        public sealed class BlockSpecf
        {
            public string Name { get; }
            public TimeOnly Start { get; }
            public TimeOnly End { get; }
            public Dictionary<long , int> PoolWeight { get; } = new();

            public int MaxConsecutiveSameSeries { get; }
            public int AvoidSameSeriesWithinMinutes { get; }

            // probably gonna need to apply more rules later on

            public BlockSpecf(string Name , TimeOnly Start , TimeOnly End , int MaxConsecutiveSameSeries , int AvoidSameSeriesWithinMinutes)
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
            TimeOnly? PrefferedStart,
            TimeOnly? PreferredEnd,
            int MinGapMinutes
            );
    }
}
