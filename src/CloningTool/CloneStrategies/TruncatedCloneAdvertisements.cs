using CloningTool.RestClient;

using Microsoft.Extensions.Logging;

namespace CloningTool.CloneStrategies
{
    public class TruncatedCloneAdvertisements : CloneAdvertisementsBase
    {
        public TruncatedCloneAdvertisements(CloningToolOptions options,
                                            IReadOnlyRestClientFacade sourceRestClient,
                                            IRestClientFacade destRestClient,
                                            ILogger<TruncatedCloneAdvertisements> logger)
            : base(options, sourceRestClient, destRestClient, logger, true)
        {
        }
    }
}
