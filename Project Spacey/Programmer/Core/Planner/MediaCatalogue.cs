namespace Project_Spacey.Programmer.Core.Planner
{
    public sealed class MediaCatalogue
    {
        private readonly Dictionary<long, List<MediaItem>> _bySeries;

        public MediaCatalogue(IEnumerable<MediaItem> items)
        {
            _bySeries = items
                .Where(x => x.IsActive)
                .GroupBy(x => x.SeriesId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ToList());
        }
        public IReadOnlyList<MediaItem> Series(long seriesID) => 
            _bySeries.TryGetValue(seriesID, out var list) ? list : Array.Empty<MediaItem>();

        public bool HasSeries(long seriesID) => _bySeries.ContainsKey(seriesID);
    }
}
