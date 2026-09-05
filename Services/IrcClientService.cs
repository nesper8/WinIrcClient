using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WinIrcClient.Models;

namespace WinIrcClient.Services
{
    public class IrcClientService
    {
        private readonly IrcConnection _connection;
        private readonly List<Message> _recentMessages = new();
        private readonly Dictionary<string, User> _users = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ChannelInfo> _channels = new(StringComparer.OrdinalIgnoreCase);
        private bool _channelListInProgress;

        public event Action<Message>? MessageReceived;
        public event Action? LoggedIn;
        public event Action? UsersChanged;
        public event Action? ChannelsChanged;
        public string CurrentNick { get; private set; } = string.Empty;
        public IReadOnlyList<Message> RecentMessages => _recentMessages.ToArray();
        public IReadOnlyList<User> Users => _users.Values.OrderBy(user => user.Nick, StringComparer.OrdinalIgnoreCase).ToArray();
        public IReadOnlyList<ChannelInfo> Channels => _channels.Values
            .OrderByDescending(channel => channel.UserCount)
            .ThenBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        public string ActiveChannel { get; private set; } = string.Empty;

        public IrcClientService(IrcConnection connection)
        {
            _connection = connection;
            _connection.RawMessageReceived += OnRawMessage;
        }

        public Task ConnectAsync(string host, int port)
        {
            return _connection.ConnectAsync(host, port);
        }

        public Task SendRawAsync(string raw) => _connection.SendRawAsync(raw);

        public void Disconnect()
        {
            _connection.Disconnect();
            CurrentNick = string.Empty;
        }

        public async Task DisconnectAsync()
        {
            await _connection.DisconnectAsync("Client closing").ConfigureAwait(false);
            CurrentNick = string.Empty;
        }

        public Task LoginAsync(string nick, string user = "winirc", string realName = "WinIrcClient", string? password = null)
        {
            return Task.Run(async () =>
            {
                CurrentNick = nick;
                if (!string.IsNullOrEmpty(password))
                {
                    var saslTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var promptTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    void rawHandler(string raw)
                    {
                        try
                        {
                            // detect SASL success/failure numeric replies
                            var m = Regex.Match(raw, @":[^ ]+ (?<code>\d{3}) (?<nick>[^ ]+) ?(?<rest>.*)");
                            if (m.Success)
                            {
                                var code = m.Groups["code"].Value;
                                if (code == "903") saslTcs.TrySetResult(true); // SASL success
                                if (code == "904" || code == "905") saslTcs.TrySetResult(false); // SASL failed
                            }

                            // server may send AUTHENTICATE + or 334
                            if (raw.StartsWith("AUTHENTICATE ") || raw.Contains(" 334 "))
                            {
                                promptTcs.TrySetResult(true);
                            }
                        }
                        catch { }
                    }

                    _connection.RawMessageReceived += rawHandler;
                    try
                    {
                        // Request SASL
                        await SendRawAsync("CAP REQ :sasl").ConfigureAwait(false);
                        await SendRawAsync("AUTHENTICATE PLAIN").ConfigureAwait(false);

                        // wait for server prompt (AUTHENTICATE + or 334) or timeout
                        var promptCompleted = await Task.WhenAny(promptTcs.Task, Task.Delay(5000)).ConfigureAwait(false);
                        // Prepare credentials: \0username\0password
                        var cred = "\0" + nick + "\0" + password;
                        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(cred));

                        // Send base64 credentials; split into 400-char chunks if needed
                        var idx = 0;
                        while (idx < b64.Length)
                        {
                            var chunk = b64.Substring(idx, Math.Min(400, b64.Length - idx));
                            await SendRawAsync($"AUTHENTICATE {chunk}").ConfigureAwait(false);
                            idx += chunk.Length;
                        }

                        // If credentials exactly ended at boundary, servers expect a final AUTHENTICATE +? But sending as above is fine.

                        // Wait for SASL result
                        var completed = await Task.WhenAny(saslTcs.Task, Task.Delay(10000)).ConfigureAwait(false);
                        if (completed == saslTcs.Task && saslTcs.Task.Result)
                        {
                            // success
                            await SendRawAsync("CAP END").ConfigureAwait(false);
                        }
                        else
                        {
                            // SASL failed or timed out - continue without SASL
                            await SendRawAsync("CAP END").ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        _connection.RawMessageReceived -= rawHandler;
                    }
                }

                await SendRawAsync($"NICK {nick}").ConfigureAwait(false);
                await SendRawAsync($"USER {user} 0 * :{realName}").ConfigureAwait(false);
            });
        }

        public Task JoinChannelAsync(string channel)
        {
            ActiveChannel = channel;
            _users.Clear();
            UsersChanged?.Invoke();
            return SendRawAsync($"JOIN {channel}");
        }

        public async Task RefreshUsersAsync(string channel)
        {
            ActiveChannel = channel;
            _users.Clear();
            UsersChanged?.Invoke();

            if (channel.StartsWith("#", StringComparison.Ordinal))
            {
                await SendRawAsync($"NAMES {channel}").ConfigureAwait(false);
            }
        }

        public async Task RequestChannelListAsync()
        {
            _channels.Clear();
            _channelListInProgress = true;
            ChannelsChanged?.Invoke();
            await SendRawAsync("LIST").ConfigureAwait(false);
        }

        public Task SendMessageAsync(string target, string message)
        {
            return SendRawAsync($"PRIVMSG {target} :{message}");
        }

