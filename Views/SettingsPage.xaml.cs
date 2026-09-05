using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using WinIrcClient.Services;

namespace WinIrcClient.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly ChatSettingsService _settings;

        public SettingsPage()
        {
            InitializeComponent();
            _settings = ((App)Application.Current).Services.GetRequiredService<ChatSettingsService>();
            AutoScrollCheckBox.IsChecked = _settings.AutoScrollIncomingMessages;
        }

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void OnAutoScrollChanged(object sender, RoutedEventArgs e)
        {
            _settings.AutoScrollIncomingMessages = AutoScrollCheckBox.IsChecked == true;
        }
    }
}