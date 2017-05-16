using System;

namespace MigrationTool
{
    [Flags]
    public enum ImportMode
    {
        ImportTemplates = 1,
        ImportAdvertisements = 2,
        ImportPositions = 4,
        ImportAll = ImportTemplates | ImportPositions | ImportAdvertisements,
        TruncatedImportAdvertisements = 8,
        TruncatedImportAll = ImportTemplates | ImportPositions | TruncatedImportAdvertisements,
        DownloadImagesWithIncorrectFormat = 16
    }
}
