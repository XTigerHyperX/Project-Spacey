using System;
using System.Collections.Generic;
using System.Text;

namespace Project_Spacey.Programmer.Core.Data
{
    using Dapper;
    using Microsoft.Data.Sqlite;

    public sealed class SeriesRepository
    {
        public async Task<Dictionary<long, string>> LoadTitlesAsync(SqliteConnection conn)
        {
            var rows = await conn.QueryAsync<(long SeriesId, string Title)>(@"
SELECT SeriesId, Title FROM Series WHERE IsActive = 1;
");

            return rows.ToDictionary(r => r.SeriesId, r => r.Title);
        }
    }

}
