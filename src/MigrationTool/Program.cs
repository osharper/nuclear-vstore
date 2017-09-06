using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MigrationTool.Models;

using NuClear.VStore.Descriptors;
using System.Threading.Tasks;

using Serilog;

namespace MigrationTool
{
    // ReSharper disable once UnusedMember.Global
    public class Program
    {
        private static readonly IReadOnlyDictionary<string, (Language Lang, bool MigrateModerationStatuses)> InstanceMap = new Dictionary<string, (Language, bool)>
            {
                { "ErmRu", (Language.Ru, true) },
                { "ErmUa", (Language.Ru, false) },
                { "ErmAe", (Language.En, false) },
                { "ErmCl", (Language.Es, false) },
                { "ErmCy", (Language.En, false) },
                { "ErmCz", (Language.Cs, false) },
                { "ErmKg", (Language.Ru, false) },
                { "ErmKz", (Language.Ru, false) }
            };

        private static readonly IDictionary<string, IDictionary<long, long>> TemplatesMap = new Dictionary<string, IDictionary<long, long>>();

        private static IConfigurationRoot Configuration { get; set; }

        public static int Main(string[] args)
        {
            var env = Environment.GetEnvironmentVariable("AMS_ENVIRONMENT") ?? "Production";
            var basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            Configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.ToLower()}.json")
                .AddEnvironmentVariables("AMS_")
                .Build();

            var apiUri = new Uri(Configuration.GetConnectionString("OkApiConnection"));
            var storageUri = new Uri(Configuration.GetConnectionString("VStoreConnection"));

            var serviceProvider = new ServiceCollection()
                .AddLogging()
                .AddOptions()
                .Configure<Options>(Configuration.GetSection("MigrationTool"))
                .AddSingleton(x => new ApiRepository(
                                  x.GetService<ILogger<ApiRepository>>(),
                                  apiUri,
                                  storageUri,
                                  x.GetService<IOptions<Options>>().Value.ApiVersion,
                                  x.GetService<IOptions<Options>>().Value.ApiToken))
                .AddSingleton(x => new ConverterService(x.GetService<ILogger<ConverterService>>()))
                .BuildServiceProvider();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .CreateLogger();

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>()
                                               .AddSerilog();

            Console.CancelKeyPress += (sender, eventArgs) => Log.CloseAndFlush();

            var logger = loggerFactory.CreateLogger<Program>();
            try
            {
                ReadMergedFileAsync("import.csv")
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(), ex, "Error occured while reading merge file");
                Log.CloseAndFlush();
                return 1;
            }

            var exitCode = RunImport(loggerFactory, serviceProvider, logger)
                .GetAwaiter()
                .GetResult();

            Log.CloseAndFlush();

