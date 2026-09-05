using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using WinIrcClient.ViewModels;

namespace WinIrcClient.Views
{
    public sealed partial class ChannelBrowser : UserControl
    {
        public ChatViewModel ViewModel { get; }
        public event Action<string>? JoinRequested;

        public ChannelBrowser()
        {
            InitializeComponent();
            ViewModel = ((App)Application.Current).Services.GetRequiredService<ChatViewModel>();
            DataContext = ViewModel;
        }

        public ChannelBrowser(ChatViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }

        private void OnJoinClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string channel }) JoinRequested?.Invoke(channel);
        }
    }
}