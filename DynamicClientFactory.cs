using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Timers;

namespace Shared.DataOp
{
    internal class DynamicHttpClientFactory
    {
        private static readonly ConcurrentDictionary<string, ClientInfo> DomainClientMap
            = new ConcurrentDictionary<string, ClientInfo>();

        private static readonly Timer DisposeTimer = new Timer();

        private static readonly object Locker = new object();

        static DynamicHttpClientFactory()
        {
            DisposeTimer.Elapsed += DisposeUnusedClients;
            DisposeTimer.Interval = new TimeSpan(0, 2, 0).TotalMilliseconds;
        }

        internal static HttpClient CreateClient(ClientCreationArgs args)
        {
            lock (Locker)
            {
                if (!DomainClientMap.ContainsKey(args.BaseAddress))
                {
                    DomainClientMap.TryAdd(args.BaseAddress, CreateClientInfo(args));
                }
                if(DomainClientMap.Count == 1) EnableDisposeTimer();
                return DomainClientMap[args.BaseAddress].GetClient();
            }
        }

        private static void EnableDisposeTimer()
        {
            lock (Locker) DisposeTimer.Enabled = true;
        }

        private static void DisableDisposeTimer()
        {
            lock (Locker) DisposeTimer.Enabled = false;
        }

        private static void DisposeUnusedClients(object source, ElapsedEventArgs e)
        {
            foreach (var (baseAddress, clientInfo) in DomainClientMap)
            {
                if (DateTime.UtcNow.Subtract(clientInfo.LastUsedOn) <= new TimeSpan(0, 1, 0)) continue;
                DomainClientMap.TryRemove(baseAddress, out _);
                clientInfo.DisposeClient();
            }
            if(DomainClientMap.IsEmpty) DisableDisposeTimer();
        }

        private static ClientInfo CreateClientInfo(ClientCreationArgs args)
            => new ClientInfo(
                new HttpClient(CreateClientHandler(args))
                {
                    BaseAddress = new Uri(args.BaseAddress)
                }
            );

        private static HttpClientHandler CreateClientHandler(ClientCreationArgs args)
        {
            var handler = new HttpClientHandler();
            // ...
            return handler;
        }

        private class ClientInfo
        {
            private readonly HttpClient _httpClient;
            public ClientInfo(HttpClient httpClient)
            {
                _httpClient = httpClient;
                LastUsedOn = DateTime.UtcNow;
            }
            public DateTime LastUsedOn { get; private set; }
            public HttpClient GetClient()
            {
                LastUsedOn = DateTime.UtcNow;
                return _httpClient;
            }
            public void DisposeClient()
            {
                _httpClient.Dispose();
            }
        }
    }
}
