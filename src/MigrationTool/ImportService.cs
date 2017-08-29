using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ImageSharp;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MigrationTool.Json;
using MigrationTool.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;
using NuClear.VStore.Objects;

using File = System.IO.File;

namespace MigrationTool
{
    public class ImportService
    {
        private readonly DateTime _thresholdDate;
        private readonly DateTime _positionsBeginDate;
        private readonly Language _language;
        private readonly string _languageCode;
        private readonly int _batchSize;
        private readonly int _maxDegreeOfParallelism;
        private readonly int _truncatedImportSize;
        private readonly int _maxImportTries;
        private readonly int? _destOrganizationUnitBranchCode;
        private readonly DbContextOptions<ErmContext> _contextOptions;

        private readonly IDictionary<long, long> _instanceTemplatesMap;
        private readonly JTokenEqualityComparer _jsonEqualityComparer = new JTokenEqualityComparer();
        private readonly ConcurrentDictionary<Tuple<long, int>, long> _templateElementsMap = new ConcurrentDictionary<Tuple<long, int>, long>();
        private readonly ILogger<ImportService> _logger;

        private long _uploadedBinariesCount;

        public ImportService(
            DbContextOptions<ErmContext> contextOptions,
            Language language,
            Options options,
            IDictionary<long, long> instanceTemplatesMap,
            ApiRepository repository,
            ConverterService converter,
            ILogger<ImportService> logger)
        {
            if (options.MaxDegreeOfParallelism < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options.MaxDegreeOfParallelism));
            }

