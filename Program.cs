using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using RedRiver.SaffronCore.Logging.JsonFile;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Threading;

namespace azure2elasticstack
{
    class Program
    {
        static BatchingLogger _logger;
        static EfContext _ctx;

        static Creds[] creds = new[]
        {
            new Creds("PutClientIdGuidHere", "PutClientSecretBase64StringHere", "PutTenantIdGuidHere"),
        };

        static Azure.IAuthenticated GetAzure(Creds creds)
        {
            return Azure.Authenticate(new AzureCredentials(new ServicePrincipalLoginInformation
            {
                ClientId = creds.ClientId,
                ClientSecret = creds.ClientSecret
            }, creds.TenantId, AzureEnvironment.AzureGlobalCloud));
        }

        static long oldestAgeMs;
        static long totalAgeMs;
        static long numStats;

        async static Task Main(string[] args)
        {
            Console.WriteLine("Updating DB...");
            _ctx = new EfContext();
            await _ctx.Database.MigrateAsync();

            Console.WriteLine("Initialising...");
            var opts = new JsonFileLoggerOptions
            {
                FileName = "azlog",
                LogDirectory = "logs",
                RetainedFileCountLimit = 60,
                IsEnabled = true,
                FileSizeLimit = 1024 * 1024 * 1024,
                BackgroundQueueSize = 1000
            };
            _logger = new JsonFileLoggerProvider(opts).CreateLogger();

            while (true)
            {
                var started = DateTime.Now;

                Console.WriteLine($"DB ready: {_ctx.MetricLastDates.Count()} records");
                Console.WriteLine("Running...");

                totalAgeMs = 0;
                numStats = 0;
                oldestAgeMs = 0;
                var total = (await Task.WhenAll(creds.Select(ProcessCredsAsync))).Sum();
                Console.WriteLine($"TOTAL {total} new stats.");
                Console.WriteLine($"Oldest age = {oldestAgeMs / 1000 / 60:0.0} minutes");
                Console.WriteLine($"Average age = {(totalAgeMs / numStats) / 1000 / 60:0.0} minutes");

                var next = started.AddMinutes(1.0);
                var now = DateTime.Now;
                if (next > now)
                {
                    var t = next - now;
                    Console.WriteLine($"Waiting for next run {t}...");
                    await Task.Delay(t);
                }
            }
        }

        async static Task<int> ProcessCredsAsync(Creds creds)
        {
            try
            {
                var azure = GetAzure(creds);
                var allSubs = await azure.Subscriptions.ListAsync();
                var all = await Task.WhenAll(allSubs.Select(s => ProcessSubscriptionAsync(creds, s)));
                return all.Sum();
            }
            catch (Exception ex)
            {
                // todo...
                Console.WriteLine("Exception getting subs: " + ex.Message);
                return 0;
            }
        }

        async static Task<int> ProcessSubscriptionAsync(Creds creds, ISubscription sub)
        {
            try
            {
                var subApi = GetAzure(creds).WithSubscription(sub.SubscriptionId);
                var groups = await subApi.ResourceGroups.ListAsync();
                var all = await Task.WhenAll(groups.Select(g => ProcessResourceGroupAsync(subApi, sub, g)));
                return all.Sum();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception getting resource groups: " + ex.Message);
                return 0;
            }
        }

        async static Task<int> ProcessResourceGroupAsync(IAzure subApi, ISubscription sub, IResourceGroup group)
        {
            try
            {
                var res = await subApi.MetricDefinitions.Manager.ResourceManager.GenericResources.ListByResourceGroupAsync(group.Name);
                var all = await Task.WhenAll(res.Select(r => ProcessResourceAsync(subApi, sub, group, r)));
                var total = all.Sum();
                //Console.WriteLine($"{sub.DisplayName} - {group.Name}: {total} new stats from {res.Count()} resources");
                return total;
            }
            catch (Exception ex)
            {
                // todo...
                Console.WriteLine("Exception getting resource groups defs: " + ex.Message);
                return 0;
            }
        }

        async static Task<int> ProcessResourceAsync(IAzure subApi, ISubscription sub, IResourceGroup group, IGenericResource res)
        {
            try
            {
                var metrics = await subApi.MetricDefinitions.ListByResourceAsync(res.Id);
                var all = await Task.WhenAll(metrics.Select(m => ProcessMetricAsync(subApi, sub, group, res, m)));
                return all.Sum();
            }
            catch (Exception ex)
            {
                // todo...
                Console.WriteLine("Exception getting metric defs: " + ex.Message);
                return 0;
            }
        }

        static SemaphoreSlim _sem = new SemaphoreSlim(32);

