using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using WinIrcClient.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinIrcClient;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly IrcClientService _ircService;
    private bool _closeAfterDisconnect;

    public MainWindow()
    {
        InitializeComponent();

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1500, 830));

        _ircService = ((App)Application.Current).Services.GetRequiredService<IrcClientService>();
        AppWindow.Closing += OnAppWindowClosing;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Navigate the root frame to the login page on startup.
        RootFrame.Navigate(typeof(WinIrcClient.Views.LoginPage));
    }

    public void Navigate(Type pageType)
    {
        RootFrame.Navigate(pageType);
    }

    private async void OnAppWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_closeAfterDisconnect) return;

        args.Cancel = true;
        _closeAfterDisconnect = true;
        try
        {
            await _ircService.DisconnectAsync();
        }
        finally
        {
            Close();
        }
    }
}
