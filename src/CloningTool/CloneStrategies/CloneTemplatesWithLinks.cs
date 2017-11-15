using System.Threading.Tasks;

namespace CloningTool.CloneStrategies
{
    public class CloneTemplatesWithLinks : ICloneStrategy
    {
        private readonly ICloneStrategy _composite;

        public CloneTemplatesWithLinks(ICloneStrategyProvider cloneStrategyProvider)
        {
            _composite = new CompositeCloneStrategy(cloneStrategyProvider.GetCloneStrategy(CloneMode.CloneTemplates),
                                                    cloneStrategyProvider.GetCloneStrategy(CloneMode.CloneContentPositionsLinks));
        }

        public async Task<bool> ExecuteAsync() => await _composite.ExecuteAsync();
    }
}
