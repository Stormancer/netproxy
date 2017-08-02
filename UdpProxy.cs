using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetProxy
{
    class UdpProxy : IProxy
    {
        public async Task Start(string remoteServerIp, ushort remoteServerPort, ushort localPort, string localIp = null)
        {
            var clients = new ConcurrentDictionary<IPEndPoint, UdpClient>();

            var server = new System.Net.Sockets.UdpClient(AddressFamily.InterNetworkV6);
            server.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            IPAddress localIpAddress = string.IsNullOrEmpty(localIp) ? IPAddress.IPv6Any : IPAddress.Parse(localIp);
            server.Client.Bind(new IPEndPoint(localIpAddress, localPort));
            Console.WriteLine($"proxy started UDP:{localIpAddress}|{localPort} -> {remoteServerIp}|{remoteServerPort}");
            var _ = Task.Run(async () =>
            {

                while (true)
                {
                    await Task.Delay(10000);
                    foreach (var client in clients.ToArray())
                    {
                        if (client.Value.lastActivity + TimeSpan.FromSeconds(60) < DateTime.UtcNow)
                        {
                            UdpClient c;
                            clients.TryRemove(client.Key, out c);
                            client.Value.Stop();
                        }
                    }
                }

            });
            while (true)
            {

                try
                {
                    var message = await server.ReceiveAsync();
                    var endpoint = message.RemoteEndPoint;
                    var client = clients.GetOrAdd(endpoint, ep => new UdpClient(server, endpoint, new IPEndPoint(IPAddress.Parse(remoteServerIp), remoteServerPort)));
                    await client.SendToServer(message.Buffer);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"an exception occurred on recieving a client datagram: {ex}");
                }

            }
        }
    }

    class UdpClient
    {
        private readonly System.Net.Sockets.UdpClient _server;
        public UdpClient(System.Net.Sockets.UdpClient server, IPEndPoint clientEndpoint, IPEndPoint remoteServer)
        {
            _server = server;

            _isRunning = true;
            _remoteServer = remoteServer;
            _clientEndpoint = clientEndpoint;
            Console.WriteLine($"Established {clientEndpoint} => {remoteServer}");
            Run();
        }


        public readonly System.Net.Sockets.UdpClient client = new System.Net.Sockets.UdpClient();
        public DateTime lastActivity = DateTime.UtcNow;
        private readonly IPEndPoint _clientEndpoint;
        private readonly IPEndPoint _remoteServer;
        private bool _isRunning;
        private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();



        public async Task SendToServer(byte[] message)
        {
            lastActivity = DateTime.UtcNow;

            await _tcs.Task;
            var sent = await client.SendAsync(message, message.Length, _remoteServer);
            Console.WriteLine($"{sent} bytes sent from a client message of {message.Length} bytes from {_clientEndpoint} to {_remoteServer}");
        }

        private void Run()
        {

            Task.Run(async () =>
            {
                client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                _tcs.SetResult(true);
                using (client)
                {
                    while (_isRunning)
                    {
                        try
                        {
                            var result = await client.ReceiveAsync();
                            lastActivity = DateTime.UtcNow;
                            var sent = await _server.SendAsync(result.Buffer, result.Buffer.Length, _clientEndpoint);
                            Console.WriteLine($"{sent} bytes sent from a return message of {result.Buffer.Length} bytes from {_remoteServer} to {_clientEndpoint}");

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"An exception occurred while recieving a server datagram : {ex}");
                        }
                    }
                }

            });
        }

        public void Stop()
        {
            Console.WriteLine($"Closed {_clientEndpoint} => {_remoteServer}");
            _isRunning = false;
        }
    }
}
