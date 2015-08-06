using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.VoiceCommands;
using Windows.Storage;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Model;

namespace PresidentsService
{
    public sealed class PresidentsService : IBackgroundTask
    {
        private VoiceCommandServiceConnection _voiceCommandServiceConnection;
        private BackgroundTaskDeferral _serviceDeferral;
        private ResourceLoader _resourceLoader;

        private string _queryKey = "XXXXXXXXXXXXXX"; // Insert your query key here - Insérez votre clé requête ici

        private string _serviceName = "presidents";
        private string _indexName = "presidentsindex";
        private string _termSearchFilter = "termStart le {0} and termEnd gt {0}";


        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            try
            {
                _resourceLoader = ResourceLoader.GetForViewIndependentUse();
            }
            catch (Exception ex)
            {
                // todo: do something
            }
            _serviceDeferral = taskInstance.GetDeferral();

            // If cancelled, set deferal
            // Mets le déféral si annulation
            taskInstance.Canceled += (sender, reason) => _serviceDeferral?.Complete();

            // Get the details of the event that trigered the service
            // Obtient les détails de l'évenement qui à démarré le service
            var triggerDetails = taskInstance.TriggerDetails as AppServiceTriggerDetails;

            // Check if it is service name set in VCD
            // Regarde si c'est le nom du service qui est mis dans le VCD
            if (triggerDetails?.Name == "PresidentsService")
            {
                _voiceCommandServiceConnection =
                    VoiceCommandServiceConnection.FromAppServiceTriggerDetails(triggerDetails);
                // Set deferal when voice command is completed
                // Mets le deferal quand la commande vocale est terminée
                _voiceCommandServiceConnection.VoiceCommandCompleted += (sender, args) => _serviceDeferral?.Complete();
                // Get voice command
                // Obtient la commande vocale
                var voicecommand = await _voiceCommandServiceConnection.GetVoiceCommandAsync();

                switch (voicecommand.CommandName)
                {
                    case "whichPresidentYear":
                        var year = voicecommand.Properties["year"][0];
                        await SendProgressMessageAsync(string.Format(GetString(Strings.LookingYear), year));
                        await SearchPresidentForYearAsync(year);
                        break;
                    case "showTerm":
                        var president = voicecommand.Properties["president"][0];
                        await SendProgressMessageAsync(string.Format(GetString(Strings.LookingTerms), president));
                        await SearchTermOfPresidentAsync(president);
                        break;
                }
            }
        }

        private async Task SendProgressMessageAsync(string message)
        {
            var progressmessage = new VoiceCommandUserMessage();

            progressmessage.DisplayMessage =
                progressmessage.SpokenMessage = message;
            // Show progress message
            // Affiche le message de progression
            var response = VoiceCommandResponse.CreateResponse(progressmessage);

            await _voiceCommandServiceConnection.ReportProgressAsync(response);
        }

        private async Task SearchPresidentForYearAsync(string year)
        {
            int i;
            var ok = int.TryParse(year, out i);

            // Check if date between 1776 and now
            // Vérifie si la date est entre 1776 et maintenant
            if ((!ok) || (i < 1776) || (i > DateTimeOffset.Now.Year))
            {
                await SendErrorMessageAsync(string.Format(GetString(Strings.YearError), DateTime.Now.Year));
                return;
            }

            // Get search service client
            // Obtient le client pour le service Search
            var searchservice = new SearchServiceClient(_serviceName, new SearchCredentials(_queryKey));
            // Get the client for the president index
            // Obtient le client pour l'index président
            var searchclient = searchservice.Indexes.GetClient(_indexName);
            // Filter data to get only the president whose term contains the year
            // Filtre les données pour avoir le seul président dont le mandat contient l'année
            var result = await searchclient.Documents.SearchAsync<President>("*", new SearchParameters { Filter = string.Format(_termSearchFilter, year) });

            var president = result.Results.First().Document;

            var successmessage = new VoiceCommandUserMessage();

            successmessage.DisplayMessage =
                successmessage.SpokenMessage = Strings.Answer;
            // Show result
            // Affiche le résultat
            var response = VoiceCommandResponse.CreateResponse(successmessage, new[] { await GetPresidentTileAsync(president) });

            await _voiceCommandServiceConnection.ReportSuccessAsync(response);
        }

