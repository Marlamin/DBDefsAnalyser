using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DBDefsAnalyser.Utils
{
    public static class Logger
    {
        private const string LogPath = "Log.txt";
        private readonly static StringBuilder LogData = new StringBuilder();

        public static void WriteLine()
        {
            Console.WriteLine();
            LogData.AppendLine();
        }

        public static void WriteLine(string str)
        {
            Console.WriteLine(str);
            LogData.AppendLine(str);
        }

        public static void Write(string str)
        {
            Console.Write(str);
            LogData.Append(str);

        }

        public static void Save(bool Append = false)
        {
            if (LogData.Length > 0)
            {
                using StreamWriter streamwriter = Append ? File.AppendText(LogPath) : File.CreateText(LogPath);
                streamwriter.WriteLine($"[{DateTime.Now}]");
                streamwriter.Write(LogData);
                streamwriter.WriteLine();
                streamwriter.Flush();
            }
        }
    }
}