            _thresholdDate = options.ThresholdDate;
            _positionsBeginDate = options.PositionsBeginDate;
            _maxImportTries = options.MaxImportTries;
            _maxDegreeOfParallelism = options.MaxDegreeOfParallelism;
            _truncatedImportSize = options.TruncatedImportSize;
            _batchSize = options.AdvertisementsImportBatchSize;
            _contextOptions = contextOptions;
            _language = language;
            _instanceTemplatesMap = instanceTemplatesMap;
            _languageCode = language.ToString().ToLowerInvariant();
            _logger = logger;
            _destOrganizationUnitBranchCode = options.DestOrganizationUnitBranchCode;
            Repository = repository;
            Converter = converter;
        }

        private ErmContext GetNewContext() => new ErmContext(_contextOptions);

        private ApiRepository Repository { get; }

        private ConverterService Converter { get; }

        public async Task<bool> ImportAsync(ImportMode mode)
        {
            var templateIds = _instanceTemplatesMap.Keys.ToList();
            switch (mode)
            {
                case ImportMode.ImportTemplates:
                    return await ImportTemplatesAsync(templateIds);
                case ImportMode.ImportPositions:
                    return await ImportPositionsAsync();
                case ImportMode.ImportAdvertisements:
                    return await ImportAdvertisementsAsync(templateIds);
                case ImportMode.ImportAll:
                    return await ImportTemplatesAsync(templateIds) &&
                           await ImportPositionsAsync() &&
                           await ImportAdvertisementsAsync(templateIds);
                case ImportMode.TruncatedImportAdvertisements:
                    return await DemoImportAdvertisementsAsync(templateIds);
                case ImportMode.TruncatedImportAll:
                    return await ImportTemplatesAsync(templateIds) &&
                           await ImportPositionsAsync() &&
                           await DemoImportAdvertisementsAsync(templateIds);
                case ImportMode.DownloadImagesWithIncorrectFormat:
                    return await DownloadImagesWithIncorrectFormatAsync();
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown import mode");
            }
        }

        private async Task<bool> ImportPositionsAsync()
        {
            IReadOnlyCollection<Position> positions;
            using (var context = GetNewContext())
            {
                // Get positions from current pricelist:
                var positionIds = await(from p in context.Prices
                                        join pp in context.PricePositions on p.Id equals pp.PriceId
                                        join pos in context.Positions on pp.PositionId equals pos.Id
                                        where !p.IsDeleted
                                              && p.BeginDate >= _positionsBeginDate
                                              && !pp.IsDeleted
                                              && !pos.IsDeleted
                                        select pos.Id)
                                      // And from orders in migration:
                                      .Union(from o in context.Orders
                                             join op in context.OrderPositions on o.Id equals op.OrderId
                                             join pp in context.PricePositions on op.PricePositionId equals pp.Id
                                             join p in context.Positions on pp.PositionId equals p.Id
                                             where !o.IsDeleted
                                                   && o.IsActive
                                                   && (o.WorkflowStepId == 6 && o.EndDistributionDateFact >= _thresholdDate || // Archived orders that were placed
                                                       o.WorkflowStepId != 6 && o.EndDistributionDateFact > DateTime.UtcNow)   // Future and current orders that are not in the archive
                                                   && !op.IsDeleted
                                                   && op.IsActive
                                                   && !pp.IsDeleted
                                                   && !p.IsDeleted
                                             select p.Id)
                                      .Distinct()
                                      .ToListAsync();

                // Find child positions:
                positionIds.AddRange(
                    await(from pc in context.PositionChildren
                           join pos in context.Positions on pc.ChildPositionId equals pos.Id
                           where positionIds.Contains(pc.MasterPositionId)
                                 && !pos.IsDeleted
                          select pos.Id)
                        .Distinct()
                        .ToListAsync());

                positions = await context.Positions
                    .Where(p => positionIds.Contains(p.Id) && p.AdvertisementTemplateId != null)
                    .Include(p => p.AdvertisementTemplate)
                    .ToListAsync();
            }

            var existedPositions = (await Repository.GetPositionsAsync()).ToDictionary(p => p.Id);

            var importedCount = 0L;
            var failedIds = new ConcurrentBag<long>();
            await ParallelImport(positions,
                                 async position =>
                                     {
                                         var positionDef = new
                                             {
                                                 position.Id,
                                                 position.Name,
                                                 TemplateId = position.AdvertisementTemplateId,
                                                 TemplateName = position.AdvertisementTemplate?.Name
                                             }.ToString();

                                         try
                                         {
                                             PositionDescriptor apiPosition = null;
                                             if (existedPositions.ContainsKey(position.Id))
                                             {
                                                 _logger.LogInformation("Position {position} already exists, skip creation", positionDef);
                                                 EnsurePositionsAreEqual(existedPositions[position.Id], position);
                                                 apiPosition = existedPositions[position.Id];
                                             }
                                             else
                                             {
                                                 await ImportPositionAsync(position);
                                             }

                                             await ImportPositionLinkAsync(position, apiPosition);
                                             _logger.LogInformation("Position import succeeded: {position}", positionDef);
                                             Interlocked.Increment(ref importedCount);
                                         }
                                         catch (Exception ex)
                                         {
                                             failedIds.Add(position.Id);
                                             _logger.LogError(new EventId(), ex, "Position import error: {position}", positionDef);
                                         }
                                     });

            _logger.LogInformation("Imported positions: {imported} of {total}", importedCount.ToString(), positions.Count.ToString());
            if (failedIds.Count > 0)
            {
                _logger.LogWarning("Id's of failed positions: {list}", failedIds);
                return false;
            }

            return true;
        }

        private void EnsurePositionsAreEqual(PositionDescriptor apiPosition, Position ermPosition)
        {
            if (apiPosition.Template == null)
            {
                return;
            }

            if (ermPosition.AdvertisementTemplateId.HasValue &&
                _instanceTemplatesMap.ContainsKey(ermPosition.AdvertisementTemplateId.Value) &&
                apiPosition.Template.Id != _instanceTemplatesMap[ermPosition.AdvertisementTemplateId.Value])
            {
                var apiObject = JsonConvert.SerializeObject(apiPosition);
                var ermObject = JsonConvert.SerializeObject(new PositionDescriptor
                {
                    Id = ermPosition.Id,
                    Name = ermPosition.Name,
                    IsContentSales = ermPosition.IsContentSales,
                    Template = new PositionDescriptor.TemplateDescriptor { Id = ermPosition.AdvertisementTemplateId.Value, Name = ermPosition.AdvertisementTemplate.Name },
                    IsDeleted = ermPosition.IsDeleted
                });
                throw new InvalidOperationException("Positions are not equal: from API " + apiObject + " and from ERM " + ermObject);
            }
        }

        private async Task ImportPositionAsync(Position position)
        {
            var positionId = position.Id.ToString();
            var positionDescriptor = new
                {
                    code = position.Id,
                    defaultName = position.Name,
                    isContentSales = position.IsContentSales,
                    isDeleted = position.IsDeleted
                };

            await Repository.CreatePositionAsync(positionId, positionDescriptor);
        }

        private async Task ImportPositionLinkAsync(Position ermPosition, PositionDescriptor apiPosition)
        {
            var templateId = ermPosition.AdvertisementTemplateId;
            if (templateId.HasValue)
            {
                var positionId = ermPosition.Id.ToString();
                if (!_instanceTemplatesMap.ContainsKey(templateId.Value))
                {
                    _logger.LogWarning("Can't create link between position {id} and template {template}: template doesn't present in config!",
                                       positionId,
                                       templateId.Value.ToString());
                    return;
                }

                var mappedId = _instanceTemplatesMap[templateId.Value];
                if (apiPosition?.Template?.Id == mappedId)
                {
                    _logger.LogInformation("Link between position {id} and template {template} (mapped {mappedId}) already exists, skip linking",
                                           positionId,
                                           templateId.Value.ToString(),
                                           mappedId);
                    return;
                }

                _logger.LogInformation("Creating link between position {id} and template {template} (mapped {mappedId})",
                                       positionId,
                                       templateId.Value.ToString(),
                                       mappedId);
                await Repository.CreatePositionTemplateLinkAsync(positionId, mappedId.ToString());
            }
        }

        private async Task<bool> DownloadImagesWithIncorrectFormatAsync()
        {
            IReadOnlyCollection<Models.File> files;
            using (var context = GetNewContext())
            {
                context.Database.SetCommandTimeout(60000);

                files = await (from o in context.Orders
                               join op in context.OrderPositions on o.Id equals op.OrderId
                               join opa in context.OrderPositionAdvertisement on op.Id equals opa.OrderPositionId
                               join adv in context.Advertisements on opa.AdvertisementId equals adv.Id
                               join ae in context.AdvertisementElements on adv.Id equals ae.AdvertisementId
                               join t in context.AdvertisementTemplates on adv.AdvertisementTemplateId equals t.Id
                               join aet in context.AdvertisementElementTemplates on ae.AdvertisementElementTemplateId equals aet.Id
                               join f in context.Files on ae.FileId equals f.Id
                               where !o.IsDeleted
                                     && o.IsActive
                                     && (o.WorkflowStepId == 6 && o.EndDistributionDateFact >= _thresholdDate ||  // Archived orders that were placed
                                         o.WorkflowStepId != 6 && o.EndDistributionDateFact > DateTime.UtcNow)    // Future and current orders that are not in the archive
                                     && !op.IsDeleted
                                     && op.IsActive
                                     && !adv.IsDeleted
                                     && !ae.IsDeleted && !aet.IsDeleted
                                     && aet.RestrictionType == ElementRestrictionType.Image
                                     && !aet.FileExtensionRestriction.Contains(f.ContentType.Replace("image/x-", string.Empty).Replace("image/", string.Empty))
                               select f)
                            .Distinct()
                            .ToListAsync();
            }

            var dirPath = $@".{Path.DirectorySeparatorChar.ToString()}download";
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            foreach (var file in files)
            {
                // Save original image:
                var fileName = "file_" + file.Id.ToString() + "." + file.ContentType.Replace("image/x-", string.Empty).Replace("image/", string.Empty);
                using (var localFile = File.Create(Path.Combine(dirPath, fileName)))
                {
                    localFile.Write(file.Data, 0, file.Data.Length);
                }

                // Convert image to png:
                using (var image = Image.Load(file.Data))
                {
                    var newFileName = "file_" + file.Id.ToString() + ".png";
                    using (var stream = File.Create(Path.Combine(dirPath, newFileName)))
                    {
                        image.SaveAsPng(stream);
                    }
                }
            }

            _logger.LogInformation("Total {count} images with incorrect format were downloaded and converted", files.Count.ToString());
            return true;
        }

        private async Task<bool> ImportTemplatesAsync(IReadOnlyCollection<long> templateIds)
        {
            IReadOnlyCollection<AdvertisementTemplate> templates;
            using (var context = GetNewContext())
            {
                var positions = await (from p in context.Prices
                                       join pp in context.PricePositions on p.Id equals pp.PriceId
                                       join pos in context.Positions on pp.PositionId equals pos.Id
                                       where !p.IsDeleted
                                             && p.BeginDate >= _positionsBeginDate
                                             && !pp.IsDeleted && pp.IsActive
                                             && !pos.IsDeleted
                                       select pos.Id)
                                    .Distinct()
                                    .ToListAsync();

                positions.AddRange(
                    await(from pc in context.PositionChildren
                           join pos in context.Positions on pc.ChildPositionId equals pos.Id
                           where positions.Contains(pc.MasterPositionId)
                                 && !pos.IsDeleted
                           select pc.ChildPositionId)
                        .Distinct()
                        .ToListAsync());

                var positionsTemplates = await (from pos in context.Positions
                                                join t in context.AdvertisementTemplates on pos.AdvertisementTemplateId equals t.Id
                                                where !t.IsDeleted && positions.Contains(pos.Id)
                                                select new { t.Id, t.Name })
                                             .Distinct()
                                             .ToListAsync();

                if (positionsTemplates.Any(pt => !templateIds.Contains(pt.Id)))
                {
                    _logger.LogWarning("Some templates are not listed in merge file: {missedTemplates}", positionsTemplates.Where(pt => !templateIds.Contains(pt.Id)));
                }

                templates = await context.AdvertisementTemplates
                                         .Where(t => templateIds.Contains(t.Id))
                                         .Include(t => t.ElementTemplatesLink)
                                            .ThenInclude(link => link.ElementTemplate)
                                         .ToArrayAsync();
            }

            long importedCount = 0;
            var failedIds = new ConcurrentBag<long>();
            await ParallelImport(templates,
                                 async template =>
                                     {
                                         var templateDef = new { template.Id, MappedId = _instanceTemplatesMap[template.Id], template.Name }.ToString();
                                         try
                                         {
                                             await ImportTemplateAsync(template);
                                             _logger.LogInformation("Template import succeeded: {template}", templateDef);
                                             Interlocked.Increment(ref importedCount);
                                         }
                                         catch (Exception ex)
                                         {
                                             failedIds.Add(template.Id);
                                             _logger.LogError(new EventId(), ex, "Template import error: {template}", templateDef);
                                         }
                                     });

            _logger.LogInformation("Imported templates: {imported} of {total}", importedCount.ToString(), templates.Count.ToString());
            if (failedIds.Count > 0)
            {
                _logger.LogWarning("Id's of failed templates: {list}", failedIds);
                return false;
            }

            return true;
        }

        private async Task<bool> DemoImportAdvertisementsAsync(IReadOnlyCollection<long> templateIds)
        {
            _uploadedBinariesCount = 0L;
            var advertisementIds = new List<long>(templateIds.Count * _truncatedImportSize);
            using (var context = GetNewContext())
            {
                foreach (var templateId in templateIds)
                {
                    var portion = await (from o in context.Orders
                                         join op in context.OrderPositions on o.Id equals op.OrderId
                                         join opa in context.OrderPositionAdvertisement on op.Id equals opa.OrderPositionId
                                         join adv in context.Advertisements on opa.AdvertisementId equals adv.Id
                                         where !o.IsDeleted
                                               && o.IsActive
                                               && (o.WorkflowStepId == 6 && o.EndDistributionDateFact >= _thresholdDate ||  // Archived orders that were placed
                                                   o.WorkflowStepId != 6 && o.EndDistributionDateFact > DateTime.UtcNow)    // Future and current orders that are not in the archive
                                               && !op.IsDeleted
                                               && op.IsActive
                                               && !adv.IsDeleted
                                               && adv.AdvertisementTemplateId == templateId
                                               && adv.FirmId != null    // Do not import stubs
                                         orderby o.ApprovalDate descending
                                         select adv.Id)
                                      .Distinct()
                                      .Take(_truncatedImportSize)
                                      .ToListAsync();

                    advertisementIds.AddRange(portion);
                }
            }

            var failedAds = await ImportAdvertisementsByIdsAsync(advertisementIds);
            _logger.LogInformation("Total uploaded binaries: {totalBinaries}", _uploadedBinariesCount);
            return failedAds.Count == 0;
        }

        private async Task<bool> ImportAdvertisementsAsync(IReadOnlyCollection<long> templateIds)
        {
            long[] advertisementsToImport;
            using (var context = GetNewContext())
            {
                OrganizationUnit organizationUnit = null;
                if (_destOrganizationUnitBranchCode != null)
                {
                    organizationUnit = await context.OrganizationUnits.SingleOrDefaultAsync(ou => ou.DgppId == _destOrganizationUnitBranchCode.Value);
                    if (organizationUnit == null)
                    {
                        throw new InvalidOperationException($"There are no organization unit with branch code {_destOrganizationUnitBranchCode}");
                    }

                    _logger.LogInformation("Import orders to organization unit {name} with branch code {code}", organizationUnit.Name, organizationUnit.DgppId);
                }

                var advertisementIds = await (from o in context.Orders
                                              join op in context.OrderPositions on o.Id equals op.OrderId
                                              join opa in context.OrderPositionAdvertisement on op.Id equals opa.OrderPositionId
                                              join adv in context.Advertisements on opa.AdvertisementId equals adv.Id
                                              where !o.IsDeleted
                                                    && o.IsActive
                                                    && (o.WorkflowStepId == 6 && o.EndDistributionDateFact >= _thresholdDate || // Archived orders that were placed
                                                        o.WorkflowStepId != 6 && o.EndDistributionDateFact > DateTime.UtcNow)   // Future and current orders that are not in the archive
                                                    && !op.IsDeleted
                                                    && op.IsActive
                                                    && !adv.IsDeleted
                                                    && adv.FirmId != null // Do not import stubs
                                                    && (organizationUnit == null || o.DestOrganizationUnitId == organizationUnit.Id)
                                              select new { adv.Id, TemplateId = adv.AdvertisementTemplateId })
                                           .Distinct()
                                           .ToListAsync();

                var missedTemplatesIds = advertisementIds.Select(adv => adv.TemplateId)
                                                         .Distinct()
                                                         .Except(templateIds)
                                                         .ToList();
                if (missedTemplatesIds.Count > 0)
                {
                    var missedTemplates = await context.AdvertisementTemplates
                                                       .Where(t => missedTemplatesIds.Contains(t.Id))
                                                       .Select(t => new { t.Id, t.Name })
                                                       .ToListAsync();

                    _logger.LogWarning("There are advertisements with next missed templates: {list}", missedTemplates);
                }

                advertisementsToImport = advertisementIds.Where(adv => templateIds.Contains(adv.TemplateId))
                                                         .Select(adv => adv.Id)
                                                         .ToArray();
            }

            var importedCount = 0L;
            _uploadedBinariesCount = 0L;
            var failedAds = new List<Advertisement>();
            for (var segmentNum = 0; ; ++segmentNum)
            {
                var offset = segmentNum * _batchSize;
                var count = Math.Min(advertisementsToImport.Length - offset, _batchSize);
                if (count <= 0)
                {
                    break;
                }

                var segment = new ArraySegment<long>(advertisementsToImport, offset, count)
                    .ToList(); // EF doesn't build query with ArraySegment

                var failedAdsInBatch = await ImportAdvertisementsByIdsAsync(segment);
                importedCount += count - failedAdsInBatch.Count;
                if (failedAdsInBatch.Count > 0)
                {
                    failedAds.AddRange(failedAdsInBatch);
                    _logger.LogWarning("Id's of failed advertisements in batch: {list}", failedAdsInBatch.Select(a => a.Id));
                }
            }

            _logger.LogInformation("Total imported advertisements: {imported} of {total}", importedCount.ToString(), advertisementsToImport.Length.ToString());
            _logger.LogInformation("Total uploaded binaries: {totalBinaries}", _uploadedBinariesCount);

            // All advertisements were imported, check the failed ones:
            if (failedAds.Count > 0)
            {
                return await ImportFailedAdvertisements(failedAds);
            }

            return true;
        }

        /// <summary>
        /// Import advertisements by identifiers
        /// </summary>
        /// <param name="advIds">identifiers of advertisements to import</param>
        /// <returns>Identifiers of failed advertisements</returns>
        private async Task<IReadOnlyCollection<Advertisement>> ImportAdvertisementsByIdsAsync(IReadOnlyCollection<long> advIds)
        {
            var batchImportedCount = 0;
            var failedAds = new ConcurrentBag<Advertisement>();
            var advertisements = await GetAdvertisementsByIds(advIds);
            await ParallelImport(advertisements,
                                 async advertisement =>
                                     {
                                         try
                                         {
                                             await ImportAdvertisementAsync(advertisement);
                                             Interlocked.Increment(ref batchImportedCount);
                                         }
                                         catch (Exception ex)
                                         {
                                             failedAds.Add(advertisement);
                                             _logger.LogError(new EventId(), ex, "Advertisement {id} import error", advertisement.Id.ToString());
                                         }
                                     });

            _logger.LogInformation("Imported advertisements in batch: {imported} of {total}", batchImportedCount.ToString(), advIds.Count.ToString());
            return failedAds;
        }

        private async Task<IReadOnlyCollection<Advertisement>> GetAdvertisementsByIds(IReadOnlyCollection<long> ids)
        {
            using (var context = GetNewContext())
            {
                return await context.Advertisements
                                    .Where(a => ids.Contains(a.Id))
                                    .Include(a => a.AdvertisementElements)
                                        .ThenInclude(ae => ae.AdvertisementElementTemplate)
                                        .ThenInclude(aet => aet.AdsTemplatesAdsElementTemplates)
                                    .Include(a => a.AdvertisementElements)
                                        .ThenInclude(ae => ae.File)
                                    .Include(a => a.AdvertisementElements)
                                        .ThenInclude(ae => ae.AdvertisementElementStatus)
                                    .Include(a => a.AdvertisementElements)
                                        .ThenInclude(ae => ae.AdvertisementElementDenialReasons)
                                        .ThenInclude(aedr => aedr.DenialReason)
                                    .ToListAsync();
            }
        }

        private async Task<bool> ImportFailedAdvertisements(IReadOnlyCollection<Advertisement> failedAds)
        {
            _uploadedBinariesCount = 0;
            var imported = 0;
            var totallyFailedAds = new ConcurrentBag<long>();
            _logger.LogInformation("Start to import failed advertisements, total {count}", failedAds.Count.ToString());

            var partitioner = Partitioner.Create(failedAds);
            var tasks = partitioner.GetOrderablePartitions(_maxDegreeOfParallelism)
                                   .Select(async partition =>
                                               {
                                                   while (partition.MoveNext())
                                                   {
                                                       bool hasFailed;
                                                       var tries = 0;
                                                       var advertisement = partition.Current.Value;
                                                       do
                                                       {
                                                           try
                                                           {
                                                               ++tries;
                                                               await ImportAdvertisementAsync(advertisement);
                                                               Interlocked.Increment(ref imported);
                                                               hasFailed = false;
                                                           }
                                                           catch (Exception ex)
                                                           {
                                                               hasFailed = true;
                                                               _logger.LogError(new EventId(), ex, "Advertisement {id} repeated import error", advertisement.Id.ToString());
                                                               await Task.Delay(200);
                                                           }
                                                       }
                                                       while (hasFailed && tries < _maxImportTries);

                                                       if (hasFailed)
                                                       {
                                                           totallyFailedAds.Add(advertisement.Id);
                                                       }
                                                   }
                                               });
            await Task.WhenAll(tasks);

            _logger.LogInformation("Failed advertisements repeated import done, imported: {imported} of {total}", imported.ToString(), failedAds.Count.ToString());
            _logger.LogInformation("Total uploaded binaries during repeated import: {totalBinaries}", _uploadedBinariesCount);
            if (totallyFailedAds.Count < 1)
            {
                return true;
            }

            _logger.LogError("Failed advertisements after repeated import: {ids}", totallyFailedAds);
            return false;
        }

        private async Task ParallelImport<T>(IEnumerable<T> list, Func<T, Task> callback)
        {
            var partitioner = Partitioner.Create(list);
            var partitions = partitioner.GetOrderablePartitions(_maxDegreeOfParallelism)
                                        .Select(async partition =>
                                                    {
                                                        while (partition.MoveNext())
                                                        {
                                                            await callback(partition.Current.Value);
                                                        }
                                                    });
            await Task.WhenAll(partitions);
        }

        private async Task ImportAdvertisementAsync(Advertisement advertisement)
        {
            var versionId = string.Empty;
            var objectId = advertisement.Id.ToString();
            var objectDescriptor = await GenerateObjectDescriptorAsync(advertisement);
            try
            {
                versionId = await Repository.CreateObjectAsync(advertisement.Id, advertisement.FirmId.ToString(), objectDescriptor);
            }
            catch (ObjectAlreadyExistsException ex)
            {
                _logger.LogWarning(new EventId(), ex, "Object {id} already exists, try to continue execution", objectId);
            }

            if (advertisement.IsSelectedToWhiteList)
            {
                await Repository.SelectObjectToWhitelist(objectId);
            }

            var moderationStatus = Converter.GetAdvertisementModerationStatus(advertisement);
            if (moderationStatus.Status != ModerationStatus.OnApproval)
            {
                if (string.IsNullOrEmpty(versionId))
                {
                    _logger.LogWarning("VersionId for object {id} is unknown, need to get latest version", objectId);
                    versionId = await Repository.GetObjectVersionAsync(advertisement.Id);
                }

                await Repository.UpdateObjectModerationStatusAsync(objectId, versionId, moderationStatus);
            }
        }

        private async Task<ObjectDescriptor> GenerateObjectDescriptorAsync(Advertisement advertisement)
        {
            var templateId = _instanceTemplatesMap[advertisement.AdvertisementTemplateId];

            var newObject = await Repository.GetNewObjectAsync(templateId.ToString(), _languageCode, advertisement.FirmId.ToString());
            newObject.Properties[Tokens.NameToken] = advertisement.Name;
            newObject.Properties[Tokens.IsWhiteListedToken] = advertisement.IsSelectedToWhiteList;

            return new ObjectDescriptor
            {
                Id = advertisement.Id,
                Language = _language,
                TemplateId = templateId,
                TemplateVersionId = newObject.TemplateVersionId,
                Elements = await Task.WhenAll(
                                   advertisement.AdvertisementElements
                                                .Where(e => !e.IsDeleted)
                                                .Select(async e => await ConvertAdvertisementElementToObjectElementDescriptorAsync(e,
                                                    newObject.Elements.Single(x => x.TemplateCode == e.AdsTemplatesAdsElementTemplates.ExportCode)))
                                                .ToList()),
                Properties = newObject.Properties
            };
        }

        private async Task<IObjectElementDescriptor> ConvertAdvertisementElementToObjectElementDescriptorAsync(AdvertisementElement element, ApiObjectElementDescriptor newElem)
        {
            try
            {
                var elementType = Converter.ConvertRestrictionTypeToDescriptorType(element.AdvertisementElementTemplate);
                var elementValue = await GetElementDescriptorValueAsync(elementType, element, newElem);
                var elementDescriptor = GetObjectElementDescriptor(element, elementType, newElem);

                return new ObjectElementDescriptor
                    {
                        Id = element.Id,
                        Type = elementDescriptor.Type,
                        TemplateCode = elementDescriptor.TemplateCode,
                        Properties = elementDescriptor.Properties,
                        Constraints = elementDescriptor.Constraints,
                        Value = elementValue
                    };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    new EventId(),
                    ex,
                    "Convert object {objectId} element {elementId} error, template code {templateCode}",
                    element.AdvertisementId.ToString(),
                    element.Id.ToString(),
                    element.AdsTemplatesAdsElementTemplates.ExportCode.ToString());
                throw;
            }
        }

        private IElementDescriptor GetObjectElementDescriptor(AdvertisementElement element, ElementDescriptorType elementType, IElementDescriptor newElem)
        {
            var templateCode = element.AdsTemplatesAdsElementTemplates.ExportCode;

            return new ElementDescriptor(
                elementType,
                templateCode,
                newElem.Properties,
                newElem.Constraints);
        }

        private async Task ImportTemplateAsync(AdvertisementTemplate template)
        {
            var templateIdStr = template.Id.ToString();
            var refTemplateId = _instanceTemplatesMap[template.Id];
            var refTemplateIdStr = refTemplateId.ToString();
            _logger.LogInformation("Template with original id {originalId} got mapped id {mappedId}", templateIdStr, refTemplateId);

            var generatedTemplate = GenerateTemplateDescriptor(template);
            var existedTemplate = await Repository.GetTemplateAsync(refTemplateIdStr);
            if (existedTemplate == null)
            {
                await Repository.CreateTemplateAsync(refTemplateIdStr, generatedTemplate);
            }
            else
            {
                _logger.LogInformation("Template with mapped id {mappedId} already exists", refTemplateIdStr);
                var equal = CompareTemplateDescriptors(existedTemplate, generatedTemplate);
                if (equal)
                {
                    // Merge name in Properties and update template:
                    await MergeTemplatesAsync(existedTemplate, generatedTemplate);
                }
                else
                {
                    throw new InvalidOperationException("Templates are not equal: original " + templateIdStr + " and mapped " + refTemplateIdStr);
                }
            }
        }

        private async Task MergeTemplatesAsync(TemplateDescriptor existedTemplate, TemplateDescriptor generatedTemplate)
        {
            try
            {
                var existedTemplateId = existedTemplate.Id.ToString();
                var currentLanguageName = GetNameProperty(existedTemplate.Properties);
                var newLanguageName = GetNameProperty(generatedTemplate.Properties);
                if (currentLanguageName == null)
                {
                    // Add new name in header and elements, then update template:
                    var nameElement = existedTemplate.Properties.GetValue(Tokens.NameToken);
                    nameElement[_languageCode] = newLanguageName;
                    _logger.LogInformation("New name was added in template {id}: {newName}", existedTemplateId, newLanguageName);

                    foreach(var pair in existedTemplate.Elements.Zip(generatedTemplate.Elements, (e, g) => new { e, g }))
                    {
                        var existedElementValue = GetNameProperty(pair.e.Properties);
                        if (existedElementValue != null)
                        {
                            throw new InvalidOperationException("Template's element mustn't contain 'name' property, but it does");
                        }

                        var elementName = pair.e.Properties.GetValue(Tokens.NameToken);
                        var generatedElementName = GetNameProperty(pair.g.Properties);
                        elementName[_languageCode] = generatedElementName;
                        _logger.LogInformation("New name was added in template's element with template code = {templateCode}: {newName}",
                                               pair.e.TemplateCode.ToString(),
                                               generatedElementName);
                    }

                    await Repository.UpdateTemplateAsync(existedTemplate);

                    _logger.LogInformation("Templates with id {id} were merged and updated", existedTemplateId);
                }
                else
                {
                    if (!string.Equals(newLanguageName, currentLanguageName, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Template already contains 'name' property '" + currentLanguageName + "' for current language and it's different from new one: " + newLanguageName);
                    }

                    foreach (var pair in existedTemplate.Elements.Zip(generatedTemplate.Elements, (e, g) => new { e, g }))
                    {
                        var existedElementName = GetNameProperty(pair.e.Properties);
                        if (existedElementName == null)
                        {
                            throw new InvalidOperationException("Template's element must contain 'name' property, but it doesn't");
                        }

                        var generatedElementName = GetNameProperty(pair.g.Properties);
                        if (existedElementName != generatedElementName)
                        {
                            throw new InvalidOperationException("Template's element already contains 'name' property '" + existedElementName + "' for current language and it's different from new one: " + generatedElementName);
                        }
                    }

                    _logger.LogInformation("Nothing to merge and update");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Error while templates merge");
                throw;
            }
        }

        private string GetNameProperty(JObject properties)
        {
            return properties.GetValue(Tokens.NameToken)[_languageCode]?.ToString();
        }

        private bool CompareTemplateDescriptors(TemplateDescriptor existedTemplate, TemplateDescriptor generatedTemplate)
        {
            var firstProps = existedTemplate.Properties
                .Properties()
                .Where(p => p.Name != Tokens.NameToken)
                .ToList();

            var secondProps = generatedTemplate.Properties
                .Properties()
                .Where(p => p.Name != Tokens.NameToken)
                .ToList();

            if (existedTemplate.Id != generatedTemplate.Id
                   || firstProps.Count != secondProps.Count
                   || existedTemplate.Elements.Count != generatedTemplate.Elements.Count
                   || !firstProps.SequenceEqual(secondProps, _jsonEqualityComparer))
            {
                var first = new { existedTemplate.Id, Elements = existedTemplate.Elements.Count, Props = new JObject(firstProps) };
                var second = new { generatedTemplate.Id, Elements = generatedTemplate.Elements.Count, Props = new JObject(secondProps) };
                _logger.LogInformation("Different template headers, existed: {existed} and generated: {generated}", first, second);
                return false;
            }

            foreach (var pair in existedTemplate.Elements.Zip(generatedTemplate.Elements, (e, g) => new { e, g }))
            {
                var firstElement = pair.e;
                var secondElement = pair.g;
                var firstConstraints = (IReadOnlyDictionary<Language, IElementConstraints>)firstElement.Constraints;
                var secondConstraints = (IReadOnlyDictionary<Language, IElementConstraints>)secondElement.Constraints;

                var firstElementProps = firstElement.Properties
                    .Properties()
                    .Where(p => p.Name != Tokens.NameToken)
                    .ToList();

                var secondElementProps = secondElement.Properties
                    .Properties()
                    .Where(p => p.Name != Tokens.NameToken)
                    .ToList();

                var generatedElementTemplateId = _templateElementsMap[Tuple.Create(generatedTemplate.Id, secondElement.TemplateCode)];
                if (firstElement.TemplateCode != secondElement.TemplateCode
                    || firstElement.Type != secondElement.Type
                    || firstConstraints.Count != secondConstraints.Count
                    || firstElementProps.Count != secondElementProps.Count
                    || !firstElementProps.SequenceEqual(secondElementProps, _jsonEqualityComparer))
                {
                    var first = new { firstElement.TemplateCode, Type = firstElement.Type.ToString(), Constraints = firstConstraints.Count, Props = new JObject(firstElementProps) };
                    var second = new { secondElement.TemplateCode, Type = secondElement.Type.ToString(), Constraints = secondConstraints.Count, Props = new JObject(secondElementProps) };
                    _logger.LogInformation("Different elements headers for template {id}, existed: {existed} and generated: {generated}", existedTemplate.Id, first, second);
                    return false;
                }

                var firstConstraint = firstElement.Constraints.For(Language.Unspecified);
                var secondConstraint = secondElement.Constraints.For(Language.Unspecified);

                if (!Equals(firstConstraint, secondConstraint))
                {
                    var first = JsonConvert.SerializeObject(firstConstraint, SerializerSettings.Default);
                    var second = JsonConvert.SerializeObject(secondConstraint, SerializerSettings.Default);
                    _logger.LogInformation("Different element constraints for template {id}, existed: {existed} and generated: {generated}", existedTemplate.Id, first, second);
                    if (!TryToMergeElementConstraints(firstElement.Type, firstConstraint, secondConstraint, generatedElementTemplateId))
                    {
                        _logger.LogWarning("Automatic merge of template element costraints failed, element id {elementId}, template code {templateCode}, template {id}",
                                           generatedElementTemplateId.ToString(),
                                           firstElement.TemplateCode.ToString(),
                                           existedTemplate.Id);
                        return false;
                    }
                }
            }

            return true;
        }

        private bool TryToMergeElementConstraints(ElementDescriptorType descriptorType, IElementConstraints existedConstraint, IElementConstraints generatedConstraint, long generatedElementTemplateId)
        {
            switch (descriptorType)
            {
                case ElementDescriptorType.BitmapImage:
                case ElementDescriptorType.VectorImage:
                case ElementDescriptorType.Article:
                    {
                        if (!(existedConstraint is IBinaryElementConstraints existed) ||
                            !(generatedConstraint is IBinaryElementConstraints generated))
                        {
                            throw new ArgumentException("Incorrect constraint type");
                        }

                        // Both constraints has or both hasn't maxSize restriction:
                        if (existed.MaxSize.HasValue && generated.MaxSize.HasValue ||
                            !existed.MaxSize.HasValue && !generated.MaxSize.HasValue)
                        {
                            return false;
                        }

                        if (existed.MaxSize.HasValue)
                        {
                            generated.MaxSize = existed.MaxSize.Value;
                            if (Equals(existed, generated))
                            {
                                _logger.LogWarning("MaxSize has been set to {maxSize} in generated element template and now they are equal, elementTemplateId = {elementTemplateId}",
                                                   existed.MaxSize.Value.ToString(),
                                                   generatedElementTemplateId.ToString());
                                return true;
                            }

                            generated.MaxSize = null;
                        }

                        return false;
                    }

                case ElementDescriptorType.FasComment:
                    {
                        if (!(existedConstraint is TextElementConstraints existed) ||
                            !(generatedConstraint is TextElementConstraints generated))
                        {
                            throw new ArgumentException("Incorrect constraint type");
                        }

                        // Both constraints has or both hasn't maxLength restriction:
                        if (existed.MaxSymbols.HasValue && generated.MaxSymbols.HasValue ||
                            !existed.MaxSymbols.HasValue && !generated.MaxSymbols.HasValue)
                        {
                            return false;
                        }

                        if (existed.MaxSymbols.HasValue)
                        {
                            generated.MaxSymbols = existed.MaxSymbols.Value;
                            if (Equals(existed, generated))
                            {
                                _logger.LogWarning("MaxSymbols has been set to {maxSymbols} in generated element template and now they are equal, elementTemplateId = {elementTemplateId}",
                                                   existed.MaxSymbols.Value.ToString(),
                                                   generatedElementTemplateId.ToString());
                                return true;
                            }

                            generated.MaxSymbols = null;
                        }

                        return false;
                    }

                case ElementDescriptorType.PlainText:
                case ElementDescriptorType.FormattedText:
                case ElementDescriptorType.Link:
                case ElementDescriptorType.Phone:
                case ElementDescriptorType.VideoLink:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(descriptorType), descriptorType, "Unknown ElementDescriptorType");
            }
        }

        private TemplateDescriptor GenerateTemplateDescriptor(AdvertisementTemplate template)
        {
            return new TemplateDescriptor
            {
                Id = _instanceTemplatesMap[template.Id],
                Elements = template.ElementTemplatesLink
                                       .Where(link => !link.IsDeleted && !link.ElementTemplate.IsDeleted)
                                       .OrderBy(link => link.ExportCode)
                                       .Select(ConvertTemplateLinkToTemplateElementDescriptor)
                                       .ToList(),
                Properties = new JObject
                {
                    { Tokens.NameToken, new JObject { { _languageCode, template.Name } } },
                    { Tokens.IsWhiteListedToken, template.IsAllowedToWhiteList }
                }
            };
        }

        private IElementDescriptor ConvertTemplateLinkToTemplateElementDescriptor(ElementTemplateLink link)
        {
            try
            {
                var elementTemplate = link.ElementTemplate;
                var elementType = Converter.ConvertRestrictionTypeToDescriptorType(elementTemplate);
                var elementConstraints = Converter.GetElementTemplateConstraints(elementType, elementTemplate);
                var constraintSetItems = new[] { new ConstraintSetItem(Language.Unspecified, elementConstraints) };
                var constraints = new ConstraintSet(constraintSetItems);
                var properties = new JObject
                {
                    { Tokens.NameToken, new JObject { { _languageCode, elementTemplate.Name } } },
                    { Tokens.IsRequiredToken, elementTemplate.IsRequired }
                };

                _templateElementsMap[Tuple.Create(_instanceTemplatesMap[link.AdsTemplateId], link.ExportCode)] = link.AdsElementTemplateId;

                return new ElementDescriptor(elementType, link.ExportCode, properties, constraints);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Convert element template link {id} error (template {templateId})", link.Id.ToString(), link.AdsTemplateId.ToString());
                throw;
            }
        }

        private async Task<IObjectElementValue> GetElementDescriptorValueAsync(ElementDescriptorType elementType, AdvertisementElement element, ApiObjectElementDescriptor newElem)
        {
            var templateCode = element.AdsTemplatesAdsElementTemplates.ExportCode;
            switch (elementType)
            {
                case ElementDescriptorType.FormattedText:
                case ElementDescriptorType.PlainText:
                    return new TextElementValue
                    {
                        Raw = element.Text
                    };
                case ElementDescriptorType.Link:
                    return new TextElementValue
                    {
                        Raw = string.IsNullOrEmpty(element.Text) || element.Text.StartsWith("http://") || element.Text.StartsWith("https://")
                                      ? element.Text
                                      : "http://" + element.Text
                    };
                case ElementDescriptorType.FasComment:
                {
                    var raw = Converter.ConvertFasCommentType(element, newElem);
                    return new FasElementValue
                    {
                        Raw = raw,
                        Text = raw != null ? element.Text : null
                    };
                }
                case ElementDescriptorType.BitmapImage:
                {
                    if (element.FileId == null)
                    {
                        return new BitmapImageElementValue();
                    }

                    EnsureFileElementIsValid(elementType, element, newElem);

                    var templateId = _instanceTemplatesMap[element.AdsTemplatesAdsElementTemplates.AdsTemplateId];

                    var constraints = (BitmapImageElementConstraints)newElem.Constraints.For(Language.Unspecified);
                    var format = Converter.PreprocessImageFile(element.File, templateId, templateCode, constraints);
                    var json = await Repository.UploadFileAsync(new Uri(newElem.UploadUrl, UriKind.RelativeOrAbsolute), element, format);
                    Interlocked.Increment(ref _uploadedBinariesCount);

                    return new BitmapImageElementValue
                        {
                            Raw = json.Value<string>("raw")
                        };
                }

                case ElementDescriptorType.VectorImage:
                {
                    if (element.FileId == null)
                    {
                        return new VectorImageElementValue();
                    }

                    EnsureFileElementIsValid(elementType, element, newElem);
                    var format = Converter.DetectFileFormat(element.File, templateCode);
                    var json = await Repository.UploadFileAsync(new Uri(newElem.UploadUrl, UriKind.RelativeOrAbsolute), element, format);
                    Interlocked.Increment(ref _uploadedBinariesCount);
                    return new VectorImageElementValue
                        {
                            Raw = json.Value<string>("raw")
                        };
                }

                case ElementDescriptorType.Article:
                {
                    if (element.FileId == null)
                    {
                        return new ArticleElementValue();
                    }

                    EnsureFileElementIsValid(elementType, element, newElem);
                    var json = await Repository.UploadFileAsync(new Uri(newElem.UploadUrl, UriKind.RelativeOrAbsolute), element, FileFormat.Chm);
                    Interlocked.Increment(ref _uploadedBinariesCount);
                    return new ArticleElementValue
                        {
                            Raw = json.Value<string>("raw")
                        };
                }

                case ElementDescriptorType.Phone:
                    return new PhoneElementValue
                        {
                            Raw = element.Text,
                            Formatted = null
                        };

                case ElementDescriptorType.VideoLink:
                    return new TextElementValue
                        {
                            Raw = element.Text
                        };
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementType), elementType, "Unknown ElementDescriptorType");
            }
        }

        private static void EnsureFileElementIsValid(ElementDescriptorType elementType, AdvertisementElement element, ApiObjectElementDescriptor newElem)
        {
            if (element.File == null)
            {
                throw new ArgumentException($"Element descriptor without {elementType.ToString()} file");
            }

            if (string.IsNullOrEmpty(newElem.UploadUrl))
            {
                throw new ArgumentException($"Generated uploadUri from OkApi is empty for {elementType.ToString()} element");
            }

            if (!Uri.IsWellFormedUriString(newElem.UploadUrl, UriKind.RelativeOrAbsolute))
            {
                throw new ArgumentException($"Generated uploadUri from OkApi is not well formed for {elementType.ToString()} element");
            }
        }
    }
}
