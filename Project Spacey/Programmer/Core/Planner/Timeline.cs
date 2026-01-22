namespace Project_Spacey.Programmer.Core.Planner
{
    public sealed class Timeline
    {
        private readonly List<FreeSlot> _free = new();
        private readonly List<ScheduledItem> _items = new();

        public IReadOnlyList<ScheduledItem> Items => _items;
        public Timeline (DateTime start , DateTime end)
        {
            if (end <= start)throw new ArgumentException("End time must be after start time.");
            _free.Add(new FreeSlot(start, end));
        }

        public IEnumerable<FreeSlot> FreeSlots => _free;
        public bool TryPlaceAt(DateTime start, int durationSeconds, out DateTime end)
        {
            end = start.AddSeconds(durationSeconds);
            for (int i = 0; i < _free.Count; i++)
            {
                var slot = _free[i];
                if (start < slot.Start || end > slot.End) continue;
                CommitPlacement(i, start, end);
                return true;
            }
            return false;
        }
        public void Add(ScheduledItem item) => _items.Add(item);

        public bool TryPlaceEarliest(
        DateTime windowStart,
        DateTime windowEnd,
        int durationSeconds,
        Func<DateTime, bool> startPredicate,
        out DateTime start,
        out DateTime end)
        {
            start = default;
            end = default;

            for (int i = 0; i < _free.Count; i++)
            {
                var slot = _free[i];
                if (slot.End <= windowStart || slot.Start >= windowEnd) continue;

                var s = Max(slot.Start, windowStart);
                var e = s.AddSeconds(durationSeconds);
                var slotEndClamped = Min(slot.End, windowEnd);

                if (e > slotEndClamped) continue;
                if (!startPredicate(s)) continue;

                // commit using original free slot index
                CommitPlacement(i, s, e);
                start = s;
                end = e;
                return true;
            }

            return false;
        }


        private void CommitPlacement(int freeSlotIndex, DateTime start, DateTime end)
        {
            var slot = _free[freeSlotIndex];
            _free.RemoveAt(freeSlotIndex);

            if (start > slot.Start)
            {
                _free.Insert(freeSlotIndex++, new FreeSlot(slot.Start, start));
            }
            if (end < slot.End)
            {
                _free.Insert(freeSlotIndex, new FreeSlot(end, slot.End));
            }
        }
        private static DateTime Max ( DateTime a , DateTime b) => a > b ? a : b;
        private static DateTime Min ( DateTime a , DateTime b) => a < b ? a : b;

    }
    public readonly record struct FreeSlot(DateTime Start , DateTime End);
}
