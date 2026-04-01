# ==============================
# CONFIG
# ==============================
$SMTPServer = "smtp.company.com"
$From = "noreply@company.com"
$To = "team@company.com"
$Subject = "Daily ELK Performance Report"

$TimeRangeText = "Last 24 Hours"
$StartTime = (Get-Date).AddHours(-24).ToString("o")
$EndTime = (Get-Date).ToString("o")

# ==============================
# HTML SECTION BUILDER
# ==============================
function New-AppHtmlSection {
    param (
        [string]$AppName,
        [array]$Rows
    )

    $rowsHtml = ""

    foreach ($row in $Rows) {
        $rowsHtml += @"
<tr>
<td>$($row.Site)</td>
<td>$($row.Transactions)</td>
<td>$($row.AvgResponseTime)</td>
<td>$($row.UserCount)</td>
</tr>
"@
    }

    return @"
<table style='width:100%;border-collapse:collapse;font-family:Arial;margin-bottom:25px'>
<tr style='background:#2f75b5;color:white'>
<th colspan='4' style='padding:8px;text-align:left'>$AppName</th>
</tr>

<tr style='background:#d9e1f2'>
<th colspan='4' style='padding:6px;text-align:left'>$TimeRangeText</th>
</tr>

<tr style='background:#f2f2f2'>
<th>Site</th>
<th>Transactions</th>
<th>Avg Response Time (sec)</th>
<th>User Count</th>
</tr>

$rowsHtml
</table>
"@
}

# ==============================
# DATA TRANSFORMATION
# ==============================
function Convert-ElasticToRows {
    param (
        $ElasticResponse,
        [string]$AppName
    )

    $rows = @()

    # 🔴 Adjust based on your aggregation structure
    foreach ($bucket in $ElasticResponse.aggregations.app.buckets) {

        $siteName = $bucket.key

        $transactions = $bucket.doc_count
        $avgResponse = if ($bucket.avg_response.value) {
            [math]::Round($bucket.avg_response.value, 3)
        } else { 0 }

        $users = if ($bucket.unique_users.value) {
            $bucket.unique_users.value
        } else { 0 }

        $rows += [PSCustomObject]@{
            Site = $siteName
            Transactions = $transactions
            AvgResponseTime = $avgResponse
            UserCount = $users
        }
    }

    return $rows
}

# ==============================
# MAIN EXECUTION
# ==============================

$appList = @("PEMS","LTCOP","BHSM")  # Add all apps here

$fullHtml = @"
<html>
<body style='font-family:Arial'>
<h2>Application Performance Report</h2>
"@

foreach ($app in $appList) {

    Write-Host "Processing $app..."

    # 🔹 Your existing functions
    $dynamicApp = Get-DynamicAppName -App $app
    $hosts = Get-HostName -App $app

    $elasticResponse = Invoke-ElasticQuery `
        -App $dynamicApp `
        -Hosts $hosts `
        -StartTime $StartTime `
        -EndTime $EndTime

    if (-not $elasticResponse) {
        Write-Warning "$app returned no data"
        continue
    }

    # 🔹 Transform ELK → Table Rows
    $rows = Convert-ElasticToRows -ElasticResponse $elasticResponse -AppName $app

    if ($rows.Count -eq 0) {
        Write-Warning "$app has empty rows"
        continue
    }

    # 🔹 Build HTML Section
    $sectionHtml = New-AppHtmlSection -AppName $app -Rows $rows

    $fullHtml += $sectionHtml
}

$fullHtml += @"
<p>Thanks,<br/>Monitoring Team</p>
</body>
</html>
"@

# ==============================
# SEND EMAIL
# ==============================
Send-MailMessage `
    -From $From `
    -To $To `
    -Subject $Subject `
    -Body $fullHtml `
    -BodyAsHtml `
    -SmtpServer $SMTPServer

Write-Host "Email Sent Successfully"
