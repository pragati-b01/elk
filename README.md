$grouped = $appResults | Group-Object Domain

$rows = @()

foreach ($g in $grouped) {

    $transactions = ($g.Group | Measure-Object -Property Transactions -Sum).Sum
    $userCount = ($g.Group | Measure-Object -Property UserCount -Sum).Sum

    $avg = ($g.Group | Where-Object { $_.AvgResponseTime -ne $null } |
            Measure-Object -Property AvgResponseTime -Average).Average

    $rows += [PSCustomObject]@{
        Site = if ($g.Name) { $g.Name } else { "Unknown" }
        Transactions = if ($transactions) { $transactions } else { 0 }
        AvgResponseTime = if ($avg) { [math]::Round($avg, 3) } else { 0 }
        UserCount = if ($userCount) { $userCount } else { 0 }
    }
}
