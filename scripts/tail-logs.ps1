[CmdletBinding()]
param(
    # The product folder under %LOCALAPPDATA% (LogFileOptions.ProductName).
    [string] $Product = 'MCPFamily',
    # Only sessions of this application (e.g. Worker).
    [string] $App,
    # Lines shown before following.
    [int] $Last = 20,
    # Show only Warning, Error and Fatal entries.
    [switch] $Problems
)

# Follows the newest running session of a product and prints its entries as readable lines.
$directory = Join-Path $env:LOCALAPPDATA (Join-Path $Product 'logs')
$pattern = if ($App) { "current-$App-*.jsonl" } else { 'current-*.jsonl' }
$file = Get-ChildItem -Path $directory -Filter $pattern -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $file)
{
    Write-Warning "No running session in $directory"
    exit 0
}

Write-Host "Following $($file.FullName)" -ForegroundColor DarkGray
Get-Content -Path $file.FullName -Tail $Last -Wait | ForEach-Object {
    if ([string]::IsNullOrWhiteSpace($_)) { return }
    $entry = $_ | ConvertFrom-Json
    if ($null -ne $entry.session) { return }
    if ($Problems -and $entry.l -notin @('Warning', 'Error', 'Fatal')) { return }

    $time = ([DateTimeOffset]::Parse($entry.t)).ToLocalTime().ToString('HH:mm:ss.fff')
    $color = switch ($entry.l) { 'Warning' { 'Yellow' } { $_ -in 'Error', 'Fatal' } { 'Red' } default { 'Gray' } }
    $stream = if ($entry.stream) { " $($entry.stream)" } else { '' }
    Write-Host "$time [$($entry.l)] [$($entry.src)$stream] $($entry.msg)" -ForegroundColor $color
}
