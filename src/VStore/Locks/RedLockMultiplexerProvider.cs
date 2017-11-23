using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
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
            _cancellationTokenSource.Cancel();
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
            var keepAlive = _lockOptions.KeepAlive ?? DefaultKeepAlive;
            var logWriter = new LogWriter(_logger);
            foreach (var endpoint in endpoints)
            {
                _logger.LogInformation(
                    "{host}:{port} will be used as RedLock endpoint.",
                    endpoint.Host,
                    endpoint.Port,
                    endpoint.Port);

                var redisConfig = new ConfigurationOptions
                    {
                        DefaultVersion = new Version(4, 0),
                        AbortOnConnectFail = false,
                        Password = _lockOptions.Password,
                        ConnectTimeout = _lockOptions.ConnectionTimeout ?? DefaultConnectionTimeout,
                        SyncTimeout = _lockOptions.SyncTimeout ?? DefaultSyncTimeout,
                        KeepAlive = keepAlive,
                        // Time (seconds) to check configuration. This serves as a keep-alive for interactive sockets, if it is supported.
                        ConfigCheckSeconds = keepAlive
                    };
                redisConfig.EndPoints.Add(new DnsEndPoint(endpoint.Host, endpoint.Port));

                var multiplexer = ConnectionMultiplexer.Connect(redisConfig, logWriter);
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

            RunConnectionChecker(multiplexers, keepAlive);

            return multiplexers;
        }

        private void RunConnectionChecker(IList<RedLockMultiplexer> multiplexers, int keepAlive)
        {
            Task.Factory.StartNew(
                () =>
                    {
                        var connectionInfos = multiplexers
                            .Select(x => new
                                {
                                    EndPoint = x.ConnectionMultiplexer.GetEndPoints()[0],
                                    x.ConnectionMultiplexer.Configuration
                                })
                            .ToList();
                        while (true)
                        {
                            for (var i = 0; i < connectionInfos.Count; ++i)
                            {
                                var endpoint = connectionInfos[i].EndPoint;
                                var configuration = connectionInfos[i].Configuration;
                                var multiplexer = multiplexers[i].ConnectionMultiplexer;
                                try
                                {
                                    if (multiplexers[i].ConnectionMultiplexer.IsConnected)
                                    {
                                        _logger.LogTrace("Cheking endpoint {endpoint} for availablity.", GetFriendlyName(endpoint));
                                        var server = multiplexer.GetServer(endpoint);
                                        server.Ping();
                                        _logger.LogTrace("Cheking endpoint {endpoint} is available.", GetFriendlyName(endpoint));
                                    }
                                    else
                                    {
                                        _logger.LogWarning("RedLock endpoint {endpoint} is unavailable. Trying to reconnect.", GetFriendlyName(endpoint));
                                        multiplexer.Dispose();
                                        multiplexers[i] = ConnectionMultiplexer.Connect(configuration);
                                    }
                                }
                                catch
                                {
                                    multiplexer.Dispose();
                                }
                            }

                            Thread.Sleep(TimeSpan.FromSeconds(keepAlive));
                        }
                    },
                _cancellationTokenSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
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