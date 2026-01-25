namespace Project_Spacey.Programmer.Core.Data
{
    using System.Text.RegularExpressions;

    namespace Project_Spacey.Programmer.Core.LibraryImport
    {
        public sealed record SeriesImport(string Title, string FolderPath);

        public sealed record EpisodeImport(
            string SeriesTitle,
            string FilePath,
            string FileName,
            int? SeasonNumber,
            int? EpisodeNumber,
            int DurationSeconds
        );

        public sealed record ScanResult(
            List<SeriesImport> Series,
            List<EpisodeImport> Episodes,
            List<string> Warnings
        );

        public static class EpisodeNameParser
        {
            // Order matters: more specific first.
            private static readonly Regex[] Patterns = new[]
            {
            // S01E03, s1e3
            new Regex(@"\bS(?<s>\d{1,2})\s*E(?<e>\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // 1x03
            new Regex(@"\b(?<s>\d{1,2})\s*[xX]\s*(?<e>\d{1,3})\b", RegexOptions.Compiled),

            // Ep 03, Episode 03, E03 (season unknown)
            new Regex(@"\b(?:EP|EPISODE|E)\s*(?<e>\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

            public static (int? season, int? episode) ParseSeasonEpisode(string fileName)
            {
                foreach (var rx in Patterns)
                {
                    var m = rx.Match(fileName);
                    if (!m.Success) continue;

                    int? s = null;
                    if (m.Groups["s"]?.Success == true && int.TryParse(m.Groups["s"].Value, out var sVal))
                        s = sVal;

                    if (m.Groups["e"]?.Success == true && int.TryParse(m.Groups["e"].Value, out var eVal))
                        return (s, eVal);
                }

                return (null, null);
            }
        }
    }

}
