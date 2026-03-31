4.read from DB

public List<TransclassMetric> GetMetrics(string connectionString)
{
    var list = new List<TransclassMetric>();

    using (var conn = new SqlConnection(connectionString))
    {
        conn.Open();

        var cmd = new SqlCommand(@"
            SELECT ApplicationName, TransClass,
                   COUNT(*) AS RequestCount,
                   AVG(DurationMs) AS AvgResponseTime
            FROM Transactions
            WHERE IsProcessed = 1 AND IsDynatraceSent = 0
            GROUP BY ApplicationName, TransClass", conn);

        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                list.Add(new TransclassMetric
                {
                    ApplicationName = reader["ApplicationName"].ToString(),
                    TransClass = reader["TransClass"].ToString(),
                    RequestCount = Convert.ToInt32(reader["RequestCount"]),
                    AvgResponseTime = Convert.ToDouble(reader["AvgResponseTime"])
                });
            }
        }
    }

    return list;
}

5. Dynatrace Sender
public class DynatraceSender
{
    private readonly HttpClient _client;
    private readonly string _url;

    public DynatraceSender(string url, string token)
    {
        _url = url;
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("Authorization", $"Api-Token {token}");
    }

    public async Task SendAsync(List<TransclassMetric> metrics)
    {
        var lines = new List<string>();

        foreach (var m in metrics)
        {
            var app = Sanitize(m.ApplicationName);
            var tc = Sanitize(m.TransClass);

            lines.Add($"custom.transclass.response,app={app},transclass={tc} avg={m.AvgResponseTime}");
            lines.Add($"custom.transclass.count,app={app},transclass={tc} count={m.RequestCount}");
        }

        var content = new StringContent(string.Join("\n", lines));

        var response = await _client.PostAsync(_url, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Dynatrace error: {error}");
        }
    }

    private string Sanitize(string value)
    {
        return value.Replace(" ", "_").Replace(",", "_");
    }
}

6. Mark Records as Sent

public void MarkAsSent(string connectionString)
{
    using (var conn = new SqlConnection(connectionString))
    {
        conn.Open();

        var cmd = new SqlCommand(@"
            UPDATE Transactions
            SET IsDynatraceSent = 1
            WHERE IsProcessed = 1 AND IsDynatraceSent = 0", conn);

        cmd.ExecuteNonQuery();
    }
}

7 main job

public async Task RunJob()
{
    string connStr = "your-db-connection";

    // Step 1: Get aggregated metrics
    var metrics = GetMetrics(connStr);

    if (metrics.Count == 0)
        return;

    // Step 2: Send to Dynatrace
    var sender = new DynatraceSender(
        "https://<your-dynatrace>/api/v2/metrics/ingest",
        "<your-token>");

    await sender.SendAsync(metrics);

    // Step 3: Mark as sent
    MarkAsSent(connStr);
}

8 local test
Console.WriteLine(string.Join("\n", lines));

Dynatrace test
avg:custom.transclass.response:splitBy(app,transclass)
avg:custom.transclass.response:splitBy(app,transclass)
