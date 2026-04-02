$appConfig = @(
    @{
        Key = "PA"
        DisplayName = "Public Application"
        Order = 1
        Description = "Handles public user transactions and submissions."

        Domains = @{
            "operations.test.com" = "Operations"
            "secure.test.com"     = "Secure"
            "state.test.com"      = "State"
        }
    },
    @{
        Key = "PB"
        DisplayName = "Provider Backend"
        Order = 2
        Description = "Used by internal provider management teams."

        Domains = @{}  # fallback to default if not mapped
    }
)

---------------

$allAppsData = @()

foreach ($app in $appConfig) {

    Write-Host "Processing $($app.Key)..."

    # 🔹 Your existing ELK call
    $appResults = Invoke-ElasticQuery -App $app.Key

    if (-not $appResults) { continue }

    # 🔥 FIX: Remove duplicate domains but KEEP raw values
    $grouped = $appResults | Group-Object Domain

    $rows = @()

    foreach ($g in $grouped) {

        $first = $g.Group[0]

        # ✅ Domain mapping (Requirement #1)
        $domainDisplay = if ($app.Domains.ContainsKey($g.Name)) {
            $app.Domains[$g.Name]
        } else {
            $g.Name
        }

        $rows += [PSCustomObject]@{
            Site = $domainDisplay
            Transactions = "{0:N0}" -f ([int]$first.Transactions)
            AvgResponseTime = "{0:N3}" -f ([double]$first.AvgResponseTimeSec)
            UserCount = "{0:N0}" -f ([int]$first.UserCount)
        }
    }

    # ✅ Store everything (including description)
    $allAppsData += [PSCustomObject]@{
        AppName = $app.DisplayName
        Description = $app.Description
        Rows = $rows
        Order = $app.Order
    }
}

----------------
function Build-HtmlBody {
    param ($AllAppsData)

    $html = @"
<html>
<body style='font-family:Arial'>
<h2>Application Performance Report</h2>
"@

    foreach ($app in ($AllAppsData | Sort-Object Order)) {

        $html += @"
<table style='width:100%;border-collapse:collapse;margin-bottom:20px'>

<tr style='background:#2f75b5;color:white'>
<th colspan='4' style='padding:8px;text-align:left'>$($app.AppName)</th>
</tr>

<tr style='background:#f2f2f2'>
<th>Site</th>
<th>Transactions</th>
<th>Avg Response Time (sec)</th>
<th>User Count</th>
</tr>
"@

        foreach ($row in $app.Rows) {
            $html += @"
<tr>
<td>$($row.Site)</td>
<td>$($row.Transactions)</td>
<td>$($row.AvgResponseTime)</td>
<td>$($row.UserCount)</td>
</tr>
"@
        }

        # ✅ Requirement #2 (Description / comment)
        if ($app.Description) {
            $html += @"
<tr>
<td colspan='4' style='font-size:12px;color:#555;padding:6px'>
$app.Description
</td>
</tr>
"@
        }

        $html += "</table>"
    }

    $html += "</body></html>"

    return $html
}
