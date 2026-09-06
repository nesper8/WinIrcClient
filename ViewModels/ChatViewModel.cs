using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WinIrcClient.Models;
using WinIrcClient.Services;

namespace WinIrcClient.ViewModels
{
    public partial class ChatViewModel : ObservableObject, IDisposable
    {
        private readonly IrcClientService _ircService;
        private readonly MessageHistoryService _history;
        private readonly ChatSettingsService _settings;
        private readonly HashSet<string> _locallyEchoedMessages = new();

        public ObservableCollection<Message> Messages { get; } = new();
        public ObservableCollection<Message> AllMessages { get; } = new();
        public ObservableCollection<string> Channels { get; } = new();
        public ObservableCollection<User> Users { get; } = new();
        public ObservableCollection<ChannelInfo> AvailableChannels { get; } = new();
        public event Action? MessagesChanged;
        public event Action? ScrollToLatestRequested;

        public ChatViewModel(IrcClientService ircService, MessageHistoryService history, ChatSettingsService settings)
        {
            _ircService = ircService;
            _history = history;
            _settings = settings;
            _ircService.MessageReceived += OnMessageReceived;
            _ircService.UsersChanged += OnUsersChanged;
            _ircService.ChannelsChanged += OnChannelsChanged;
            _history.HistoryCleared += OnHistoryCleared;
            Channels.Add("Server");
            CurrentChannel = string.IsNullOrWhiteSpace(_settings.LastChannel)
                ? "Server"
                : _settings.LastChannel;
        }

        public void Dispose()
        {
            _ircService.MessageReceived -= OnMessageReceived;
            _ircService.UsersChanged -= OnUsersChanged;
            _ircService.ChannelsChanged -= OnChannelsChanged;
            _history.HistoryCleared -= OnHistoryCleared;
        }

        private void OnHistoryCleared()
        {
            var app = (App)Microsoft.UI.Xaml.Application.Current;
            var dq = app.MainWindowInstance?.DispatcherQueue;
            void Clear()
            {
                AllMessages.Clear();
                Messages.Clear();
                MessagesChanged?.Invoke();
            }
            if (dq != null) dq.TryEnqueue(Clear); else Clear();
        }

        public void OpenPrivateConversation(string nick)
        {
            if (string.IsNullOrWhiteSpace(nick)) return;
            if (!Channels.Contains(nick)) Channels.Add(nick);
            PrivateTarget = nick;
            CurrentChannel = nick;
        }

        public Task RequestChannelListAsync()
        {
            return _ircService.RequestChannelListAsync();
        }

        public async Task JoinListedChannelAsync(string channel)
        {
            await JoinChannelAsync(channel).ConfigureAwait(false);
        }

        public async System.Threading.Tasks.Task InitializeAsync()
        {
            // Load recent messages from the database
            IReadOnlyList<Message> msgs;
            try
            {
                msgs = await _history.LoadRecentAsync().ConfigureAwait(false);
            }
            catch
            {
                msgs = Array.Empty<Message>();
            }

            var mergedMessages = msgs
                .Concat(_ircService.RecentMessages)
                .GroupBy(BuildMessageKey)
                .Select(group => group.Last())
                .OrderBy(message => message.Id)
                .ThenBy(message => message.Timestamp)
                .ToList();
            OnUsersChanged();

            var app = (App)Microsoft.UI.Xaml.Application.Current;
            var dq = app.MainWindowInstance?.DispatcherQueue;
            if (dq != null)
            {
                dq.TryEnqueue(() =>
                {
                    AllMessages.Clear();
                    foreach (var m in mergedMessages) AllMessages.Add(m);
                    foreach (var ch in mergedMessages.Select(m => m.Channel).Distinct()) if (!Channels.Contains(ch)) Channels.Add(ch);
                    RefreshFilteredMessages();
                    MessagesChanged?.Invoke();
                    ScrollToLatestRequested?.Invoke();
                });
            }
            else
            {
                AllMessages.Clear();
                foreach (var m in mergedMessages) AllMessages.Add(m);
                foreach (var ch in mergedMessages.Select(m => m.Channel).Distinct()) if (!Channels.Contains(ch)) Channels.Add(ch);
                RefreshFilteredMessages();
                MessagesChanged?.Invoke();
                ScrollToLatestRequested?.Invoke();
            }
        }

        private void RefreshFilteredMessages()
        {
            Messages.Clear();
            var selected = CurrentChannel;
            var list = AllMessages.Where(m => string.IsNullOrEmpty(selected) || m.Channel == selected).ToList();
            foreach (var m in list) Messages.Add(m);
        }

        partial void OnCurrentChannelChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _settings.LastChannel = value;
                _ = _ircService.RefreshUsersAsync(value);
            }

