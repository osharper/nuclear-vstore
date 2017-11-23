using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

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
        private const int DefaultKeepAlive = 1;

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
                        AbortOnConnectFail = true,
                        Password = _lockOptions.Password,
                        AllowAdmin = true,
                        ConnectTimeout = _lockOptions.ConnectionTimeout ?? DefaultConnectionTimeout,
                        SyncTimeout = _lockOptions.SyncTimeout ?? DefaultSyncTimeout,
                        KeepAlive = _lockOptions.KeepAlive ?? DefaultKeepAlive,
                        // Time (seconds) to check configuration. This serves as a keep-alive for interactive sockets, if it is supported.
                        ConfigCheckSeconds = _lockOptions.KeepAlive ?? DefaultKeepAlive,
                    };
                redisConfig.EndPoints.Add(new DnsEndPoint(endpoint.Host, endpoint.Port));

                var multiplexer = ConnectionMultiplexer.Connect(redisConfig, new LogWriter(_logger));
                multiplexer.ConnectionFailed +=
                    (sender, args) =>
                        {
                            _logger.LogWarning(
                                args.Exception,
                                "ConnectionFailed: {endpoint} ConnectionType: {connectionType} FailureType: {failureType}",
                                GetFriendlyName(args.EndPoint),
                                args.ConnectionType,
                                args.FailureType);
                        };

                multiplexer.ConnectionRestored +=
                    (sender, args) =>
                        {
                            _logger.LogWarning(
                                args.Exception,
                                "ConnectionRestored: {endpoint} ConnectionType: {connectionType} FailureType: {failureType}",
                                GetFriendlyName(args.EndPoint),
                                args.ConnectionType,
                                args.FailureType);
                        };

                multiplexer.InternalError +=
                    (sender, args) =>
                        {
                            _logger.LogWarning(
                                args.Exception,
                                "InternalError: {endpoint} ConnectionType: {connectionType} Origin: {origin}",
                                GetFriendlyName(args.EndPoint),
                                args.ConnectionType,
                                args.Origin);
                        };

                multiplexer.ErrorMessage +=
                    (sender, args) =>
                        {
                            _logger.LogWarning("ErrorMessage: {endpoint} Message: {message}", GetFriendlyName(args.EndPoint), args.Message);
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

        private class LogWriter : TextWriter
        {
            private readonly ILogger _logger;

            public LogWriter(ILogger logger)
            {
                _logger = logger;
            }

            public override Encoding Encoding => Encoding.UTF8;

            public override void WriteLine(string value) => _logger.LogTrace(value);
            public override void WriteLine(string format, object arg0) => _logger.LogTrace(format, arg0);
            public override void WriteLine(string format, params object[] arg) => _logger.LogTrace(format, arg);
        }
    }
}