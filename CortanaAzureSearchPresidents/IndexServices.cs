using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.UI.Popups;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Model;
using Newtonsoft.Json;

namespace CortanaAzureSearchPresidents
{
    public class IndexServices
    {
        private string _adminkey = "XXXXXXXXXXXXXX"; // Insert your admin key here - Insérez votre clé admin ici

        private string _baseURI = "https://presidents.search.windows.net";

        private string _indexname = "presidentsindex";

        private string _fileName = "presidents.json";

        public string _serviceName = "presidents";

        public async Task CreateIndexAsync()
        {
            var searchclient = new SearchServiceClient(new SearchCredentials(_adminkey), new Uri(_baseURI));

            var index = GetIndex();

            var response = await searchclient.Indexes.CreateOrUpdateAsync(index);

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
            {
                var dialog = new MessageDialog("Error creating the index");
                await dialog.ShowAsync();
            }
        }

        public async Task ImportDataAsync()
        {
            var indexclient = new SearchIndexClient(_serviceName, _indexname, new SearchCredentials(_adminkey));

            var data = await ReadStringFromLocalFile(_fileName);

            var presidentlist = JsonConvert.DeserializeObject<List<President>>(data);

            try
            {
                // Add data to the index
                // Ajoute les données à l'index
                var response = indexclient.Documents.Index(IndexBatch.Create(presidentlist.Select(IndexAction.Create)));
            }
            catch (Exception e)
            {
                var dialog = new MessageDialog("Error importing data");
                await dialog.ShowAsync();
            }
        }
        public static async Task<string> ReadStringFromLocalFile(string filename)
        {
            var local = Package.Current.InstalledLocation;

            var stream = await local.OpenStreamForReadAsync(filename);
            string text;

            using (var reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }

            return text;
        }
        private Index GetIndex()
        {
            var index = new Index(_indexname, new List<Field>
{
    new Field("id", DataType.String) {IsKey = true},
    new Field("name", DataType.String) {IsSearchable = true},
    new Field("party", DataType.String) {IsSearchable = true},
    new Field("rawTerms", DataType.String),
    new Field("termStart", DataType.Int32) {IsFilterable = true},
    new Field("termEnd", DataType.Int32) {IsFilterable = true},
});

            return index;
        }
    }
}
