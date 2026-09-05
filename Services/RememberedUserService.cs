using Windows.Security.Credentials;
using Windows.Storage;

namespace WinIrcClient.Services
{
    public sealed record RememberedUser(string Server, int Port, string Nick, string Password);

    public sealed class RememberedUserService
    {
        private const string Resource = "WinIrcClient";
        private const string ServerKey = "rememberedServer";
        private const string PortKey = "rememberedPort";
        private const string NickKey = "rememberedNick";

        private readonly ApplicationDataContainer _settings =
            ApplicationData.Current.LocalSettings;

        public RememberedUser? Load()
        {
            if (!_settings.Values.ContainsKey(ServerKey) ||
                !_settings.Values.ContainsKey(PortKey) ||
                !_settings.Values.ContainsKey(NickKey))
            {
                return null;
            }

            var server = _settings.Values[ServerKey]?.ToString();
            var nick = _settings.Values[NickKey]?.ToString();
            if (!int.TryParse(_settings.Values[PortKey]?.ToString(), out var port) ||
                string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(nick))
            {
                return null;
            }

            try
            {
                var vault = new PasswordVault();
                var credential = vault.Retrieve(Resource, nick);
                credential.RetrievePassword();
                return new RememberedUser(server, port, nick, credential.Password);
            }
            catch (Exception)
            {
                return new RememberedUser(server, port, nick, string.Empty);
            }
        }

        public void Save(string server, int port, string nick, string password)
        {
            _settings.Values[ServerKey] = server;
            _settings.Values[PortKey] = port;
            _settings.Values[NickKey] = nick;

            var vault = new PasswordVault();
            RemoveCredentials(vault);
            vault.Add(new PasswordCredential(Resource, nick, password));
        }

        public void Clear()
        {
            _settings.Values.Remove(ServerKey);
            _settings.Values.Remove(PortKey);
            _settings.Values.Remove(NickKey);
            RemoveCredentials(new PasswordVault());
        }

        private static void RemoveCredentials(PasswordVault vault)
        {
            try
            {
                foreach (var credential in vault.FindAllByResource(Resource))
                {
                    vault.Remove(credential);
                }
            }
            catch (Exception)
            {
                // No stored credentials is a normal first-run state.
            }
        }
    }
}