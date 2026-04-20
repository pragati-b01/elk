# Create output folder
$folder = "C:\UnifiedUtility\Reports\Test"

if (!(Test-Path $folder)) {
    New-Item -ItemType Directory -Path $folder | Out-Null
}

# Output file
$file = "$folder\test_$(Get-Date -Format yyyyMMddHHmmss).html"

# Capture ALL raw arguments (works even if none passed)
$argsList = $args

# Build HTML
$html = "<html><body>"
$html += "<h2>Simple Argument Test</h2>"

if ($argsList.Count -eq 0) {
    $html += "<p>No arguments received</p>"
} else {
    $i = 1
    foreach ($a in $argsList) {
        $html += "<p>Arg $i: $a</p>"
        $i++
    }
}

$html += "</body></html>"

# Write file
$html | Out-File $file

# Print to console also
Write-Output "Arguments received:"
$i = 1
foreach ($a in $argsList) {
    Write-Output "Arg $i: $a"
    $i++
}

# IMPORTANT: return file path for your EXE
Write-Output $file
