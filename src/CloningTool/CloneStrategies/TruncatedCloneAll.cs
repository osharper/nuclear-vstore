using System.Threading.Tasks;

namespace CloningTool.CloneStrategies
{
    public class TruncatedCloneAll : ICloneStrategy
    {
        private readonly ICloneStrategy _composite;

        public TruncatedCloneAll(ICloneStrategyProvider cloneStrategyProvider)
        {
            _composite = new CompositeCloneStrategy(cloneStrategyProvider.GetCloneStrategy(CloneMode.CloneTemplates),
                                                    cloneStrategyProvider.GetCloneStrategy(CloneMode.CloneContentPositionsLinks),
                                                    cloneStrategyProvider.GetCloneStrategy(CloneMode.TruncatedCloneAdvertisements));
        }

        public async Task<bool> ExecuteAsync() => await _composite.ExecuteAsync();
    }
}
