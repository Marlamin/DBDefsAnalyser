using DBDefsAnalyser.Models;
using DBDefsAnalyser.Utils;
using DBDefsLib;
using Microsoft.VisualBasic.FileIO;
using System;
using System.IO;
using System.Net;

namespace DBDefsAnalyser.Services
{
    public class WoWToolsService : IDisposable
    {
        private readonly WebClientEx Client;

        public WoWToolsService()
        {
            Client = new WebClientEx()
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            };
        }

        public bool TryGetDataTable(string definition, BuildVersionPair version, out DataTable dataTable, Build build = null)
        {
            try
            {
                build ??= version.Build;

                var url = string.Format(Constants.CSVUrl, definition.ToLower(), build.ToString());
                var data = Client.DownloadString(url);

                System.Threading.Thread.Sleep(1500);

                using var stream = new StringReader(data);
                using var reader = new TextFieldParser(stream);
                reader.SetDelimiters(",");
                reader.HasFieldsEnclosedInQuotes = true;

                var result = new DataTable(build, reader.ReadFields(), version.IdField);
                while (!reader.EndOfData)
                    result.AddRow(reader.ReadFields());

                dataTable = result;
                return true;
            }
            catch (Exception)
            {
                Logger.WriteLine();
                Logger.WriteLine($"Unable to download {definition} for build {build}");
                dataTable = null;
                return false;
            }
        }

        public void Dispose() => Client.Dispose();
    }
}
