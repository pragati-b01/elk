using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;

namespace DynatraceMetricsExtract
{
    public class Program
    {
        static void Main(string[] args)
        {
            try
            {
                DynatraceApiService service = new DynatraceApiService();

                Console.WriteLine("Starting Dynatrace Extraction...");

                var cpuRecords = service.GetCpuMetrics();
                var memoryRecords = service.GetMemoryMetrics();
                var diskRecords = service.GetDiskMetrics();

                Console.WriteLine($"CPU Records : {cpuRecords.Count}");
                Console.WriteLine($"Memory Records : {memoryRecords.Count}");
                Console.WriteLine($"Disk Records : {diskRecords.Count}");

                // CsvExportService.ExportCpuMetrics(cpuRecords);
                // CsvExportService.ExportMemoryMetrics(memoryRecords);
                // CsvExportService.ExportDiskMetrics(diskRecords);

                Console.WriteLine("Completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            Console.ReadKey();
        }
    }

    #region Models

    public class CpuRecord
    {
        public string HostId { get; set; }
        public string HostName { get; set; }
        public DateTime TimeStamp { get; set; }

        public double? CpuUsageAvg { get; set; }
        public double? CpuUsageMax { get; set; }
        public double? CpuUsageMin { get; set; }

        public double? CpuIdleAvg { get; set; }
        public double? CpuIdleMax { get; set; }
        public double? CpuIdleMin { get; set; }
    }

    public class MemoryRecord
    {
        public string HostId { get; set; }
        public string HostName { get; set; }
        public DateTime TimeStamp { get; set; }

        public double? MemoryUsedAvg { get; set; }
        public double? MemoryUsedMax { get; set; }
        public double? MemoryTotal { get; set; }
    }

    public class DiskRecord
    {
        public string HostId { get; set; }
        public string HostName { get; set; }

        public string DiskId { get; set; }
        public string DiskName { get; set; }

        public DateTime TimeStamp { get; set; }

        public double? DiskUsedAvg { get; set; }
        public double? DiskUsedMax { get; set; }

        public double? DiskAvailAvg { get; set; }
        public double? DiskAvailMax { get; set; }
    }

    #endregion

    #region Config

    public class MetricConfig
    {
        public string MetricId { get; set; }

        public bool Avg { get; set; }

        public bool Max { get; set; }

        public bool Min { get; set; }
    }

    #endregion

    #region Service

    public class DynatraceApiService
    {
        private readonly string baseUrl;
        private readonly string token;

        public DynatraceApiService()
        {
            baseUrl = ConfigurationManager.AppSettings["DynatraceBaseUrl"];
            token = ConfigurationManager.AppSettings["ApiToken"];
        }

        public List<CpuRecord> GetCpuMetrics()
        {
            var metrics = new List<MetricConfig>
            {
                new MetricConfig
                {
                    MetricId="builtin:host.cpu.usage",
                    Avg=true,
                    Max=true
                }
            };

            string selector = BuildSelector(metrics);

            JObject result = ExecuteMetricQuery(selector);

            return ParseCpuMetrics(result);
        }

        public List<MemoryRecord> GetMemoryMetrics()
        {
            var metrics = new List<MetricConfig>
            {
                new MetricConfig
                {
                    MetricId="builtin:host.mem.used",
                    Avg=true,
                    Max=true
                },
                new MetricConfig
                {
                    MetricId="builtin:host.mem.total"
                }
            };

            string selector = BuildSelector(metrics);

            JObject result = ExecuteMetricQuery(selector);

            return ParseMemoryMetrics(result);
        }

        public List<DiskRecord> GetDiskMetrics()
        {
            var metrics = new List<MetricConfig>
            {
                new MetricConfig
                {
                    MetricId="builtin:host.disk.used",
                    Avg=true,
                    Max=true
                },
                new MetricConfig
                {
                    MetricId="builtin:host.disk.avail",
                    Avg=true,
                    Max=true
                }
            };

            string selector = BuildSelector(metrics);

            JObject result = ExecuteMetricQuery(selector);

            return ParseDiskMetrics(result);
        }

        private string BuildSelector(List<MetricConfig> metrics)
        {
            List<string> selectors = new List<string>();

            foreach (var metric in metrics)
            {
                if (metric.Avg)
                    selectors.Add($"{metric.MetricId}:avg");

                if (metric.Max)
                    selectors.Add($"{metric.MetricId}:max");

                if (metric.Min)
                    selectors.Add($"{metric.MetricId}:min");

                if (!metric.Avg &&
                    !metric.Max &&
                    !metric.Min)
                {
                    selectors.Add(metric.MetricId);
                }
            }

            return string.Join(",", selectors);
        }

        private JObject ExecuteMetricQuery(string metricSelector)
        {
            string hosts =
                ConfigurationManager.AppSettings["Hosts"];

            string entitySelector =
                Uri.EscapeDataString(
                    $"type(HOST),entityName.in({hosts})");

            string from;
            string to;

            GetTimeRange(out from, out to);

            string resolution =
                ConfigurationManager.AppSettings["Resolution"];

            string url =
                $"{baseUrl}/api/v2/metrics/query" +
                $"?metricSelector={Uri.EscapeDataString(metricSelector)}" +
                $"&entitySelector={entitySelector}" +
                $"&from={from}" +
                $"&to={to}" +
                $"&resolution={resolution}";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add(
                    "Authorization",
                    $"Api-Token {token}");

                string json =
                    client.GetStringAsync(url).Result;

                return JObject.Parse(json);
            }
        }

        private void GetTimeRange(
            out string from,
            out string to)
        {
            string mode =
                ConfigurationManager.AppSettings["TimeMode"];

            if (mode == "Absolute")
            {
                from =
                    ConfigurationManager.AppSettings["FromDateTime"];

                to =
                    ConfigurationManager.AppSettings["ToDateTime"];
            }
            else
            {
                int days =
                    Convert.ToInt32(
                        ConfigurationManager.AppSettings["RelativeDays"]);

                DateTime end =
                    DateTime.UtcNow;

                DateTime start =
                    end.AddDays(-days);

                from =
                    start.ToString("yyyy-MM-ddTHH:mm:ss");

                to =
                    end.ToString("yyyy-MM-ddTHH:mm:ss");
            }
        }

        #region Parsers

        private List<CpuRecord> ParseCpuMetrics(JObject json)
        {
            List<CpuRecord> records =
                new List<CpuRecord>();

            var maxMetric =
                json["result"]
                .FirstOrDefault(x =>
                    x["metricId"]?
                    .ToString()
                    .Contains("cpu.usage:max") == true);

            var avgMetric =
                json["result"]
                .FirstOrDefault(x =>
                    x["metricId"]?
                    .ToString()
                    .Contains("cpu.usage:avg") == true);

            if (maxMetric == null ||
                avgMetric == null)
            {
                return records;
            }

            JArray maxData =
                (JArray)maxMetric["data"];

            JArray avgData =
                (JArray)avgMetric["data"];

            foreach (JObject maxSeries in maxData)
            {
                string hostId =
                    maxSeries["dimensions"][0]
                    .ToString();

                JToken avgSeries =
                    avgData.FirstOrDefault(x =>
                        x["dimensions"][0]
                        .ToString()
                        .Equals(hostId,
                        StringComparison.OrdinalIgnoreCase));

                if (avgSeries == null)
                    continue;

                JArray timestamps =
                    (JArray)maxSeries["timestamps"];

                JArray maxValues =
                    (JArray)maxSeries["values"];

                JArray avgValues =
                    (JArray)avgSeries["values"];

                for (int i = 0; i < timestamps.Count; i++)
                {
                    records.Add(
                        new CpuRecord
                        {
                            HostId = hostId,
                            HostName = hostId,
                            TimeStamp =
                                DateTimeOffset
                                .FromUnixTimeMilliseconds(
                                    timestamps[i].Value<long>())
                                .LocalDateTime,

                            CpuUsageMax =
                                maxValues[i]?.Value<double>(),

                            CpuUsageAvg =
                                avgValues[i]?.Value<double>()
                        });
                }
            }

            return records;
        }

        private List<MemoryRecord> ParseMemoryMetrics(JObject json)
        {
            return new List<MemoryRecord>();
        }

        private List<DiskRecord> ParseDiskMetrics(JObject json)
        {
            return new List<DiskRecord>();
        }

        #endregion
    }

    #endregion
}
