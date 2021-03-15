#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace NetProxy
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                var configJson = System.IO.File.ReadAllText("config.json");
                var configs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ProxyConfig>>(configJson);
                if (configs == null)
                {
                    throw new Exception("configs is null");
                }

                Task.WhenAll(configs.Select(c =>
                {
                    var proxyName = c.Key;
                    var proxyConfig = c.Value;
                    var forwardPort = proxyConfig.forwardPort;
                    var localPort = proxyConfig.localPort;
                    var forwardIp = proxyConfig.forwardIp;
                    var localIp = proxyConfig.localIp;
                    try
                    {
                        if (forwardIp == null)
                        {
                            throw new Exception("forwardIp is null");
                        }
                        if (!forwardPort.HasValue)
                        {
                            throw new Exception("forwardPort is null");
                        }
                        if (!localPort.HasValue)
                        {
                            throw new Exception("localPort is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to start {proxyName} : {ex.Message}");
                        throw;
                    }

                    if (proxyConfig.protocol == "udp")
                    {
                        try
                        {
                            var proxy = new UdpProxy();
                            return proxy.Start(forwardIp, forwardPort.Value, localPort.Value, localIp);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to start {proxyName} : {ex.Message}");
                            throw;
                        }
                    }
                    else if (proxyConfig.protocol == "tcp")
                    {
                        try
                        {
                            var proxy = new TcpProxy();
                            return proxy.Start(forwardIp, forwardPort.Value, localPort.Value, localIp);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to start {proxyName} : {ex.Message}");
                            throw;
                        }
                    }
                    else
                    {
                        return Task.FromException(
                            new InvalidOperationException($"protocol not supported {proxyConfig.protocol}"));
                    }
                })).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred : {ex}");
            }
        }
    }

    public class ProxyConfig
    {
        public string? protocol { get; set; }
        public ushort? localPort { get; set; }
        public string? localIp { get; set; }
        public string? forwardIp { get; set; }
        public ushort? forwardPort { get; set; }
    }

    internal interface IProxy
    {
        Task Start(string remoteServerIp, ushort remoteServerPort, ushort localPort, string? localIp = null);
    }
}