<#
Production-ready skeleton for exporting Elasticsearch data using PIT + search_after.
Fill in authentication if required.
#>

param(
    [ValidateSet("Prod","Test","Regression")]
    [string]$Environment="Test",
    [string]$Index="portalweb-*",
    [datetime]$StartDate,
    [datetime]$EndDate,
    [string]$OutputFile=".\Export.csv",
    [int]$BatchSize=5000
)

$Config=@{
 Prod="http://prod-elastic:9200"
 Test="http://test-elastic:9200"
 Regression="http://regr-elastic:9200"
}

$ElasticHost=$Config[$Environment]

function New-PIT{
 param($Index)
 $body=@{keep_alive="5m"}|ConvertTo-Json
 Invoke-RestMethod -Method Post -Uri "$ElasticHost/$Index/_pit?keep_alive=5m" -ContentType "application/json"
}

function Close-PIT($PitId){
 $b=@{id=$PitId}|ConvertTo-Json
 Invoke-RestMethod -Method Delete -Uri "$ElasticHost/_pit" -Body $b -ContentType "application/json"|Out-Null
}

$pit=New-PIT $Index
$pitId=$pit.id

$searchAfter=$null
$first=$true
$total=0

try{
 while($true){
   $query=@{
     size=$BatchSize
     pit=@{id=$pitId;keep_alive="5m"}
     sort=@(
       @{"@timestamp"=@{order="asc";format="strict_date_optional_time_nanos"}},
       @{"_shard_doc"="asc"}
     )
     track_total_hits=$true
     query=@{
       range=@{
         "@timestamp"=@{
           gte=$StartDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
           lte=$EndDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
         }
       }
     }
   }
   if($searchAfter){$query.search_after=$searchAfter}
   $json=$query|ConvertTo-Json -Depth 20
   $resp=Invoke-RestMethod -Method Post -Uri "$ElasticHost/_search" -Body $json -ContentType "application/json"
   $hits=$resp.hits.hits
   if(!$hits -or $hits.Count -eq 0){break}

   $rows=foreach($h in $hits){
      $row=[ordered]@{}
      foreach($p in $h.fields.PSObject.Properties){
        $v=$p.Value
        if($v -is [System.Array]){$row[$p.Name]=$v -join ";"}
        else{$row[$p.Name]=$v}
      }
      [pscustomobject]$row
   }

   if($first){
      $rows | Export-Csv $OutputFile -NoTypeInformation
      $first=$false
   }else{
      $rows | Export-Csv $OutputFile -NoTypeInformation -Append
   }

   $total += $hits.Count
   Write-Host "Exported $total records..."
   $searchAfter=$hits[-1].sort
 }
 Write-Host "Completed. Total exported: $total"
}
finally{
 if($pitId){Close-PIT $pitId}
}