            var app = (App)Microsoft.UI.Xaml.Application.Current;
            var dq = app.MainWindowInstance?.DispatcherQueue;
            if (dq != null) dq.TryEnqueue(() => RefreshFilteredMessages());
            else RefreshFilteredMessages();
        }

        [ObservableProperty]
        private string currentChannel = "Server";

        [ObservableProperty]
        private string outgoingText = string.Empty;

        [ObservableProperty]
        private string channelToJoin = string.Empty;

        [ObservableProperty]
        private string privateTarget = string.Empty;

        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(OutgoingText)) return;
            var text = OutgoingText.Trim();
            await _ircService.SendMessageAsync(CurrentChannel, text).ConfigureAwait(false);

            // Show outgoing messages immediately; some IRC servers do not echo them.
            var message = new Message
            {
                Channel = CurrentChannel,
                Sender = string.IsNullOrWhiteSpace(_ircService.CurrentNick) ? "me" : _ircService.CurrentNick,
                Text = text,
                Timestamp = DateTimeOffset.Now,
                Kind = CurrentChannel.StartsWith("#", StringComparison.Ordinal) ? MessageKind.Channel : MessageKind.Private
            };
            _locallyEchoedMessages.Add(BuildMessageKey(message));
            AddMessage(message);
            ScrollToLatestRequested?.Invoke();
            OutgoingText = string.Empty;
        }

        [RelayCommand]
        private async Task SendPrivateMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(PrivateTarget) || string.IsNullOrWhiteSpace(OutgoingText)) return;
            var target = PrivateTarget.Trim();
            var text = OutgoingText.Trim();
            await _ircService.SendMessageAsync(target, text).ConfigureAwait(false);

            var message = new Message
            {
                Channel = target,
                Sender = string.IsNullOrWhiteSpace(_ircService.CurrentNick) ? "me" : _ircService.CurrentNick,
                Text = text,
                Timestamp = DateTimeOffset.Now,
                Kind = MessageKind.Private
            };
            if (!Channels.Contains(target)) Channels.Add(target);
            CurrentChannel = target;
            _locallyEchoedMessages.Add(BuildMessageKey(message));
            AddMessage(message);
            ScrollToLatestRequested?.Invoke();
            OutgoingText = string.Empty;
        }

        private static string BuildMessageKey(Message message)
        {
            return $"{message.Channel}\u001f{message.Sender}\u001f{message.Text}";
        }

        [RelayCommand]
        private async Task JoinChannelAsync(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel)) return;
            channel = channel.Trim();
            if (!channel.StartsWith("#")) channel = "#" + channel;
            await _ircService.JoinChannelAsync(channel).ConfigureAwait(false);
            if (!Channels.Contains(channel)) Channels.Add(channel);
            CurrentChannel = channel;
            ChannelToJoin = string.Empty;
        }

        private void OnMessageReceived(Message message)
        {
            // Libera may echo our PRIVMSG. Keep the local copy and avoid displaying it twice.
            if (_locallyEchoedMessages.Remove(BuildMessageKey(message))) return;

            AddMessage(message);
        }

        private void OnUsersChanged()
        {
            var users = _ircService.Users;
            var app = (App)Microsoft.UI.Xaml.Application.Current;
            var dq = app.MainWindowInstance?.DispatcherQueue;
            void Update()
            {
                Users.Clear();
                foreach (var user in users) Users.Add(user);
            }
            if (dq != null) dq.TryEnqueue(Update); else Update();
        }

        private void OnChannelsChanged()
        {
            var channels = _ircService.Channels;
            var app = (App)Microsoft.UI.Xaml.Application.Current;
            var dq = app.MainWindowInstance?.DispatcherQueue;
            void Update()
            {
                AvailableChannels.Clear();
                foreach (var channel in channels) AvailableChannels.Add(channel);
            }
            if (dq != null) dq.TryEnqueue(Update); else Update();
        }

        private void AddMessage(Message message)
        {
            // Ensure UI thread update
            var app = (App)Microsoft.UI.Xaml.Application.Current;
            var dq = app.MainWindowInstance?.DispatcherQueue;
            if (dq != null)
            {
                dq.TryEnqueue(() =>
                {
                    AllMessages.Add(message);
                    if (string.IsNullOrEmpty(CurrentChannel) || message.Channel == CurrentChannel) Messages.Add(message);
                    if (!Channels.Contains(message.Channel)) Channels.Add(message.Channel);
                    MessagesChanged?.Invoke();
                    if (_settings.AutoScrollIncomingMessages) ScrollToLatestRequested?.Invoke();
                });
            }
            else
            {
                AllMessages.Add(message);
                if (string.IsNullOrEmpty(CurrentChannel) || message.Channel == CurrentChannel) Messages.Add(message);
                if (!Channels.Contains(message.Channel)) Channels.Add(message.Channel);
                MessagesChanged?.Invoke();
                if (_settings.AutoScrollIncomingMessages) ScrollToLatestRequested?.Invoke();
            }

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await _history.SaveAsync(message).ConfigureAwait(false);
                }
                catch { }
            });
        }
    }
}
