private string BuildSearchRequest(
    DateTime startDate,
    DateTime endDate,
    object[] searchAfter,
    int batchSize)
{
    var sb = new StringBuilder();

    sb.Append("{");

    sb.Append("\"size\":").Append(batchSize).Append(",");

    sb.Append("\"track_total_hits\":true,");

    // Sort
    sb.Append("\"sort\":[");
    sb.Append("{");
    sb.Append("\"@timestamp\":{");
    sb.Append("\"order\":\"asc\",");
    sb.Append("\"format\":\"strict_date_optional_time_nanos\"");
    sb.Append("}");
    sb.Append("},");

    sb.Append("{");
    sb.Append("\"_doc\":{");
    sb.Append("\"order\":\"asc\"");
    sb.Append("}");
    sb.Append("}");
    sb.Append("],");

    // Fields
    sb.Append("\"fields\":[");
    sb.Append("{");
    sb.Append("\"field\":\"*\",");
    sb.Append("\"include_unmapped\":true");
    sb.Append("}");
    sb.Append("],");

    // _source
    sb.Append("\"_source\":false,");

    // version
    sb.Append("\"version\":true,");

    // Query
    sb.Append("\"query\":{");
    sb.Append("\"range\":{");
    sb.Append("\"@timestamp\":{");
    sb.Append("\"gte\":\"")
      .Append(startDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))
      .Append("\",");

    sb.Append("\"lte\":\"")
      .Append(endDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))
      .Append("\"");

    sb.Append("}");
    sb.Append("}");
    sb.Append("}");

    // search_after
    if (searchAfter != null && searchAfter.Length > 0)
    {
        sb.Append(",\"search_after\":[");

        for (int i = 0; i < searchAfter.Length; i++)
        {
            if (i > 0)
                sb.Append(",");

            if (searchAfter[i] is string)
                sb.Append("\"").Append(searchAfter[i]).Append("\"");
            else
                sb.Append(searchAfter[i]);
        }

        sb.Append("]");
    }

    sb.Append("}");

    return sb.ToString();
}
