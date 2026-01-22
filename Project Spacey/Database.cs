using Microsoft.Data.Sqlite;

namespace Project_Spacey
{
    internal class Database
    {
        public const string DbFile = "starwavestv.db";

        public static SqliteConnection Open()
        {
            var connection = new SqliteConnection($"Data Source={DbFile}");
            connection.Open();
            return connection;
        }

        public static void Initialize()
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Series (
  SeriesId INTEGER PRIMARY KEY AUTOINCREMENT,
  Title TEXT NOT NULL,
  Type TEXT NOT NULL,
  IsActive INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS MediaItem (
  MediaId INTEGER PRIMARY KEY AUTOINCREMENT,
  SeriesId INTEGER NOT NULL,
  SeasonNumber INTEGER,
  EpisodeNumber INTEGER,
  Title TEXT NOT NULL,
  DurationSeconds INTEGER NOT NULL,
  FilePath TEXT NOT NULL,
  IsActive INTEGER NOT NULL DEFAULT 1,
  Priority INTEGER NOT NULL DEFAULT 0,
  FOREIGN KEY (SeriesId) REFERENCES Series(SeriesId)
);

CREATE TABLE IF NOT EXISTS PlaybackHistory (
  HistoryId INTEGER PRIMARY KEY AUTOINCREMENT,
  MediaId INTEGER NOT NULL,
  ChannelId INTEGER NOT NULL,
  PlayedAt TEXT NOT NULL,
  DayKey TEXT NOT NULL,
  FOREIGN KEY (MediaId) REFERENCES MediaItem(MediaId)
);

CREATE TABLE IF NOT EXISTS ScheduleItem (
  ScheduleItemId INTEGER PRIMARY KEY AUTOINCREMENT,
  DayKey TEXT NOT NULL,
  ChannelId INTEGER NOT NULL,
  StartTime TEXT NOT NULL,
  EndTime TEXT NOT NULL,
  MediaId INTEGER NOT NULL,
  ItemType TEXT NOT NULL,
  Reason TEXT,
  FOREIGN KEY (MediaId) REFERENCES MediaItem(MediaId)
);
";
            cmd.ExecuteNonQuery();
        }
    }
}
