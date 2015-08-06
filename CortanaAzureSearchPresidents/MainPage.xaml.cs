using System;
using Windows.UI.Xaml.Controls;

namespace CortanaAzureSearchPresidents
{
    public sealed partial class MainPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private async void ImportButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ImportButton.IsEnabled = false;

            try
            {
                var service = new IndexServices();

                InfoText.Text = "Create index";

                await service.CreateIndexAsync();

                InfoText.Text = "Importing data";

                await service.ImportDataAsync();
            }
            catch (Exception ex)
            {
                
            }
            finally
            {
                ImportButton.IsEnabled = true;
                InfoText.Text = string.Empty;
            }
        }
    }
}
