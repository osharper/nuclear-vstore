using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Options;

using RedLockNet.SERedis.Configuration;

using StackExchange.Redis;

namespace NuClear.VStore.Locks
{
    public sealed class RedLockMultiplexerProvider : IDisposable
    {
        private const int DefaultConnectionTimeout = 1000;
        private const int DefaultSyncTimeout = 1000;

        private readonly object _syncRoot = new object();
        private readonly DistributedLockOptions _lockOptions;
        private readonly ILogger<RedLockMultiplexerProvider> _logger;

        private readonly IList<RedLockMultiplexer> _multiplexers;

        public RedLockMultiplexerProvider(DistributedLockOptions lockOptions, ILogger<RedLockMultiplexerProvider> logger)
        {
            _lockOptions = lockOptions;
            _logger = logger;

            if (_multiplexers == null)
            {
                lock (_syncRoot)
                {
                    if (_multiplexers == null)
                    {
                        _multiplexers = Initialize();
                    }
                }
            }
        }

        public IList<RedLockMultiplexer> Get() => _multiplexers;

        public void Dispose()
        {
            if (_multiplexers != null)
            {
                foreach (var multiplexer in _multiplexers)
                {
                    multiplexer.ConnectionMultiplexer.Dispose();
                }
            }
        }

        private IList<RedLockMultiplexer> Initialize()
        {
            var endpoints = _lockOptions.GetEndPoints();
            if (endpoints == null || !endpoints.Any())
            {
                throw new ArgumentException("No endpoints specified");
            }

            var multiplexers = new List<RedLockMultiplexer>(endpoints.Count);
            foreach (var endpoint in endpoints)
            {
                _logger.LogInformation(
                    "{host}:{port} will be used as RedLock endpoint.",
                    endpoint.Host,
                    endpoint.Port,
                    endpoint.Port);

                var redisConfig = new ConfigurationOptions
                    {
                        AbortOnConnectFail = false,
                        Password = _lockOptions.Password,
                        ConnectTimeout = _lockOptions.ConnectionTimeout ?? DefaultConnectionTimeout,
                        SyncTimeout = _lockOptions.SyncTimeout ?? DefaultSyncTimeout,
                        KeepAlive = 1
                    };
                redisConfig.EndPoints.Add(new DnsEndPoint(endpoint.Host, endpoint.Port));

                var multiplexer = ConnectionMultiplexer.Connect(redisConfig);
                multiplexer.ConnectionFailed +=
                    (sender, args) =>
                        {
                            _logger.LogWarning(
                                $"ConnectionFailed: {GetFriendlyName(args.EndPoint)} ConnectionType: {args.ConnectionType} FailureType: {args.FailureType}");
                        };

                multiplexer.ConnectionRestored +=
                    (sender, args) =>
                        {
                            _logger.LogWarning(
                                $"ConnectionRestored: {GetFriendlyName(args.EndPoint)} ConnectionType: {args.ConnectionType} FailureType: {args.FailureType}");
                        };

                multiplexer.ErrorMessage +=
                    (sender, args) =>
                        {
                            _logger.LogWarning($"ErrorMessage: {GetFriendlyName(args.EndPoint)} Message: {args.Message}");
                        };

                multiplexers.Add(multiplexer);
            }

            return multiplexers;
        }

        private static string GetFriendlyName(EndPoint endPoint)
        {
            switch (endPoint)
            {
                case DnsEndPoint dnsEndPoint:
                    return $"{dnsEndPoint.Host}:{dnsEndPoint.Port}";
                case IPEndPoint ipEndPoint:
                    return $"{ipEndPoint.Address}:{ipEndPoint.Port}";
            }

            return endPoint.ToString();
        }
    }
}