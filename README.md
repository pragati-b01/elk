$config = @{
    common = @{
        must = @(
            @{ term = @{ "environment.keyword" = "prod" } }
        )

        mustNot = @(
            @{ term = @{ "status.keyword" = "healthcheck" } }
        )
    }

    apps = @{
        PA = @{
            index = "iis-*"

            must = @()
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
        }

        PB = @{
            index = "iis-web*"

            must = @()
            mustNot = @()
        }

        PC = @{
            index = "iis-api*"

            must = @()
            mustNot = @()
        }
    }

    domains = @{
        Stage = @{
            must = @(
                @{ term = @{ "domain.keyword" = "stage" } }
            )
            mustNot = @()
        }

        secure = @{
            must = @(
                @{ term = @{ "domain.keyword" = "secure" } }
            )
            mustNot = @()
        }

        opers = @{
            must = @(
                @{ term = @{ "domain.keyword" = "operations" } }
            )
            mustNot = @()
        }
    }
}
