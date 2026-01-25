using System;
using System.Linq;

namespace Project_Spacey.Programmer.Core
{
    internal static class ConsoleUi
    {
        public static void Section(string title)
        {
            var bar = new string('-', Math.Max(12, title.Length + 6));
            WriteLine(bar, ConsoleColor.DarkCyan);
            WriteLine(title.ToUpperInvariant(), ConsoleColor.Cyan);
            WriteLine(bar, ConsoleColor.DarkCyan);
        }

        public static void Info(string message) => WriteLine(message, ConsoleColor.Gray);
        public static void Success(string message) => WriteLine(message, ConsoleColor.Green);
        public static void Warn(string message) => WriteLine($"WARN: {message}", ConsoleColor.Yellow);
        public static void Error(string message) => WriteLine($"ERROR: {message}", ConsoleColor.Red);

        public static void KeyValue(string key, string value)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(key + ": ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(value);
            Console.ForegroundColor = prev;
        }

        public static void Bullet(string message)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(" • ");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(message);
            Console.ForegroundColor = prev;
        }

        public static void TableHeader(string[] columns, int[] widths)
        {
            TableRow(columns, widths, ConsoleColor.White, ConsoleColor.DarkGray);
            var line = string.Join('+', widths.Select(w => new string('-', Math.Max(3, w))));
            WriteLine(line, ConsoleColor.DarkGray);
        }

        public static void TableRow(string[] columns, int[] widths, ConsoleColor textColor = ConsoleColor.Gray, ConsoleColor separatorColor = ConsoleColor.DarkGray)
        {
            var prev = Console.ForegroundColor;
            for (int i = 0; i < columns.Length; i++)
            {
                Console.ForegroundColor = separatorColor;
                Console.Write(i == 0 ? "" : " | ");
                Console.ForegroundColor = textColor;
                var col = i < widths.Length ? PadOrTrim(columns[i] ?? string.Empty, widths[i]) : columns[i] ?? string.Empty;
                Console.Write(col);
            }
            Console.ForegroundColor = prev;
            Console.WriteLine();
        }

        public static void TableRowColored(string[] columns, int[] widths, ConsoleColor[]? columnColors, ConsoleColor separatorColor = ConsoleColor.DarkGray, ConsoleColor fallbackColor = ConsoleColor.Gray)
        {
            var prev = Console.ForegroundColor;
            for (int i = 0; i < columns.Length; i++)
            {
                Console.ForegroundColor = separatorColor;
                Console.Write(i == 0 ? "" : " | ");

                var color = (columnColors is not null && i < columnColors.Length && columnColors[i] != default)
                    ? columnColors[i]
                    : fallbackColor;
                Console.ForegroundColor = color;

                var col = i < widths.Length ? PadOrTrim(columns[i] ?? string.Empty, widths[i]) : columns[i] ?? string.Empty;
                Console.Write(col);
            }
            Console.ForegroundColor = prev;
            Console.WriteLine();
        }

        private static string PadOrTrim(string value, int width)
        {
            value ??= string.Empty;
            if (value.Length > width)
                return value.Substring(0, Math.Max(1, width - 1)) + "…";
            return value.PadRight(width);
        }

        private static void WriteLine(string message, ConsoleColor color)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = prev;
        }
    }
}
