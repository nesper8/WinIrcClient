using System;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinIrcClient.Services
{
    public class IrcConnection : IDisposable
    {
        private TcpClient? _client;
        private System.IO.Stream? _stream;
        private CancellationTokenSource? _cts;

        public event Action<string>? RawMessageReceived;

        public IrcConnection() { }

        public async Task ConnectAsync(string host, int port, CancellationToken cancellation = default)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port, cancellation).ConfigureAwait(false);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

            // Use TLS for the common secure IRC port 6697
            if (port == 6697)
            {
                var netStream = _client.GetStream();
                var ssl = new SslStream(netStream, leaveInnerStreamOpen: false, userCertificateValidationCallback: (a,b,c,d) => true);
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                }, cancellation).ConfigureAwait(false);
                _stream = ssl;
            }
            else
            {
                _stream = _client.GetStream();
            }

            _ = Task.Run(() => ReadLoopAsync(_cts.Token));
        }

        public async Task SendRawAsync(string raw)
        {
            if (_stream == null) throw new InvalidOperationException("Not connected");
            var bytes = Encoding.UTF8.GetBytes(raw + "\r\n");
            await _stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            await _stream.FlushAsync().ConfigureAwait(false);
        }

        public void Disconnect()
        {
            try { _cts?.Cancel(); } catch { }
            _stream?.Dispose();
            _client?.Dispose();
            _stream = null;
            _client = null;
            _cts?.Dispose();
            _cts = null;
        }

        public async Task DisconnectAsync(string reason = "Client closing")
        {
            if (_stream != null)
            {
                try
                {
                    await SendRawAsync($"QUIT :{reason}").ConfigureAwait(false);
                }
                catch
                {
                }
            }

            Disconnect();
        }

        private async Task ReadLoopAsync(CancellationToken cancellation)
        {
            if (_stream == null) return;
            var reader = new StreamReader(_stream, Encoding.UTF8);
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) break;
                    RawMessageReceived?.Invoke(line);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
