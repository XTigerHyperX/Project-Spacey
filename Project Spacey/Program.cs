using Dapper;
using Project_Spacey;
using Project_Spacey.Programmer.Core;

Database.Initialize();

using var conn = Database.Open();

var scanner = new MediaScanner();

var seriesId = await scanner.EnsureSeriesAsync(conn, title: "One Piece", type: "Anime");

var folder = @"C:\Users\Mega-PC\Downloads\One Piece 1";

await scanner.ScanSeriesFolderAsync(conn, seriesId, folder);

Console.WriteLine("Scan complete");

var count = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM MediaItem;");
Console.WriteLine($"Total MediaItem rows: {count}");
