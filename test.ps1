param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AllArgs
)

# Output folder
$folder = "C:\UnifiedUtility\Reports\Test"

if (!(Test-Path $folder)) {
    New-Item -ItemType Directory -Path $folder | Out-Null
}

# File path
$file = "$folder\args_$(Get-Date -Format yyyyMMddHHmmss).html"

# Build HTML
$html = "<html><body>"
$html += "<h2>Argument Test Output</h2>"

if ($AllArgs.Count -eq 0) {
    $html += "<p>No arguments passed</p>"
} else {
    $i = 1
    foreach ($arg in $AllArgs) {
        $html += "<p><b>Arg $i:</b> $arg</p>"
        $i++
    }
}

$html += "</body></html>"

# Write file
$html | Out-File $file

# Also print to console (for debugging)
Write-Output "Arguments received:"
$i = 1
foreach ($arg in $AllArgs) {
    Write-Output "Arg $i: $arg"
    $i++
}

# Return file path (IMPORTANT for your EXE)
Write-Output $file
