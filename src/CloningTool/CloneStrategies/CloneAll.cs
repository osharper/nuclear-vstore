using System.Threading.Tasks;

namespace CloningTool.CloneStrategies
{
    public class CloneAll : ICloneStrategy
    {
        private readonly ICloneStrategy _composite;

        public CloneAll(ICloneStrategyProvider cloneStrategyProvider)
        {
            _composite = new CompositeCloneStrategy(cloneStrategyProvider.GetCloneStrategy(CloneMode.CloneTemplates),
                                                    cloneStrategyProvider.GetCloneStrategy(CloneMode.CloneContentPositionsLinks),
                                                    cloneStrategyProvider.GetCloneStrategy(CloneMode.CloneAdvertisements));
        }

        public async Task<bool> ExecuteAsync() => await _composite.ExecuteAsync();
    }
}