        private async Task SearchTermOfPresidentAsync(string presidentname)
        {
            // Get search service client
            // Obtient le client pour le service Search
            var searchservice = new SearchServiceClient(_serviceName, new SearchCredentials(_queryKey));
            // Get the client for the president index
            // Obtient le client pour l'index président
            var searchclient = searchservice.Indexes.GetClient(_indexName);

            // Search by name
            // Recherche sur le nom
            var result = await searchclient.Documents.SearchAsync<President>(presidentname);

            // If no results, show error
            // Si pas de résultats, on afficher une erreur
            if (result.Count == 0)
            {
                await SendErrorMessageAsync(string.Format(Strings.NoPresidentFound, presidentname));
                return;
            }

            President president;

            // If more than one result, desambiguate it
            // Si plus qu'un résultat, on demande une clarification
            if (result.Results.Count > 1)
            {
                president = await DisambiguatePresidentAsync(result.Results.Select(d => d.Document));
            }
            else
            {
                president = result.Results[0].Document;
            }


            var progressmessage = new VoiceCommandUserMessage();

            progressmessage.DisplayMessage =
                progressmessage.SpokenMessage = GetString(Strings.Answer);
            // Show result
            // Affiche le résultat
            var response = VoiceCommandResponse.CreateResponse(progressmessage, new[] { await GetPresidentTileAsync(president) });

            await _voiceCommandServiceConnection.ReportSuccessAsync(response);
        }

        private async Task<President> DisambiguatePresidentAsync(IEnumerable<President> presidents)
        {
            // Prompt and repeat promt
            // L'avis et la répétition de l'avis
            var promptmessage = new VoiceCommandUserMessage();
            promptmessage.DisplayMessage = promptmessage.SpokenMessage = GetString(Strings.DisambiguatePresidents);

            var repeatpromptmessage = new VoiceCommandUserMessage();
            promptmessage.DisplayMessage = promptmessage.SpokenMessage = GetString(Strings.DisambiguatePresidentsRepeat);

            var tilelist = new List<VoiceCommandContentTile>();

            // Get tile for each country flag
            // Genère une tuile pour chaque drapeaux
            foreach (var president in presidents)
            {
                var tile = await GetDesambiguationTileAsync(president);

                tilelist.Add(tile);
            }

            // Create a prompt response message
            // Crée un message réponse prompt (c'est a dire qui pose une question)
            var response = VoiceCommandResponse.CreateResponseForPrompt(promptmessage, repeatpromptmessage, tilelist);

            // Show and get answer from user
            // L'affiche et attend la réponse de l'utilisateur
            var result = await _voiceCommandServiceConnection.RequestDisambiguationAsync(response);

            // Return the selected item (or null)
            // Renvoie l'élément choisi (ou null)
            return result?.SelectedItem.AppContext as President;
        }

        private async Task SendErrorMessageAsync(string errortext)
        {
            var errormessage = new VoiceCommandUserMessage();

            errormessage.DisplayMessage =
                errormessage.SpokenMessage = errortext;
            // Show error message
            // Affiche le message d'erreur
            var response = VoiceCommandResponse.CreateResponse(errormessage, new List<VoiceCommandContentTile> { await GetErrorTileAsync() });

            await _voiceCommandServiceConnection.ReportFailureAsync(response);
        }

        private async Task<VoiceCommandContentTile> GetPresidentTileAsync(President president)
        {
            // Build a tile from selected country flag
            // Construit une tuile à partir du drapeau selectionné
            return new VoiceCommandContentTile
            {
                ContentTileType = VoiceCommandContentTileType.TitleWith68x68IconAndText,
                AppContext = president,
                AppLaunchArgument = "selectedid=" + president.Id,
                Title = president.Name,
                TextLine1 = president.RawTerms,
                TextLine2 = president.Party,
                Image = await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///PresidentsService/Assets/PresidentSeal.png"))
            };
        }

        private async Task<VoiceCommandContentTile> GetDesambiguationTileAsync(President president)
        {
            // Build a tile from selected country flag
            // Construit une tuile à partir du drapeau selectionné
            return new VoiceCommandContentTile
            {
                ContentTileType = VoiceCommandContentTileType.TitleOnly,
                AppContext = president,
                AppLaunchArgument = "selectedid=" + president.Id,
                Title = president.Name
            };
        }

        private async Task<VoiceCommandContentTile> GetErrorTileAsync()
        {
            // Build an error tile
            // Construit une tuile erreur
            return new VoiceCommandContentTile
            {
                ContentTileType = VoiceCommandContentTileType.TitleWith280x140IconAndText,
                Image = await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri($"ms-appx:///PresidentsService/Assets/Sad.png"))
            };
        }

        private string GetString(string key)
        {
            // Get string from resource
            // Obtient la chaine de caractère depuis la resource
            return _resourceLoader.GetString(key);
        }
    }
}
