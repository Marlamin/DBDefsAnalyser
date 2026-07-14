using DBDefsAnalyser.Models;
using DBDefsAnalyser.Utils;
using DBDefsLib;
using Microsoft.VisualBasic.FileIO;
using System;
using System.IO;
using System.Net.Http;

namespace DBDefsAnalyser.Services
{
    public class CSVService : IDisposable
    {
        private readonly HttpClient Client;
        private readonly string Domain;
        private readonly string UrlFormat;

        public CSVService(string domain, string urlFormat)
        {
            Domain = domain;
            UrlFormat = urlFormat;
            Client = new();
        }

        public bool TryGetDataTable(string definition, BuildVersionPair version, out DataTable dataTable, Build build = null)
        {
            try
            {
                build ??= version.Build;

                var url = string.Format(UrlFormat, Domain, definition.ToLower(), build.ToString());
                var data = Client.GetStringAsync(url).Result;

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
            catch (Exception e)
            {
                Logger.WriteLine();
                Logger.WriteLine($"Unable to download {definition} for build {build}");
                Logger.WriteLine(e.Message);
                dataTable = null;
                return false;
            }
        }

        public void Dispose() => Client.Dispose();
    }
}
