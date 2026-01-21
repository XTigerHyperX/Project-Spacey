using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;

namespace Project_Spacey.Programmer.Core
{
    public sealed class MediaScanner
    {
        private static readonly string[] videoExtensions = [
            // Gotta fill this shit with all video extensions later

            ".mp4" , ".mkv" , ".mov" , ".avi" , ".m4v" , ".webm"
            ];

        public async Task <long>EnsureSeriesAsync(SqliteConnection connection , string title , string type)
        {
            long s = 0;
            return s;
        }
    }
}
