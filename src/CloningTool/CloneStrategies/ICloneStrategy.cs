using System.Threading.Tasks;

namespace CloningTool.CloneStrategies
{
    public interface ICloneStrategy
    {
        Task<bool> ExecuteAsync();
    }
}
