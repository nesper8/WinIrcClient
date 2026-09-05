using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using WinIrcClient.Data;
using WinIrcClient.Services;
using WinIrcClient.ViewModels;
using System.IO;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinIrcClient;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    public IServiceProvider Services { get; private set; } = default!;
    public MainWindow? MainWindowInstance { get; private set; }
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        ConfigureServices();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();
        var dataDirectory = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        Directory.CreateDirectory(dataDirectory);
        var databasePath = System.IO.Path.Combine(dataDirectory, "winirc.db");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite($"Data Source={databasePath}");
        });

        services.AddSingleton<ChatSettingsService>();
        services.AddSingleton<RememberedUserService>();
        services.AddSingleton<IrcConnection>();
        services.AddSingleton<IrcClientService>();
        services.AddSingleton<MessageHistoryService>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<WinIrcClient.ViewModels.ChatViewModel>();

        Services = services.BuildServiceProvider();
        // Ensure DB is created
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        MainWindowInstance = (MainWindow)_window;
        _window.Activate();
    }
}
