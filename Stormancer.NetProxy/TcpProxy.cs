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
    class TcpProxy: IProxy
    {
        public async Task Start(string remoteServerIp, ushort remoteServerPort, ushort localPort,string localIp)
        {
            //var clients = new ConcurrentDictionary<IPEndPoint, TcpClient>();

            IPAddress localIpAddress = string.IsNullOrEmpty(localIp) ? IPAddress.IPv6Any : IPAddress.Parse(localIp);
            var server = new System.Net.Sockets.TcpListener(new IPEndPoint(localIpAddress, localPort));
            server.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            server.Start();

            Console.WriteLine($"TCP proxy started {localPort} -> {remoteServerIp}|{remoteServerPort}");
            while (true)
            {

                try
                {
                    var remoteClient = await server.AcceptTcpClientAsync();
                    remoteClient.NoDelay = true;
                    var ips = await Dns.GetHostAddressesAsync(remoteServerIp);

                    new TcpClient(remoteClient, new IPEndPoint(ips.First(), remoteServerPort));


                }
                catch (Exception ex) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex);
                    Console.ResetColor();
                }

            }
        }
    }

    class TcpClient
    {
        private System.Net.Sockets.TcpClient _remoteClient;
        private IPEndPoint _clientEndpoint;
        private IPEndPoint _remoteServer;

        public TcpClient(System.Net.Sockets.TcpClient remoteClient, IPEndPoint remoteServer)
        {
            _remoteClient = remoteClient;

          
            _remoteServer = remoteServer;
            client.NoDelay = true;
            _clientEndpoint = (IPEndPoint)_remoteClient.Client.RemoteEndPoint;
            Console.WriteLine($"Established {_clientEndpoint} => {remoteServer}");
            Run();
        }


        public System.Net.Sockets.TcpClient client = new System.Net.Sockets.TcpClient();

      

        private void Run()
        {
            
            Task.Run(async () =>
            {
                try
                {
                    using (_remoteClient)
                    using (client)
                    {
                        await client.ConnectAsync(_remoteServer.Address, _remoteServer.Port);
                        var serverStream = client.GetStream();
                        var remoteStream = _remoteClient.GetStream();

                        await Task.WhenAny(remoteStream.CopyToAsync(serverStream), serverStream.CopyToAsync(remoteStream));



                    }
                }
                catch (Exception) { }
                finally
                {
                    Console.WriteLine($"Closed {_clientEndpoint} => {_remoteServer}");
                    _remoteClient = null;
                }
            });
        }

    
    }
}