            return exitCode;
        }

        private static async Task<int> RunImport(ILoggerFactory loggerFactory, IServiceProvider serviceProvider, Microsoft.Extensions.Logging.ILogger logger)
        {
            var importLogger = loggerFactory.CreateLogger<ImportService>();
            var options = serviceProvider.GetService<IOptions<Options>>().Value;
            var repository = serviceProvider.GetService<ApiRepository>();
            var converter = serviceProvider.GetService<ConverterService>();

            await repository.EnsureApiAvailable(options.InitialPingInterval, options.InitialPingTries);

            options.ThresholdDate = ConvertDateParameterToUniversalTime(logger, nameof(options.ThresholdDate), options.ThresholdDate);
            options.PositionsBeginDate = ConvertDateParameterToUniversalTime(logger, nameof(options.PositionsBeginDate), options.PositionsBeginDate);
            options.OrdersModificationBeginDate = ConvertDateParameterToUniversalTime(logger, nameof(options.OrdersModificationBeginDate), options.OrdersModificationBeginDate);
            options.OrdersMinDistributionDate = ConvertDateParameterToUniversalTime(logger, nameof(options.OrdersMinDistributionDate), options.OrdersMinDistributionDate);
            options.OrdersMaxDistributionDate = ConvertDateParameterToUniversalTime(logger, nameof(options.OrdersMaxDistributionDate), options.OrdersMaxDistributionDate);

            foreach (var instance in InstanceMap)
            {
                var connectionString = Configuration.GetConnectionString($"{instance.Key}Connection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    logger.LogInformation("Connection string for {instance} not found, skip this instance from import", instance.Key);
                    continue;
                }

                logger.LogInformation("Start import from {instance} in mode: {mode}", instance.Key, options.Mode.ToString());
                var contextOptions = new DbContextOptionsBuilder<ErmContext>()
                    .UseLoggerFactory(loggerFactory)
                    .UseSqlServer(connectionString)
                    .Options;

                try
                {
                    var importService = new ImportService(
                        contextOptions,
                        instance.Value.Lang,
                        options,
                        TemplatesMap[instance.Key],
                        instance.Value.MigrateModerationStatuses,
                        repository,
                        converter,
                        importLogger);

                    if (await importService.ImportAsync(options.Mode))
                    {
                        logger.LogInformation("Import from {instance} finished successfully", instance.Key);
                    }
                    else
                    {
                        logger.LogError("Import from {instance} wasn't complete, skip other instances", instance.Key);
                        {
                            return 3;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(new EventId(), ex, "Fatal error while import from {instance} in {mode} mode", instance.Key, options.Mode.ToString());
                    {
                        return 2;
                    }
                }
            }

            return 0;
        }

        private static DateTime? ConvertDateParameterToUniversalTime(Microsoft.Extensions.Logging.ILogger logger, string parameterName, DateTime? date)
        {
            if (!date.HasValue)
            {
                return null;
            }

            return ConvertDateParameterToUniversalTime(logger, parameterName, date.Value);
        }

        private static DateTime ConvertDateParameterToUniversalTime(Microsoft.Extensions.Logging.ILogger logger, string parameterName, DateTime date)
        {
            if (date.Kind == DateTimeKind.Utc)
            {
                return date;
            }

            var newValue = date.ToUniversalTime();
            logger.LogWarning("{parameter} parameter value was manually converted to UTC: {newValue:o} from {value:o}", parameterName, newValue, date);
            return newValue;
        }

        private static async Task ReadMergedFileAsync(string fileName)
        {
            using (var file = System.IO.File.Open(fileName, FileMode.Open, FileAccess.Read))
            {
                using (var stream = new StreamReader(file))
                {
                    var lineNumber = 0L;
                    while (!stream.EndOfStream)
                    {
                        ++lineNumber;
                        var line = await stream.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var values = line.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        if (values.Length % 2 != 0 || values.Length < 1)
                        {
                            throw new IOException("Incorrect number of values in line: " + lineNumber.ToString());
                        }

                        var referenceValue = new long?();
                        for (var i = 0; i < values.Length; i += 2)
                        {
                            var instance = values[i];
                            if (!InstanceMap.ContainsKey(instance))
                            {
                                throw new IOException("Incorrect instance: " + instance + " on line " + lineNumber.ToString());
                            }

                            if (!long.TryParse(values[i + 1], out long id))
                            {
                                throw new InvalidCastException("Cannot parse id for " + instance + " instance on line " + lineNumber.ToString());
                            }

                            if (!TemplatesMap.ContainsKey(instance))
                            {
                                TemplatesMap.Add(instance, new Dictionary<long, long>());
                            }

                            if (TemplatesMap[instance].ContainsKey(id))
                            {
                                throw new InvalidOperationException(
                                    $"Instance {instance} already contains id {TemplatesMap[instance][id].ToString()}, cannot add id {id.ToString()} from line {lineNumber.ToString()}");
                            }

                            // Add first identifier in line as reference value:
                            if (!referenceValue.HasValue)
                            {
                                referenceValue = id;
                            }

                            TemplatesMap[instance][id] = referenceValue.Value;
                        }
                    }
                }
            }
        }
    }
}
