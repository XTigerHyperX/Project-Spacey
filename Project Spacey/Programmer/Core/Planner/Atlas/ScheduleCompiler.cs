using Project_Spacey.Programmer.Core.Planner.Project_Spacey.Programmer.Core.Planner;

namespace Project_Spacey.Programmer.Core.Planner.Atlas
{
    public sealed class ScheduleCompiler
    {
        public sealed record Result(DateTime WindowStart, DateTime WindowEnd, List<ScheduledItem> Items);

        public Result Compile(DateOnly day, SchedulePlan plan, MediaCatalogue catalog, ProgressStore progress)
        {
            var (ws, we) = ComputeWindow(day, plan.DayStart, plan.DayEnd);
            var timeline = new Timeline(ws, we);
            var state = new ConstraintState();
            var fallbackSeries = CollectSeries(plan).Where(catalog.HasSeries).Distinct().ToList();

            foreach (var a in plan.Anchors.OrderBy(x => x.At))
            {
                var start = day.ToDateTime(a.At);
                if (start < ws || start >= we) continue;
                if (!catalog.HasSeries(a.SeriesId)) continue;

                var pick = progress.PickNext(plan.ChannelId, a.SeriesId, catalog);
                if (timeline.TryPlaceAt(start, pick.DurationSeconds, out var end))
                {
                    timeline.Add(new ScheduledItem(start, end, pick.MediaId, pick.SeriesId, "Anchor", $"Anchor {a.At:HH:mm}"));
                    progress.OnPlaced(plan.ChannelId, pick.SeriesId, catalog);
                }
            }

            foreach (var b in plan.Blocks.OrderBy(x => x.Start))
            {
                var (bs, be) = ComputeWindow(day, b.Start, b.End);
                bs = Max(bs, ws); be = Min(be, we);
                if (be <= bs) continue;

                FillMixBlock(plan, catalog, progress, timeline, state, bs, be, b);
            }

            foreach (var q in plan.Quotas)
            {
                if (!catalog.HasSeries(q.SeriesId)) continue;

                for (int i = 0; i < q.CountPerDay; i++)
                {
                    var pick = progress.PickNext(plan.ChannelId, q.SeriesId, catalog);

                    var preferred = GetPreferredWindow(day, q, ws, we);

                    var placedPreferred = TryPlaceEpisode(
                        timeline, state,
                        windowStart: preferred.start, windowEnd: preferred.end,
                        durationSeconds: pick.DurationSeconds,
                        seriesId: q.SeriesId,
                        minGapMinutes: q.MinGapMinutes,
                        maxConsecutive: int.MaxValue,
                        out var start, out var end
                    );

                    if (placedPreferred)
                    {
                        timeline.Add(new ScheduledItem(start, end, pick.MediaId, pick.SeriesId, "Quota", "Quota (preferred)"));
                        progress.OnPlaced(plan.ChannelId, pick.SeriesId, catalog);
                        continue;
                    }

                    var placedSpill = TryPlaceEpisode(
                        timeline, state,
                        windowStart: ws, windowEnd: we,
                        durationSeconds: pick.DurationSeconds,
                        seriesId: q.SeriesId,
                        minGapMinutes: q.MinGapMinutes,
                        maxConsecutive: int.MaxValue,
                        out start, out end
                    );

                    if (placedSpill)
                    {
                        timeline.Add(new ScheduledItem(start, end, pick.MediaId, pick.SeriesId, "Quota", "Quota (spilled)"));
                        progress.OnPlaced(plan.ChannelId, pick.SeriesId, catalog);
                        continue;
                    }

                    break;
                }
            }
            if (fallbackSeries.Count > 0)
            {
                FillRemaining(timeline, catalog, progress, plan.ChannelId, fallbackSeries);
            }
            var items = timeline.Items.OrderBy(i => i.StartTime).ToList();
            return new Result(ws, we, items);
        }

        private static void FillRemaining(
            Timeline timeline,
            MediaCatalogue catalog,
            ProgressStore progress,
            int channelId,
            IReadOnlyList<long> seriesPool)
        {
            if (seriesPool.Count == 0) return;

            int poolIndex = 0;

            foreach (var slot in timeline.FreeSlots.OrderBy(s => s.Start).ToList())
            {
                var cursor = slot.Start;
                while (true)
                {
                    var remaining = (int)(slot.End - cursor).TotalSeconds;
                    if (remaining <= 5) break;

                    MediaItem? pick = null;
                    long sid = 0;

                    for (int i = 0; i < seriesPool.Count; i++)
                    {
                        var candidate = seriesPool[(poolIndex + i) % seriesPool.Count];
                        if (!catalog.HasSeries(candidate)) continue;

                        var next = progress.PickNext(channelId, candidate, catalog);
                        if (next.DurationSeconds <= remaining)
                        {
                            pick = next;
                            sid = candidate;
                            poolIndex = (poolIndex + i + 1) % seriesPool.Count;
                            break;
                        }
                    }

                    if (pick is null) break;

                    if (!timeline.TryPlaceAt(cursor, pick.DurationSeconds, out var end)) break;

                    timeline.Add(new ScheduledItem(cursor, end, pick.MediaId, pick.SeriesId, "Fallback", "Fill free time"));
                    progress.OnPlaced(channelId, sid, catalog);

                    cursor = end;
                }
            }
        }

