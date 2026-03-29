using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string elasticHost = "http://your-elastic-host:9200";
        string username = "your-username";
        string password = "your-password";

        var apps = new List<string> { "pems", "ltc", "sms" };

        string startTime = "2026-03-18T20:00:00.000Z";
        string endTime = "2026-03-18T20:15:00.000Z";

        var handler = new HttpClientHandler()
        {
            Credentials = new System.Net.NetworkCredential(username, password)
        };

        using var client = new HttpClient(handler);

        // 🔹 STEP 1: Create PIT
        var pitResponse = await client.PostAsync(
            $"{elasticHost}/iis-*/_pit?keep_alive=2m",
            null);

        var pitJson = await pitResponse.Content.ReadAsStringAsync();
        var pitDoc = JsonDocument.Parse(pitJson);
        string pitId = pitDoc.RootElement.GetProperty("id").GetString();

        Console.WriteLine($"PIT Created: {pitId}");

        var results = new List<AppResult>();

        // 🔹 STEP 2: Loop Apps
        foreach (var app in apps)
        {
            Console.WriteLine($"Processing {app}...");

            var body = BuildQuery(app, pitId, startTime, endTime);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{elasticHost}/_search", content);
            var json = await response.Content.ReadAsStringAsync();

            var doc = JsonDocument.Parse(json);

            // ✅ Aggregation-based count (accurate, no 10k limit)
            long count = doc.RootElement
                .GetProperty("aggregations")
                .GetProperty("total_count")
                .GetProperty("value")
                .GetInt64();

            double avgMs = doc.RootElement
                .GetProperty("aggregations")
                .GetProperty("avg_time")
                .GetProperty("value")
                .GetDouble();

            double avgSec = Math.Round(avgMs / 1000, 2);

            results.Add(new AppResult
            {
                Application = app,
                Transactions = count,
                AvgResponseTimeSec = avgSec
            });
        }

        // 🔹 STEP 3: Delete PIT
        var deleteBody = JsonSerializer.Serialize(new { id = pitId });
        await client.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Delete,
            RequestUri = new Uri($"{elasticHost}/_pit"),
            Content = new StringContent(deleteBody, Encoding.UTF8, "application/json")
        });

        Console.WriteLine("PIT Deleted");

        // 🔹 STEP 4: Output
        Console.WriteLine("\nResults:");
        foreach (var r in results)
        {
            Console.WriteLine($"{r.Application} | Count: {r.Transactions} | Avg(s): {r.AvgResponseTimeSec}");
        }
    }

    static string BuildQuery(string app, string pitId, string start, string end)
    {
        var query = new
        {
            size = 0,

            pit = new
            {
                id = pitId,
                keep_alive = "2m"
            },

            aggs = new
            {
                total_count = new
                {
                    value_count = new
                    {
                        field = "_id"
                    }
                },
                avg_time = new
                {
                    avg = new
                    {
                        field = "timetaken"
                    }
                }
            },

            query = new
            {
                @bool = new
                {
                    must = new object[]
                    {
                        new
                        {
                            regexp = new
                            {
                                uri_target_keyword = new
                                {
                                    value = $"/{app.ToUpper()}(/.*)?",
                                    case_insensitive = true
                                }
                            }
                        },
                        new
                        {
                            match_phrase = new
                            {
                                cs_host = "secure.test.com"
                            }
                        }
                    },

                    must_not = new object[]
                    {
                        new
                        {
                            regexp = new
                            {
                                uri_target_keyword = new
                                {
                                    value = "/(.*)/(rb|ruxitagentjs).*",
                                    case_insensitive = true
                                }
                            }
                        },
                        new
                        {
                            regexp = new
                            {
                                username = new
                                {
                                    value = ".*portal.test.*",
                                    case_insensitive = true
                                }
                            }
                        }
                    },

                    filter = new object[]
                    {
                        new
                        {
                            range = new
                            {
                                @timestamp = new
                                {
                                    gte = start,
                                    lte = end
                                }
                            }
                        }
                    }
                }
            }
        };

        // 🔥 IMPORTANT: Fix field names (ES needs ".keyword")
        string json = JsonSerializer.Serialize(query)
            .Replace("uri_target_keyword", "uri_target.keyword")
            .Replace("cs_host", "cs-host");

        return json;
    }
}

class AppResult
{
    public string Application { get; set; }
    public long Transactions { get; set; }
    public double AvgResponseTimeSec { get; set; }
}
