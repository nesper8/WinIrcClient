using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinIrcClient.Services;
using WinIrcClient.ViewModels;
using WinIrcClient.Models;
using Microsoft.Extensions.DependencyInjection;

namespace WinIrcClient.Views
{
    public sealed partial class ChatPage : Page
    {
        public ChatViewModel ViewModel { get; }
        private readonly IrcClientService _ircService;

        public ChatPage()
        {
            this.InitializeComponent();
            var services = ((App)Application.Current).Services;
            _ircService = services.GetRequiredService<IrcClientService>();
            ViewModel = services.GetRequiredService<ChatViewModel>();
            DataContext = ViewModel;
            ViewModel.MessagesChanged += UpdateEmptyState;
            ViewModel.ScrollToLatestRequested += ScrollToLatest;
        }

        private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                await ViewModel.InitializeAsync();
                UpdateEmptyState();
                ScrollToLatest();
            }
            catch { }
        }

        private async void OnRefreshClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                await ViewModel.InitializeAsync();
                UpdateEmptyState();
            }
            catch { }
        }

        private void OnJoinClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.JoinChannelCommand.CanExecute(ViewModel.ChannelToJoin))
            {
                ViewModel.JoinChannelCommand.Execute(ViewModel.ChannelToJoin);
            }
        }

        private async void OnChannelsClicked(object sender, RoutedEventArgs e)
        {
            var browser = new ChannelBrowser(ViewModel);
            var dialog = new ContentDialog
            {
                Title = "Available channels",
                Content = browser,
                CloseButtonText = "Close",
                XamlRoot = XamlRoot
            };

            browser.JoinRequested += async channel =>
            {
                await ViewModel.JoinListedChannelAsync(channel);
                dialog.Hide();
            };

            await ViewModel.RequestChannelListAsync();
            await dialog.ShowAsync();
        }

        private void OnPrivateClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SendPrivateMessageCommand.CanExecute(null))
            {
                ViewModel.SendPrivateMessageCommand.Execute(null);
            }
        }

        private async void OnUsernameClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            var nick = button.Tag switch
            {
                User user => user.Nick,
                Message message => message.Sender,
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(nick) ||
                nick.Equals(_ircService.CurrentNick, StringComparison.OrdinalIgnoreCase) ||
                nick.Equals("server", StringComparison.OrdinalIgnoreCase) ||
                nick.Equals("MOTD", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = $"Private message with {nick}",
                Content = $"Start a private conversation with {nick}?",
                PrimaryButtonText = "Start",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ViewModel.OpenPrivateConversation(nick);
                MessageBox.Focus(FocusState.Programmatic);
            }
        }

        private void OnSettingsClicked(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).MainWindowInstance?.Navigate(typeof(SettingsPage));
        }

        private void UpdateEmptyState()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                EmptyState.Visibility = ViewModel.Messages.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            });
        }

        private void ScrollToLatest()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.Messages.Count > 0)
                {
                    MessageList.ScrollIntoView(ViewModel.Messages[^1]);
                }
            });
        }

        private void OnSendClicked(object sender, RoutedEventArgs e)
        {
            SendCurrentMessage();
        }

        private async void OnLogoutClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.Dispose();
            await _ircService.DisconnectAsync();
            ((App)Application.Current).MainWindowInstance?.Navigate(typeof(LoginPage));
        }

        private void OnMessageBoxKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                SendCurrentMessage();
                e.Handled = true;
            }
        }

        private void SendCurrentMessage()
        {
            ViewModel.OutgoingText = MessageBox.Text;
            if (ViewModel.SendMessageCommand.CanExecute(null))
            {
                ViewModel.SendMessageCommand.Execute(null);
                MessageBox.Text = string.Empty;
            }
        }
    }
}
