using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using WinIrcClient.Services;

namespace WinIrcClient.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly ChatSettingsService _settings;
        private readonly MessageHistoryService _history;
        private readonly IrcClientService _ircService;
        private readonly RememberedUserService _rememberedUserService;

        public SettingsPage()
        {
            InitializeComponent();
            var services = ((App)Application.Current).Services;
            _settings = services.GetRequiredService<ChatSettingsService>();
            _history = services.GetRequiredService<MessageHistoryService>();
            _ircService = services.GetRequiredService<IrcClientService>();
            _rememberedUserService = services.GetRequiredService<RememberedUserService>();
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

        private async void OnDeleteHistoryClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Delete all chat history?",
                Content = "This will permanently remove all messages from the local database. This action cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                DeleteHistoryButton.IsEnabled = false;
                try
                {
                    await _history.ClearAllAsync();
                    _ircService.ClearRecentMessages();
                    StatusInfoBar.Severity = InfoBarSeverity.Success;
                    StatusInfoBar.Title = "History Deleted";
                    StatusInfoBar.Message = "All stored messages and chat history have been cleared.";
                    StatusInfoBar.IsOpen = true;
                }
                catch (Exception ex)
                {
                    StatusInfoBar.Severity = InfoBarSeverity.Error;
                    StatusInfoBar.Title = "Error";
                    StatusInfoBar.Message = $"Failed to clear history: {ex.Message}";
                    StatusInfoBar.IsOpen = true;
                }
                finally
                {
                    DeleteHistoryButton.IsEnabled = true;
                }
            }
        }

        private async void OnClearCredentialsClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Remove saved credentials?",
                Content = "This will remove your remembered server details, username, and password. You will need to enter them manually next time you log in.",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ClearCredentialsButton.IsEnabled = false;
                try
                {
                    _rememberedUserService.Clear();
                    StatusInfoBar.Severity = InfoBarSeverity.Success;
                    StatusInfoBar.Title = "Credentials Removed";
                    StatusInfoBar.Message = "Remembered server, username, and password have been cleared.";
                    StatusInfoBar.IsOpen = true;
                }
                catch (Exception ex)
                {
                    StatusInfoBar.Severity = InfoBarSeverity.Error;
                    StatusInfoBar.Title = "Error";
                    StatusInfoBar.Message = $"Failed to remove credentials: {ex.Message}";
                    StatusInfoBar.IsOpen = true;
                }
                finally
                {
                    ClearCredentialsButton.IsEnabled = true;
                }
            }
        }
    }
}