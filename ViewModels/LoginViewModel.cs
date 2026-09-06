using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using WinIrcClient.Services;

namespace WinIrcClient.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IrcClientService _ircService;
        private readonly RememberedUserService _rememberedUserService;
        private readonly ChatSettingsService _chatSettings;

        public LoginViewModel(
            IrcClientService ircService,
            RememberedUserService rememberedUserService,
            ChatSettingsService chatSettings)
        {
            _ircService = ircService;
            _rememberedUserService = rememberedUserService;
            _chatSettings = chatSettings;
            LoadRememberedUser();
        }

        public event Action? LoginSucceeded;

        [ObservableProperty]
        private string server = "irc.libera.chat";

        [ObservableProperty]
        private int port = 6697;

        [ObservableProperty]
        private string nick = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        [ObservableProperty]
        private bool rememberUser;

        [ObservableProperty]
        private bool isLoggingIn;

        public bool CanLogin => !IsLoggingIn && !string.IsNullOrWhiteSpace(Nick);

        partial void OnNickChanged(string value)
        {
            OnPropertyChanged(nameof(CanLogin));
            LoginCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsLoggingInChanged(bool value)
        {
            OnPropertyChanged(nameof(CanLogin));
            LoginCommand.NotifyCanExecuteChanged();
        }

        public void LoadRememberedUser()
        {
            var saved = _rememberedUserService.Load();
            if (saved == null) return;

            Server = saved.Server;
            Port = saved.Port;
            Nick = saved.Nick;
            Password = saved.Password;
            RememberUser = true;
        }

        [RelayCommand(CanExecute = nameof(CanLogin))]
        private async Task LoginAsync()
        {
            if (IsLoggingIn) return;
            IsLoggingIn = true;
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler() => tcs.TrySetResult(true);
            try
            {
                _ircService.LoggedIn += Handler;
                await _ircService.ConnectAsync(Server, Port).ConfigureAwait(false);
                await _ircService.LoginAsync(Nick, "winirc", "WinIrcClient", Password).ConfigureAwait(false);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000)).ConfigureAwait(false);
                if (completed == tcs.Task)
                {
                    // Start in the server/MOTD view; the user can join a channel manually.
                    _chatSettings.LastChannel = "Server";
                    if (RememberUser)
                    {
                        _rememberedUserService.Save(Server, Port, Nick, Password);
                    }
                    else
                    {
                        _rememberedUserService.Clear();
                    }
                    LoginSucceeded?.Invoke();
                }
                else
                {
                    // timeout - treat as failed; still try to navigate for debugging
                    // do not invoke LoginSucceeded to indicate failure
                }
            }
            finally
            {
                _ircService.LoggedIn -= Handler;
                IsLoggingIn = false;
            }
        }
    }
}
