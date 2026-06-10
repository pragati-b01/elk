1. JSON Configuration

Instead of App.config:

{
  "Dynatrace": {
    "BaseUrl": "https://tenant.live.dynatrace.com",
    "ApiToken": "xxxx",

    "Hosts": [
      "testhost1.com",
      "testhost2.com"
    ],

    "TimeRange": {
      "Mode": "Relative",
      "From": "-1d",
      "To": "now"
    },

    "OutputFolder": "C:\\Temp",

    "Metrics": {
      "Cpu": {
        "Resolution": "5m",
        "Selector": "builtin:host.cpu.usage"
      },

      "Memory": {
        "Resolution": "5m",
        "Selector": "builtin:host.mem.(used,total)"
      },

      "Disk": {
        "Resolution": "5m",
        "Selector": "builtin:host.disk.(used,avail)"
      }
    }
  }
}
2. Configuration Classes
public class DynatraceConfig
{
    public string BaseUrl { get; set; }
    public string ApiToken { get; set; }

    public List<string> Hosts { get; set; }

    public TimeRangeConfig TimeRange { get; set; }

    public string OutputFolder { get; set; }

    public MetricsConfig Metrics { get; set; }
}

public class TimeRangeConfig
{
    public string Mode { get; set; }

    public string From { get; set; }

    public string To { get; set; }
}

public class MetricsConfig
{
    public MetricConfig Cpu { get; set; }

    public MetricConfig Memory { get; set; }

    public MetricConfig Disk { get; set; }
}

public class MetricConfig
{
    public string Resolution { get; set; }

    public string Selector { get; set; }
}
3. Read JSON

Newtonsoft.Json

using Newtonsoft.Json;

string json = File.ReadAllText("config.json");

var root =
    JsonConvert.DeserializeObject<RootConfig>(json);

var config = root.Dynatrace;

Wrapper:

public class RootConfig
{
    public DynatraceConfig Dynatrace { get; set; }
}
4. Build Host Filter

Hosts:

[
  "host1",
  "host2",
  "host3"
]

Convert to:

entitySelector=type(HOST),entityName.in("host1","host2","host3")

Code:

private string BuildHostSelector(List<string> hosts)
{
    return string.Join(",",
        hosts.Select(h => $"\"{h}\""));
}

Usage:

string hostFilter =
    BuildHostSelector(config.Hosts);

Result:

"host1","host2","host3"
5. Build Relative / Absolute Time

This was one of the action items from discussion.

private string BuildTimeQuery(TimeRangeConfig timeRange)
{
    if (timeRange.Mode.Equals(
        "Relative",
        StringComparison.OrdinalIgnoreCase))
    {
        return $"from={timeRange.From}&to={timeRange.To}";
    }

    return $"from={Uri.EscapeDataString(timeRange.From)}" +
           $"&to={Uri.EscapeDataString(timeRange.To)}";
}

Relative:

{
 "Mode":"Relative",
 "From":"-1d",
 "To":"now"
}

Produces:

from=-1d&to=now

Absolute:

{
 "Mode":"Absolute",
 "From":"2026-06-01T00:00:00Z",
 "To":"2026-06-02T00:00:00Z"
}

Produces:

from=2026-06-01T00%3A00%3A00Z
&to=2026-06-02T00%3A00%3A00Z
6. Build Metrics API

Single generic method

public string BuildMetricsUrl(
    DynatraceConfig config,
    MetricConfig metric)
{
    string hosts =
        BuildHostSelector(config.Hosts);

    string time =
        BuildTimeQuery(config.TimeRange);

    return
        $"{config.BaseUrl}/api/v2/metrics/query?" +
        $"metricSelector={metric.Selector}" +
        $"&entitySelector=type(HOST)," +
        $"entityName.in({hosts})" +
        $"&resolution={metric.Resolution}" +
        $"&{time}";
}
CPU API
var cpuUrl =
    BuildMetricsUrl(
        config,
        config.Metrics.Cpu);

Produces:

/api/v2/metrics/query
?metricSelector=builtin:host.cpu.usage
&entitySelector=type(HOST),
entityName.in("host1","host2")
&resolution=5m
&from=-1d
&to=now
Memory API

Single call

builtin:host.mem.(used,total)
var memoryUrl =
    BuildMetricsUrl(
        config,
        config.Metrics.Memory);
Disk API

Single call

builtin:host.disk.(used,avail)
var diskUrl =
    BuildMetricsUrl(
        config,
        config.Metrics.Disk);
7. Host Lookup API

Your lead suggested:

Execute one query and keep hostId -> hostName in memory.

Example:

{
  "entities": [
    {
      "entityId":"HOST-ABC123",
      "displayName":"testhost1.com"
    }
  ]
}

Model:

public class EntityResponse
{
    public List<Entity> Entities { get; set; }
}

public class Entity
{
    public string EntityId { get; set; }

    public string DisplayName { get; set; }
}

Dictionary:

var hostLookup =
    response.Entities
        .ToDictionary(
            x => x.EntityId,
            x => x.DisplayName);

Result:

hostLookup["HOST-ABC123"]

returns

testhost1.com
8. Disk Lookup

Dynatrace usually returns dimensions like:

[
  "HOST-ABC123",
  "C:"
]

or

[
  "HOST-ABC123",
  "\\Device\\HarddiskVolume1"
]

Create:

public class DiskLookup
{
    public string HostId { get; set; }

    public string DiskName { get; set; }
}

Dictionary:

Dictionary<string,string> diskLookup

Key:

HOST-ABC123|DISK-123

Value:

C:
9. During CSV Export

Current response:

{
  "dimensions": [
      "HOST-ABC123"
  ]
}

Export:

string hostId =
    dataPoint.Dimensions[0];

record.HostId = hostId;

record.HostName =
    hostLookup.ContainsKey(hostId)
        ? hostLookup[hostId]
        : hostId;

For disk:

record.DiskName =
    diskLookup[key];
Architecture I would use
JSON Config
      |
      V
Build Time Filter
      |
      V
Build Host Filter
      |
      V
Host Lookup API (1 call)
      |
      +------> Dictionary<HostId,HostName>
      |
      V
CPU Metrics API
Memory Metrics API
Disk Metrics API
      |
      V
Map HostId -> HostName
      |
      V
Export CSV

This approach exactly matches what your lead described:
