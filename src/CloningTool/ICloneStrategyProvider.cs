using CloningTool.CloneStrategies;

namespace CloningTool
{
    public interface ICloneStrategyProvider
    {
        ICloneStrategy GetCloneStrategy(CloneMode mode);
    }
}
