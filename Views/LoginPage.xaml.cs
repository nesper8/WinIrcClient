using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinIrcClient.Services;
using WinIrcClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace WinIrcClient.Views
{
    public sealed partial class LoginPage : Page
    {
        public LoginViewModel ViewModel { get; }

        public LoginPage()
        {
            this.InitializeComponent();
            var services = ((App)Application.Current).Services;
            ViewModel = services.GetRequiredService<LoginViewModel>();
            DataContext = ViewModel;
            PasswordBox.Password = ViewModel.Password;
            ViewModel.LoginSucceeded += OnLoginSucceeded;
            PasswordBox.PasswordChanged += (s, e) =>
            {
                ViewModel.Password = PasswordBox.Password;
            };
        }

        private void OnLoginClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.LoginCommand.CanExecute(null))
            {
                ViewModel.LoginCommand.Execute(null);
            }
        }

        private void OnSettingsClicked(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).MainWindowInstance?.Navigate(typeof(SettingsPage));
        }

        private void OnLoginSucceeded()
        {
            var app = (App)Application.Current;
            try
            {
                // Ensure navigation happens on the UI thread
                app.MainWindowInstance?.DispatcherQueue.TryEnqueue(() =>
                {
                    try { app.MainWindowInstance?.Navigate(typeof(WinIrcClient.Views.ChatPage)); }
                    catch { }
                });
            }
            catch { }
        }
    }
}
