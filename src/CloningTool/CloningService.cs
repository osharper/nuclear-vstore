using System;
using System.Threading.Tasks;

using CloningTool.RestClient;

namespace CloningTool
{
    public class CloningService
    {
        private readonly int _initialPingInterval;
        private readonly int _initialPingTries;
        private readonly ICloneStrategyProvider _strategyProvider;

        public CloningService(
            CloningToolOptions options,
            IReadOnlyRestClientFacade sourceRepository,
            IRestClientFacade destRepository,
            ICloneStrategyProvider strategyProvider)
        {
            if (options.MaxDegreeOfParallelism < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options.MaxDegreeOfParallelism));
            }

            _initialPingInterval = options.InitialPingInterval;
            _initialPingTries = options.InitialPingTries;
            SourceRepository = sourceRepository;
            DestRepository = destRepository;
            _strategyProvider = strategyProvider;
        }

        private IReadOnlyRestClientFacade SourceRepository { get; }

        private IRestClientFacade DestRepository { get; }

        public async Task<bool> CloneAsync(CloneMode mode)
        {
            await SourceRepository.EnsureApiAvailableAsync(_initialPingInterval, _initialPingTries);
            await DestRepository.EnsureApiAvailableAsync(_initialPingInterval, _initialPingTries);

            var strategy = _strategyProvider.GetCloneStrategy(mode);
            return await strategy.ExecuteAsync();
        }
    }
}
