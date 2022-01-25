#nullable enable
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetProxy
{
    internal class UdpProxy : IProxy
    {
        /// <summary>
        /// Milliseconds
        /// </summary>
        public int ConnectionTimeout { get; set; } = (4 * 60 * 1000);

        public async Task Start(string remoteServerHostNameOrAddress, ushort remoteServerPort, ushort localPort, string? localIp = null)
        {
            var connections = new ConcurrentDictionary<IPEndPoint, UdpConnection>();

            // TCP will lookup every time while this is only once.
            var ips = await Dns.GetHostAddressesAsync(remoteServerHostNameOrAddress).ConfigureAwait(false);
            var remoteServerEndPoint = new IPEndPoint(ips[0], remoteServerPort);

            var localServer = new UdpClient(AddressFamily.InterNetworkV6);
            localServer.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            IPAddress localIpAddress = string.IsNullOrEmpty(localIp) ? IPAddress.IPv6Any : IPAddress.Parse(localIp);
            localServer.Client.Bind(new IPEndPoint(localIpAddress, localPort));

            Console.WriteLine($"UDP proxy started [{localIpAddress}]:{localPort} -> [{remoteServerHostNameOrAddress}]:{remoteServerPort}");

            var _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                    foreach (var connection in connections.ToArray())
                    {
                        if (connection.Value.LastActivity + ConnectionTimeout < Environment.TickCount64)
                        {
                            connections.TryRemove(connection.Key, out UdpConnection? c);
                            connection.Value.Stop();
                        }
                    }
                }
            });

            while (true)
            {
                try
                {
                    var message = await localServer.ReceiveAsync().ConfigureAwait(false);
                    var sourceEndPoint = message.RemoteEndPoint;
                    var client = connections.GetOrAdd(sourceEndPoint,
                        ep =>
                        {
                            var udpConnection = new UdpConnection(localServer, sourceEndPoint, remoteServerEndPoint);
                            udpConnection.Run();
                            return udpConnection;
                        });
                    await client.SendToServerAsync(message.Buffer).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"an exception occurred on receiving a client datagram: {ex}");
                }
            }
        }
    }

    internal class UdpConnection
    {
        private readonly UdpClient _localServer;
        private readonly UdpClient _forwardClient;
        public long LastActivity { get; private set; } = Environment.TickCount64;
        private readonly IPEndPoint _sourceEndpoint;
        private readonly IPEndPoint _remoteEndpoint;
        private readonly EndPoint? _serverLocalEndpoint;
        private EndPoint? _forwardLocalEndpoint;
        private bool _isRunning;
        private long _totalBytesForwarded;
        private long _totalBytesResponded;
        private readonly TaskCompletionSource<bool> _forwardConnectionBindCompleted = new TaskCompletionSource<bool>();

        public UdpConnection(UdpClient localServer, IPEndPoint sourceEndpoint, IPEndPoint remoteEndpoint)
        {
            _localServer = localServer;
            _serverLocalEndpoint = _localServer.Client.LocalEndPoint;

            _isRunning = true;
            _remoteEndpoint = remoteEndpoint;
            _sourceEndpoint = sourceEndpoint;

            _forwardClient = new UdpClient(AddressFamily.InterNetworkV6);
            _forwardClient.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
        }

        public async Task SendToServerAsync(byte[] message)
        {
            LastActivity = Environment.TickCount64;

            await _forwardConnectionBindCompleted.Task.ConfigureAwait(false);
            var sent = await _forwardClient.SendAsync(message, message.Length, _remoteEndpoint).ConfigureAwait(false);
            Interlocked.Add(ref _totalBytesForwarded, sent);
        }

        public void Run()
        {
            Task.Run(async () =>
            {
                using (_forwardClient)
                {
                    _forwardClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                    _forwardLocalEndpoint = _forwardClient.Client.LocalEndPoint;
                    _forwardConnectionBindCompleted.SetResult(true);
                    Console.WriteLine($"Established UDP {_sourceEndpoint} => {_serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}");

                    while (_isRunning)
                    {
                        try
                        {
                            var result = await _forwardClient.ReceiveAsync().ConfigureAwait(false);
                            LastActivity = Environment.TickCount64;
                            var sent = await _localServer.SendAsync(result.Buffer, result.Buffer.Length, _sourceEndpoint).ConfigureAwait(false);
                            Interlocked.Add(ref _totalBytesResponded, sent);
                        }
                        catch (Exception ex)
                        {
                            if (_isRunning)
                            {
                                Console.WriteLine($"An exception occurred while receiving a server datagram : {ex}");
                            }
                        }
                    }
                }
            });
        }

        public void Stop()
        {
            try
            {
                Console.WriteLine($"Closed UDP {_sourceEndpoint} => {_serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}. {_totalBytesForwarded} bytes forwarded, {_totalBytesResponded} bytes responded.");
                _isRunning = false;
                _forwardClient.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An exception occurred while closing UdpConnection : {ex}");
            }
        }
    }
}
