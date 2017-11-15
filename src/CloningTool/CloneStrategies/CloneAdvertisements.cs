using CloningTool.RestClient;

using Microsoft.Extensions.Logging;

namespace CloningTool.CloneStrategies
{
    public class CloneAdvertisements : CloneAdvertisementsBase
    {
        public CloneAdvertisements(CloningToolOptions options,
                                   IReadOnlyRestClientFacade sourceRestClient,
                                   IRestClientFacade destRestClient,
                                   ILogger<CloneAdvertisements> logger)
            : base(options, sourceRestClient, destRestClient, logger, false)
        {
        }
    }
}
