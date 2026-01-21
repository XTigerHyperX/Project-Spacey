using Dapper;
using Project_Spacey;

using var conn = Database.Open();

// Insert one Series
var seriesId = conn.ExecuteScalar<long>(
    "INSERT INTO Series (Title, Type) VALUES (@Title, @Type); SELECT last_insert_rowid();",
    new { Title = "My Custom Channel", Type = "Custom" }
);

Console.WriteLine($"Inserted SeriesId = {seriesId}");

// Read it back
var series = conn.QuerySingle<(long SeriesId, string Title, string Type)>(
    "SELECT SeriesId, Title, Type FROM Series WHERE SeriesId = @id",
    new { id = seriesId }
);

Console.WriteLine($"Read back: {series.Title} ({series.Type})");