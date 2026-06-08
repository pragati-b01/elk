using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;

public class DynatraceApiService
{
    private readonly string baseUrl;
    private readonly string token;

    public DynatraceApiService()
    {
        baseUrl =
            ConfigurationManager.AppSettings["DynatraceBaseUrl"];

        token =
            ConfigurationManager.AppSettings["ApiToken"];
    }

    private JObject ExecuteMetricQuery(
        string metricSelector,
        string host)
    {
        string entitySelector =
            $"type(HOST),entityName.equals({host})";

        string from;
        string to;

        GetTimeRange(out from, out to);

        string resolution =
            ConfigurationManager.AppSettings["Resolution"];

        string url =
            $"{baseUrl}/api/v2/metrics/query";

        url +=
            $"?metricSelector={HttpUtility.UrlEncode(metricSelector)}";

        url +=
            $"&entitySelector={HttpUtility.UrlEncode(entitySelector)}";

        url += $"&from={from}";
        url += $"&to={to}";
        url += $"&resolution={resolution}";

        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Api-Token",
                    token);

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

        if (mode == "Relative")
        {
            int days =
                Convert.ToInt32(
                    ConfigurationManager.AppSettings["RelativeDays"]);

            DateTime end =
                DateTime.UtcNow;

            DateTime start =
                end.AddDays(-days);

            from = start.ToString("o");
            to = end.ToString("o");
        }
        else
        {
            from =
                ConfigurationManager.AppSettings["FromDateTime"];

            to =
                ConfigurationManager.AppSettings["ToDateTime"];
        }
    }

    public List<CpuRecord> GetCpuMetrics(
        string host)
    {
        string selector =
            ConfigurationManager.AppSettings["CpuMetricSelector"];

        JObject result =
            ExecuteMetricQuery(selector, host);

        return ParseCpu(result, host);
    }

    public List<MemoryRecord> GetMemoryMetrics(
        string host)
    {
        return new List<MemoryRecord>();

        // Implement after actual response
    }

    public List<DiskRecord> GetDiskMetrics(
        string host)
    {
        return new List<DiskRecord>();

        // Implement after actual response
    }

    private List<CpuRecord> ParseCpu(
        JObject json,
        string host)
    {
        List<CpuRecord> records =
            new List<CpuRecord>();

        JArray timestamps =
            (JArray)json["timestamps"];

        JArray values =
            (JArray)json["result"][0]["data"][0]["values"];

        for (int i = 0; i < timestamps.Count; i++)
        {
            records.Add(
                new CpuRecord
                {
                    HostName = host,
                    TimeStamp =
                        DateTimeOffset
                        .FromUnixTimeMilliseconds(
                            timestamps[i].Value<long>())
                        .LocalDateTime,

                    CpuUsage =
                        values[i] == null
                        ? 0
                        : values[i].Value<double>()
                });
        }

        return records;
    }
}




using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;

public static class CsvExportService
{
    public static void ExportCpu(
        List<CpuRecord> rows)
    {
        string folder =
            ConfigurationManager.AppSettings["OutputFolder"];

        Directory.CreateDirectory(folder);

        string file =
            Path.Combine(folder, "Cpu.csv");

        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine(
            "HostName,TimeStamp,CpuUsage");

        foreach (var r in rows)
        {
            sb.AppendLine(
                $"{r.HostName}," +
                $"{r.TimeStamp}," +
                $"{r.CpuUsage}");
        }

        File.WriteAllText(
            file,
            sb.ToString());
    }

    public static void ExportMemory(
        List<MemoryRecord> rows)
    {
        string folder =
            ConfigurationManager.AppSettings["OutputFolder"];

        string file =
            Path.Combine(folder,
            "Memory.csv");

        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine(
            "HostName,TimeStamp,MemoryUsed,MemoryTotal");

        foreach (var r in rows)
        {
            sb.AppendLine(
                $"{r.HostName}," +
                $"{r.TimeStamp}," +
                $"{r.MemoryUsed}," +
                $"{r.MemoryTotal}");
        }

        File.WriteAllText(
            file,
            sb.ToString());
    }

    public static void ExportDisk(
        List<DiskRecord> rows)
    {
        string folder =
            ConfigurationManager.AppSettings["OutputFolder"];

        string file =
            Path.Combine(folder,
            "Disk.csv");

        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine(
            "HostName,DiskName,TimeStamp,DiskUsed,DiskTotal");

        foreach (var r in rows)
        {
            sb.AppendLine(
                $"{r.HostName}," +
                $"{r.DiskName}," +
                $"{r.TimeStamp}," +
                $"{r.DiskUsed}," +
                $"{r.DiskTotal}");
        }

        File.WriteAllText(
            file,
            sb.ToString());
    }
}