        private static IEnumerable<long> CollectSeries(SchedulePlan plan)
        {
            foreach (var a in plan.Anchors) yield return a.SeriesId;
            foreach (var b in plan.Blocks)
                foreach (var sid in b.MixPoolWeights.Keys)
                    yield return sid;
            foreach (var q in plan.Quotas) yield return q.SeriesId;
        }

        private static void FillMixBlock(
            SchedulePlan plan,
            MediaCatalogue catalog,
            ProgressStore progress,
            Timeline timeline,
            ConstraintState state,
            DateTime bs, DateTime be,
            BlockSpec block)
        {
            if (block.MixPoolWeights.Count == 0) return;

            while (true)
            {
                // Find the earliest free slot overlapping the block
                var slot = timeline.FreeSlots.FirstOrDefault(s => s.End > bs && s.Start < be);
                if (slot.End == default) break;

                var slotStart = Max(slot.Start, bs);
                var slotEnd = Min(slot.End, be);
                var remaining = (int)(slotEnd - slotStart).TotalSeconds;
                if (remaining <= 10) break;

                long chosen = WeightBasePicker.PickSeries(block.MixPoolWeights, sid =>
                {
                    if (!catalog.HasSeries(sid)) return false;
                    if (!state.CanUseConsecutive(sid, block.MaxConsecutiveSameSeries)) return false;

                    var next = progress.PickNext(plan.ChannelId, sid, catalog);
                    var startCandidate = state.GetEarliestAllowedStart(sid, slotStart, block.AvoidSameSeriesWithinMinutes);

                    if (!state.CanUseSeries(sid, startCandidate, block.AvoidSameSeriesWithinMinutes)) return false;

                    var endCandidate = startCandidate.AddSeconds(next.DurationSeconds);
                    if (endCandidate > slotEnd) return false;

                    return true;
                });

                if (chosen == 0) break;

                var pick = progress.PickNext(plan.ChannelId, chosen, catalog);
                var start = state.GetEarliestAllowedStart(pick.SeriesId, slotStart, block.AvoidSameSeriesWithinMinutes);
                var end = start.AddSeconds(pick.DurationSeconds);
                if (end > slotEnd) break;

                if (!timeline.TryPlaceAt(start, pick.DurationSeconds, out var realEnd))
                {
                    // If the exact slot is no longer free, stop this block to avoid a tight loop
                    break;
                }

                timeline.Add(new ScheduledItem(start, realEnd, pick.MediaId, pick.SeriesId, "Block", $"Block:{block.Name} Mix"));
                state.OnPlaced(pick.SeriesId, realEnd);
                progress.OnPlaced(plan.ChannelId, pick.SeriesId, catalog);
            }
        }
       
        private static bool TryPlaceEpisode(
            Timeline timeline,
            ConstraintState state,
            DateTime windowStart,
            DateTime windowEnd,
            int durationSeconds,
            long seriesId,
            int minGapMinutes,
            int maxConsecutive,
            out DateTime start,
            out DateTime end)
        {
            bool Predicate(DateTime s)
            {
                if (!state.CanUseConsecutive(seriesId, maxConsecutive)) return false;
                if (!state.CanUseSeries(seriesId, s, minGapMinutes)) return false;
                return true;
            }

            return timeline.TryPlaceEarliest(windowStart, windowEnd, durationSeconds, Predicate, out start, out end);
        }

        private static (DateTime start, DateTime end) GetPreferredWindow(DateOnly day, QuotaRule q, DateTime ws, DateTime we)
        {
            if (q.PreferredStart is null || q.PreferredEnd is null) return (ws, we);
            var (ps, pe) = ComputeWindow(day, q.PreferredEnd.Value, q.PreferredEnd.Value);
            ps = Max(ps, ws); pe = Min(pe, we);
            return (ps, pe);
        }

        private static (DateTime start, DateTime end) ComputeWindow(DateOnly day, TimeOnly start, TimeOnly end)
        {
            var s = day.ToDateTime(start);
            var eDay = end < start ? day.AddDays(1) : day;
            var e = eDay.ToDateTime(end);
            return (s, e);
        }

        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    }
}
