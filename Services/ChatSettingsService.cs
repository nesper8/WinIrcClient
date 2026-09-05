using Windows.Storage;

namespace WinIrcClient.Services
{
    public sealed class ChatSettingsService
    {
        private const string AutoScrollIncomingKey = "autoScrollIncomingMessages";
        private const string LastChannelKey = "lastChatTarget";
        private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

        public bool AutoScrollIncomingMessages
        {
            get => !_settings.Values.TryGetValue(AutoScrollIncomingKey, out var value) || value is not bool enabled || enabled;
            set => _settings.Values[AutoScrollIncomingKey] = value;
        }

        public string LastChannel
        {
            get => _settings.Values.TryGetValue(LastChannelKey, out var value)
                ? value?.ToString() ?? string.Empty
                : string.Empty;
            set => _settings.Values[LastChannelKey] = value;
        }
    }
}