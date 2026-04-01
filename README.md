$grouped = $appResults | Group-Object Domain

$rows = @()

foreach ($g in $grouped) {

    $transactions = ($g.Group | Measure-Object -Property Transactions -Sum).Sum
    $userCount = ($g.Group | Measure-Object -Property UserCount -Sum).Sum

    $avg = ($g.Group | Where-Object { $_.AvgResponseTime -ne $null } |
            Measure-Object -Property AvgResponseTime -Average).Average

    $rows += [PSCustomObject]@{
        Site = if ($g.Name) { $g.Name } else { "Unknown" }
        Transactions = if ($transactions) { "{0:N0}" -f $transactions } else { "0" }
        AvgResponseTime = if ($avg) { "{0:N3}" -f $avg } else { "0.000" }
        UserCount = if ($userCount) { "{0:N0}" -f $userCount } else { "0" }
    }
}//////////////////

function Build-HtmlBody {
    param (
        [array]$AllAppsData,
        [string]$TimeRange
    )

    $html = @"
<html>
<body style='font-family:Arial'>

<h2>Application Performance Report</h2>
<p>$TimeRange</p>

"@

    foreach ($app in $AllAppsData) {

        $html += @"
<table style='width:100%;border-collapse:collapse;margin-bottom:25px'>
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

        $html += "</table>"
    }

    $html += @"
<p>Thanks,<br/>Monitoring Team</p>
</body>
</html>
"@

    return $html
}


=========
$allAppsData = @()

foreach ($app in $appList) {

    $appResults = Invoke-ElasticQuery ... # your existing call

    # 🔹 Group + transform (your logic)
    $grouped = $appResults | Group-Object Domain

    $rows = @()

    foreach ($g in $grouped) {

        $transactions = ($g.Group | Measure-Object -Property Transactions -Sum).Sum
        $userCount = ($g.Group | Measure-Object -Property UserCount -Sum).Sum

        $avg = ($g.Group | Where-Object { $_.AvgResponseTime -ne $null } |
                Measure-Object -Property AvgResponseTime -Average).Average

        $rows += [PSCustomObject]@{
            Site = if ($g.Name) { $g.Name } else { "Unknown" }
            Transactions = "{0:N0}" -f ($transactions ? $transactions : 0)
            AvgResponseTime = "{0:N3}" -f ($avg ? $avg : 0)
            UserCount = "{0:N0}" -f ($userCount ? $userCount : 0)
        }
    }

    # 🔹 Store data (NOT HTML)
    $allAppsData += [PSCustomObject]@{
        AppName = $app
        Rows = $rows
    }
}

# ✅ BUILD HTML ONLY ONCE
$htmlBody = Build-HtmlBody -AllAppsData $allAppsData -TimeRange "Last 24 Hours"


---------------------------------=
