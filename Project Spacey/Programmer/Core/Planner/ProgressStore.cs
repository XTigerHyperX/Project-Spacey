using System;
using System.Collections.Generic;
using System.Text;

namespace Project_Spacey.Programmer.Core.Planner
{
    public sealed class ProgressStore
    {
        private readonly Dictionary<(int channelId , long seriesId) , int> _nextIndex = new();
        public void LoadFrom (IEnumerable<(int channelId , long seriesId , int nextIndex)> rows)
        {
            _nextIndex.Clear();
           foreach(var r in rows)
            {
                _nextIndex[(r.channelId, r.seriesId)] = Math.Max(0, r.nextIndex);
            }
        }

        public IEnumerable<(int channelId , long seriesId , int nextIndex)> Dump() => _nextIndex.Select(kv => (kv.Key.channelId , kv.Key.seriesId , kv.Value));
        public MediaItem PickNext (int channelId , long seriesId , MediaCatalogue catalog)
        {
            var list = catalog.Series(seriesId);
            if (list.Count == 0)
                throw new InvalidOperationException($"No media items for series {seriesId}");

            var key = (channelId, seriesId);
            var index = _nextIndex.TryGetValue(key, out var v) ? v : 0;
            if (index >= list.Count) index = 0;

            var pick = list[index];
            var next = index + 1;
            if (next >= list.Count) next = 0;
            _nextIndex[key] = next;
            return pick;
        }

        public int PeekNextDuration(int channelId , long seriesId, MediaCatalogue catalog)
        {
            var list = catalog.Series(seriesId);
            if (list.Count == 0)
                return int.MaxValue;

            var index = _nextIndex.TryGetValue((channelId, seriesId), out var v) ? v : 0;
            if (index >= list.Count) index = 0;
            return list[index].DurationSeconds;
        }

    }
}
