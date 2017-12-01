using System.Collections.Generic;
using System.Threading.Tasks;

namespace CloningTool.CloneStrategies
{
    public class CompositeCloneStrategy : ICloneStrategy
    {
        private readonly IReadOnlyList<ICloneStrategy> _strategies;

        public CompositeCloneStrategy(params ICloneStrategy[] strategy)
        {
            _strategies = new List<ICloneStrategy>(strategy);
        }

        public async Task<bool> ExecuteAsync()
        {
            foreach (var strategy in _strategies)
            {
                if (!await strategy.ExecuteAsync())
                {
                    return false;
                }
            }

            return true;
        }
    }
}
