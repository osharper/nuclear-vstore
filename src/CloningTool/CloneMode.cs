using System;

namespace CloningTool
{
    [Flags]
    public enum CloneMode
    {
        CloneTemplates = 1,
        CloneAdvertisements = 2,
        CloneContentPositionsLinks = 4,
        CloneTemplatesWithLinks = CloneTemplates | CloneContentPositionsLinks,
        CloneAll = CloneTemplates | CloneContentPositionsLinks | CloneAdvertisements,
        TruncatedCloneAdvertisements = 8,
        TruncatedCloneAll = CloneTemplates | CloneContentPositionsLinks | TruncatedCloneAdvertisements,
        ReloadFiles = 16
    }
}