        private void OnRawMessage(string raw)
        {
            // Reply to server PINGs to keep connection alive
            if (raw.StartsWith("PING "))
            {
                var token = raw.Substring(5).Trim();
                _ = SendRawAsync($"PONG :{token}");
                return;
            }

            // Detect login success (numeric 001 welcome)
            // Example: ":irc.example.net 001 nick :Welcome to the IRC Network"
            var numericMatch = System.Text.RegularExpressions.Regex.Match(raw, @":[^ ]+ (?<code>\d{3}) (?<nick>[^ ]+) ?(?<rest>.*)");
            if (numericMatch.Success)
            {
                var code = numericMatch.Groups["code"].Value;
                if (code == "001")
                {
                    LoggedIn?.Invoke();
                }
            }

            // Simple PRIVMSG parsing
            // Example: ":nick!user@host PRIVMSG #chan :message"
            var m = Regex.Match(raw, @":(?<nick>[^!]+)!.* PRIVMSG (?<target>[^ ]+) :(?<text>.+)");
            if (m.Success)
            {
                var target = m.Groups["target"].Value;
                var sender = m.Groups["nick"].Value;
                var isChannel = target.StartsWith("#", StringComparison.Ordinal);
                var conversation = !isChannel && sender.Equals(CurrentNick, StringComparison.OrdinalIgnoreCase)
                    ? target
                    : sender;
                var msg = new Message
                {
                    Sender = sender,
                    Channel = isChannel ? target : conversation,
                    Text = m.Groups["text"].Value,
                    Timestamp = DateTimeOffset.Now,
                    Kind = isChannel ? MessageKind.Channel : MessageKind.Private
                };
                PublishMessage(msg);
                return;
            }

            var notice = Regex.Match(raw, @":(?<sender>[^! ]+)(?:!.*)? NOTICE (?<target>[^ ]+) :(?<text>.+)");
            if (notice.Success)
            {
                PublishMessage(new Message
                {
                    Channel = "Server",
                    Sender = notice.Groups["sender"].Value,
                    Text = notice.Groups["text"].Value,
                    Timestamp = DateTimeOffset.Now,
                    Kind = MessageKind.Server
                });
                return;
            }

            if (numericMatch.Success)
            {
                var code = numericMatch.Groups["code"].Value;
                var text = numericMatch.Groups["rest"].Value.TrimStart(':');
                if (code == "353")
                {
                    var names = Regex.Match(raw, @" 353 [^ ]+ [^ ]+ (?<channel>[^ ]+) :(?<names>.*)$");
                    if (names.Success && names.Groups["channel"].Value.Equals(ActiveChannel, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var name in names.Groups["names"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var nick = name.TrimStart('@', '+', '%', '&', '~');
                            if (!string.IsNullOrWhiteSpace(nick)) _users[nick] = new User { Nick = nick };
                        }
                        UsersChanged?.Invoke();
                    }
                }
                else if (code == "322")
                {
                    var channel = Regex.Match(raw, @" 322 [^ ]+ (?<name>[^ ]+) (?<count>\d+) :?(?<topic>.*)$");
                    if (channel.Success && int.TryParse(channel.Groups["count"].Value, out var count))
                    {
                        _channels[channel.Groups["name"].Value] = new ChannelInfo
                        {
                            Name = channel.Groups["name"].Value,
                            UserCount = count
                        };
                    }
                }
                else if (code == "323" && _channelListInProgress)
                {
                    _channelListInProgress = false;
                    ChannelsChanged?.Invoke();
                }
                if (code is "001" or "002" or "003" or "004" or "005" or "251" or "252" or "253" or "254" or "255" or "265" or "266" or "250")
                {
                    PublishMessage(new Message
                    {
                        Channel = "Server",
                        Sender = "server",
                        Text = text,
                        Timestamp = DateTimeOffset.Now,
                        Kind = MessageKind.Server
                    });
                }
                else if (code is "375" or "372" or "376")
                {
                    PublishMessage(new Message
                    {
                        Channel = "Server",
                        Sender = "MOTD",
                        Text = text,
                        Timestamp = DateTimeOffset.Now,
                        Kind = MessageKind.Motd
                    });
                }
            }

            var join = Regex.Match(raw, @":(?<nick>[^! ]+)!.* JOIN (?<channel>[^ ]+)");
            if (join.Success && join.Groups["channel"].Value.Equals(ActiveChannel, StringComparison.OrdinalIgnoreCase))
            {
                _users[join.Groups["nick"].Value] = new User { Nick = join.Groups["nick"].Value };
                UsersChanged?.Invoke();
            }

            var part = Regex.Match(raw, @":(?<nick>[^! ]+)!.* (?:PART|QUIT)(?: (?<channel>[^ ]+))?");
            if (part.Success)
            {
                _users.Remove(part.Groups["nick"].Value);
                UsersChanged?.Invoke();
            }

            var nickChange = Regex.Match(raw, @":(?<old>[^! ]+)!.* NICK :?(?<new>[^ ]+)");
            if (nickChange.Success && _users.Remove(nickChange.Groups["old"].Value))
            {
                _users[nickChange.Groups["new"].Value] = new User { Nick = nickChange.Groups["new"].Value };
                UsersChanged?.Invoke();
            }
        }

        private void PublishMessage(Message message)
        {
            lock (_recentMessages)
            {
                _recentMessages.Add(message);
                if (_recentMessages.Count > 500)
                {
                    _recentMessages.RemoveRange(0, _recentMessages.Count - 500);
                }
            }

            MessageReceived?.Invoke(message);
        }
    }
}
