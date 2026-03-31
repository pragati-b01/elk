# ==============================
# 1. CONFIGURATION SECTION
# ==============================

$config = @{
    ElasticHost = "https://your-elastic-host:9200"

    Credential = Get-Credential   # or secure vault

    domainField = "co-host"

    domains = @{
        Stage  = "stage.test.com"
        Secure = "secure.test.com"
        Oper   = "operations.test.com"
    }

    common = @{
        must = @(
            # Add common filters here if needed
        )
        mustNot = @()
    }

    apps = @{
        PA = @{
            index = "iis-*"
            mustNot = @(
                @{
                    regexp = @{
                        "uri_target.keyword" = @{
                            value = "/(.*)/(rb|ruxitagentjs).*"
                            case_insensitive = $true
                        }
                    }
                }
            )
            must = @()
        }

        PB = @{
            index = "iis-web*"
            must = @(
                # example app-specific must filter
                # @{ term = @{ "status.keyword" = "200" } }
            )
            mustNot = @()
        }

        PC = @{
            index = "iis-web*"
            must = @()
            mustNot = @()
        }
    }
}

# ==============================
# 2. FUNCTION: BUILD FILTERS
# ==============================

function Build-Filters {
    param($config, $app, $domainValue)

    $domainField = $config.domainField

    # domain filter (IMPORTANT FIX)
    $domainFilter = @(
        @{
            match_phrase = @{
                $domainField = $domainValue
            }
        }
    )

    $must =
        $config.common.must +
        $domainFilter +
        $config.apps[$app].must

    $mustNot =
        $config.common.mustNot +
        $config.apps[$app].mustNot

    return @{
        must = $must
        mustNot = $mustNot
    }
}

# ==============================
# 3. FUNCTION: EXECUTE QUERY
# ==============================

function Invoke-ElasticQuery {
    param(
        $config,
        $app,
        $domainKey
    )

    $domainValue = $config.domains[$domainKey]
    $filters = Build-Filters $config $app $domainValue

    $index = $config.apps[$app].index
    $uri = "$($config.ElasticHost)/$index/_search"

    $body = @{
        size = 0
        query = @{
            bool = @{
                must = $filters.must
                must_not = $filters.mustNot
            }
        }
        aggs = @{
            avg_response = @{
                avg = @{
                    field = "response_time"
                }
            }
            total_hits = @{
                value_count = @{
                    field = "_id"
                }
            }
        }
    } | ConvertTo-Json -Depth 10

    try {
        $response = Invoke-RestMethod -Method POST `
            -Uri $uri `
            -Credential $config.Credential `
            -Body $body `
            -ContentType "application/json"

        return [PSCustomObject]@{
            App     = $app
            Domain  = $domainKey
            Avg     = $response.aggregations.avg_response.value
            Count   = $response.aggregations.total_hits.value
        }
    }
    catch {
        return [PSCustomObject]@{
            App     = $app
            Domain  = $domainKey
            Avg     = 0
            Count   = 0
            Error   = $_.Exception.Message
        }
    }
}

# ==============================
# 4. PARALLEL EXECUTION ENGINE
# ==============================

$Apps = @("PA", "PB", "PC")
$Domains = @("Stage", "Secure", "Oper")

$Results = [System.Collections.Concurrent.ConcurrentBag[object]]::new()

$Jobs = foreach ($app in $Apps) {
    foreach ($domain in $Domains) {

        Start-Job -ScriptBlock {
            param($config, $app, $domain)

            # recreate function inside job
            function Build-Filters {
                param($config, $app, $domainValue)

                $domainField = $config.domainField

                $domainFilter = @(
                    @{
                        match_phrase = @{
                            $domainField = $domainValue
                        }
                    }
                )

                $must =
                    $config.common.must +
                    $domainFilter +
                    $config.apps[$app].must

                $mustNot =
                    $config.common.mustNot +
                    $config.apps[$app].mustNot

                return @{
                    must = $must
                    mustNot = $mustNot
                }
            }

            $filters = Build-Filters $config $app $config.domains[$domain]

            $index = $config.apps[$app].index
            $uri = "$($config.ElasticHost)/$index/_search"

            $body = @{
                size = 0
                query = @{
                    bool = @{
                        must = $filters.must
                        must_not = $filters.mustNot
                    }
                }
                aggs = @{
                    avg_response = @{
                        avg = @{ field = "response_time" }
                    }
                }
            } | ConvertTo-Json -Depth 10

            try {
                $response = Invoke-RestMethod -Method POST `
                    -Uri $uri `
                    -Credential $config.Credential `
                    -Body $body `
                    -ContentType "application/json"

                [PSCustomObject]@{
                    App    = $app
                    Domain = $domain
                    Avg    = $response.aggregations.avg_response.value
                }
            }
            catch {
                [PSCustomObject]@{
                    App    = $app
                    Domain = $domain
                    Avg    = 0
                }
            }

        } -ArgumentList $config, $app, $domain
    }
}

# Wait for all jobs
$Jobs | Wait-Job | ForEach-Object {
    $Results.Add((Receive-Job $_))
}

# Cleanup
$Jobs | Remove-Job

# ==============================
# 5. FINAL OUTPUT FORMAT
# ==============================

$Grouped = $Results | Group-Object App

foreach ($appGroup in $Grouped) {

    Write-Host "`n===================="
    Write-Host $appGroup.Name
    Write-Host "===================="

    $appGroup.Group | Format-Table -AutoSize
}
