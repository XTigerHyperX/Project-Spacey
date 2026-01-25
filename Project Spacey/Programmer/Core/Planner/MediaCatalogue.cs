using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Project_Spacey.Programmer.Core.Planner
{
    public sealed class MediaCatalogue
    {
        private readonly Dictionary<long, List<MediaItem>> _bySeries;

        private static readonly Regex OnePaceIndex = new(@"\[[^\]]+\]\[(?<a>\d{1,4})(?:-(?<b>\d{1,4}))?\]",
            RegexOptions.Compiled);

        public MediaCatalogue(IEnumerable<MediaItem> items)
        {
            _bySeries = items
                .Where(x => x.IsActive)
                .GroupBy(x => x.SeriesId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderBy(x => GetSortKey(x.Title))
                        .ThenBy(x => x.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                );
        }

        public bool HasSeries(long seriesId) => _bySeries.ContainsKey(seriesId);

        public IReadOnlyList<MediaItem> Series(long seriesId)
            => _bySeries.TryGetValue(seriesId, out var list) ? list : Array.Empty<MediaItem>();

        private static int GetSortKey(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return int.MaxValue;

            var m = OnePaceIndex.Match(title);
            if (m.Success && int.TryParse(m.Groups["a"].Value, out var a))
                return a;

            var num = Regex.Match(title, @"\b(?<n>\d{1,4})\b");
            if (num.Success && int.TryParse(num.Groups["n"].Value, out var n))
                return n;

            return int.MaxValue;
        }
    }
}
