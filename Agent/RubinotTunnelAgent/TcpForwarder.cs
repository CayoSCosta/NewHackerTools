using System.Net;
using System.Net.Sockets;

namespace RubinotTunnelAgent
{
    /// <summary>
    /// Escuta em uma porta local (ex: 127.0.0.1:7171) e encaminha cada conexão
    /// recebida para o relay remoto (o servidor onde o DevToolsController.StartTunnel
    /// está rodando), que por sua vez encaminha até o servidor real (Rubinot).
    ///
    /// Fluxo:
    ///   Cliente do jogo -> 127.0.0.1:PortaLocal (este agente)
    ///                    -> RelayHost:RelayPort (seu servidor / DevToolsController)
    ///                    -> Rubinot (destino final, configurado no servidor)
    /// </summary>
    public sealed class TcpForwarder : IDisposable
    {
        public int LocalPort { get; }
        public string RelayHost { get; }
        public int RelayPort { get; }

        public event Action<string>? OnLog;
        public event Action<int>? OnActiveConnectionsChanged;

        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private int _activeConnections;
        private Task? _listenTask;

        public bool IsRunning { get; private set; }

        public TcpForwarder(int localPort, string relayHost, int relayPort)
        {
            LocalPort = localPort;
            RelayHost = relayHost;
            RelayPort = relayPort;
        }

        public void Start()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, LocalPort);
            _listener.Start();
            IsRunning = true;

            Log($"Agente escutando em 127.0.0.1:{LocalPort}, encaminhando para {RelayHost}:{RelayPort}");

            _listenTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            if (!IsRunning) return;

            IsRunning = false;
            _cts?.Cancel();

            try
            {
                _listener?.Stop();
            }
            catch { /* já pode ter sido derrubado */ }

            Log("Agente parado.");
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    TcpClient client;
                    try
                    {
                        client = await _listener!.AcceptTcpClientAsync(token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    _ = Task.Run(() => HandleClientAsync(client, token), token);
                }
            }
            catch (Exception ex)
            {
                Log($"Erro no loop de aceitação: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(TcpClient gameClient, CancellationToken token)
        {
            IncrementActive();
            var remoteEndpoint = gameClient.Client.RemoteEndPoint?.ToString() ?? "desconhecido";
            Log($"Nova conexão local de {remoteEndpoint}");

            using (gameClient)
            using (var relayClient = new TcpClient())
            {
                try
                {
                    relayClient.NoDelay = true;
                    gameClient.NoDelay = true;

                    using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    connectCts.CancelAfter(TimeSpan.FromSeconds(10));

                    await relayClient.ConnectAsync(RelayHost, RelayPort, connectCts.Token);

                    using var gameStream = gameClient.GetStream();
                    using var relayStream = relayClient.GetStream();

                    var toRelay = gameStream.CopyToAsync(relayStream, token);
                    var toGame = relayStream.CopyToAsync(gameStream, token);

                    await Task.WhenAny(toRelay, toGame);
                }
                catch (OperationCanceledException)
                {
                    Log($"Timeout/cancelado ao conectar no relay {RelayHost}:{RelayPort}");
                }
                catch (Exception ex)
                {
                    Log($"Conexão encerrada: {ex.Message}");
                }
            }

            DecrementActive();
        }

        private void IncrementActive()
        {
            var count = Interlocked.Increment(ref _activeConnections);
            OnActiveConnectionsChanged?.Invoke(count);
        }

        private void DecrementActive()
        {
            var count = Interlocked.Decrement(ref _activeConnections);
            OnActiveConnectionsChanged?.Invoke(count);
        }

        private void Log(string message) => OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
            _listener = null;
        }
    }
}