        async static Task<int> ProcessMetricAsync(IAzure subApi, ISubscription subscription, IResourceGroup group, IGenericResource res, IMetricDefinition metric)
        {
            int m = 0;

            DateTime? last = null;
            MetricLastDate record = null;
            lock (_ctx)
            {
                record = _ctx.MetricLastDates.Where(d => d.Key == metric.Id).SingleOrDefault();
            }
            last = record?.LastDate;

            try
            {
                await _sem.WaitAsync();

                var from = last?.AddMinutes(1.0) ?? DateTime.UtcNow.RoundMinutes().AddHours(-1.0);
                var to = DateTime.UtcNow.RoundMinutes().AddMinutes(10.0);

                var maxDataToGet = TimeSpan.FromHours(2.0);
                if (to - from > maxDataToGet)
                {
                    to = from + maxDataToGet;
                }

                if (metric.Inner.IsDimensionRequired == true)
                {
                    var dims = metric.Inner.Dimensions;
                    // can ignore..
                    return 0;
                }

                var data = await metric.DefineQuery()
                    .StartingFrom(from)
                    .EndsBefore(to) // as much as possible
                    .WithAggregation("Average,Minimum,Maximum,Count,Total")
                    .WithResultType(ResultType.Data)
                    .WithInterval(TimeSpan.FromMinutes(1))
                    .ExecuteAsync();

                if (data.Metrics.Count != 1 || data.Metrics[0].Timeseries.Count != 1) return 0;

                /*
                Console.WriteLine($"query from {from} to {to}: {data.Metrics[0].Timeseries[0].Data.Count()} results");
                Console.WriteLine($" min: {data.Metrics[0].Timeseries[0].Data.Min(d => d.Minimum)}");
                Console.WriteLine($" max: {data.Metrics[0].Timeseries[0].Data.Max(d => d.Maximum)}");
                Console.WriteLine($" avg: {data.Metrics[0].Timeseries[0].Data.Average(d => d.Average)}");
                */

                var dubiousCutoffUtc = DateTime.UtcNow.AddMinutes(-30.0);

                var pts = data.Metrics[0].Timeseries[0].Data

                    // from future to past
                    .OrderByDescending(d => d.TimeStamp)

                    // skip anything with no data yet
                    .SkipWhile(d =>
                        d.TimeStamp > dubiousCutoffUtc &&
                        (d.Average ?? 0) == 0 &&
                        (d.Maximum ?? 0) == 0 &&
                        (d.Minimum ?? 0) == 0 &&
                        (d.Total ?? 0) == 0 &&
                        (
                            (d.Count ?? 0) == 0 || d.TimeStamp > dubiousCutoffUtc
                        )
                    )

                    // and the first one with data (probably the current slice)
                    .Skip(1);

                /*
                if (pts.Any())
                    Console.WriteLine($"actual data points: {pts.Count()}, from {pts.Min(p => p.TimeStamp)} to {pts.Max(p => p.TimeStamp)}");
                else
                    Console.WriteLine($"all data points dismissed");
                */

                if (pts.Any())
                {
                    lock (_logger)
                    {
                        foreach (var pt in pts.Reverse())
                        {
                            var entry = new Dictionary<string, object>(13)
                            {
                                { "@timestamp", pt.TimeStamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFZ") },
                                { "lib", "Saffron" },
                                { "type", "cloud" },
                                { "cloud", "Azure" },
                                { "sub", subscription.DisplayName },
                                { "group", group.Name },
                                { "resource", new Dictionary<string,object>(3) {
                                    { "name", res.Name },
                                    { "type", res.Type },
                                    { "tags", res.Tags }
                                }},
                                { "metric", new Dictionary<string,object>(2) {
                                    { "name", metric.Name.Value },
                                    { "unit", metric.Unit }
                                }},
                            };
                            if (pt.Average != null) entry["avg"] = pt.Average;
                            if (pt.Count != null) entry["num"] = pt.Count;
                            if (pt.Maximum != null) entry["max"] = pt.Maximum;
                            if (pt.Minimum != null) entry["min"] = pt.Minimum;
                            if (pt.Total != null) entry["sum"] = pt.Total;
                            _logger.LogAsJson(entry);
                            m++;
                        }
                    }
                    lock (_ctx)
                    {
                        if (record == null)
                        {
                            record = new MetricLastDate { Key = metric.Id };
                            _ctx.MetricLastDates.Add(record);
                        }
                        record.LastDate = pts.First().TimeStamp;
                        _ctx.SaveChanges();


                        numStats++;
                        long ageMs = (long)(DateTime.UtcNow - record.LastDate).TotalMilliseconds;
                        totalAgeMs += ageMs;
                        if (ageMs > oldestAgeMs) oldestAgeMs = ageMs;
                    }
                }
            }
            catch (ErrorResponseException ex)
            {
                // ignored
                //Console.WriteLine($"Error reading {metric.Name.LocalizedValue} from {res.Name} ({res.Type}): {ex.Message} - {ex.Body.Code} {ex.Body.Message}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP error reading {metric.Name.LocalizedValue} from {res.Name} ({res.Type}): {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading {metric.Name.LocalizedValue} from {res.Name} ({res.Type}): {ex.Message}");
            }
            finally
            {
                _sem.Release();
            }

            return m;
        }
    }

}
