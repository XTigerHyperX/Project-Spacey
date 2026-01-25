using System;
using System.Collections.Generic;
using System.Text;

namespace Project_Spacey.Programmer.Core.Planner
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    namespace Project_Spacey.Programmer.Core.Planner
    {
        public sealed class ProgressStore
        {
            private readonly Dictionary<(int channelId, long seriesId), int> _nextIndex = new();

            public void LoadFrom(IEnumerable<(int channelId, long seriesId, int nextIndex)> rows)
            {
                _nextIndex.Clear();
                foreach (var r in rows)
                    _nextIndex[(r.channelId, r.seriesId)] = Math.Max(0, r.nextIndex);
            }

            public MediaItem PickNext(int channelId, long seriesId, MediaCatalogue catalog)
            {
                var list = catalog.Series(seriesId);
                if (list.Count == 0)
                    throw new InvalidOperationException($"Series {seriesId} has no media.");

                var key = (channelId, seriesId);
                _nextIndex.TryGetValue(key, out var idx);

                idx = ((idx % list.Count) + list.Count) % list.Count;

                return list[idx];
            }

            public void OnPlaced(int channelId, long seriesId, MediaCatalogue catalog)
            {
                var list = catalog.Series(seriesId);
                if (list.Count == 0) return;

                var key = (channelId, seriesId);
                _nextIndex.TryGetValue(key, out var idx);

                idx = ((idx % list.Count) + list.Count) % list.Count;
                _nextIndex[key] = (idx + 1) % list.Count;
            }

            public IEnumerable<(int channelId, long seriesId, int nextIndex)> Dump()
                => _nextIndex.Select(kv => (kv.Key.channelId, kv.Key.seriesId, kv.Value));
        }
    }
}
