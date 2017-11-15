using System;

using Autofac.Features.Indexed;

using CloningTool.CloneStrategies;

namespace CloningTool
{
    public class CloneStrategyProvider : ICloneStrategyProvider
    {
        public IIndex<CloneMode, ICloneStrategy> Strategies { get; }

        public CloneStrategyProvider(IIndex<CloneMode, ICloneStrategy> strategies)
        {
            Strategies = strategies;
        }

        public ICloneStrategy GetCloneStrategy(CloneMode mode)
        {
            if (!Strategies.TryGetValue(mode, out var strategy))
            {
                throw new NotSupportedException($"Unknown strategy for mode {mode}");
            }

            return strategy;
        }
    }
}
