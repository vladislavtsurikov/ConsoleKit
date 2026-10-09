[CmdletBinding()]
param(
    # The product folder under %LOCALAPPDATA% (LogFileOptions.ProductName).
    [string] $Product = 'MCPFamily',
    # Print the path instead of opening it.
    [switch] $PathOnly
)

# Opens the folder with the log sessions of a product (%LOCALAPPDATA%\<Product>\logs).
$directory = Join-Path $env:LOCALAPPDATA (Join-Path $Product 'logs')
if ($PathOnly)
{
    Write-Output $directory
    exit 0
}

if (-not (Test-Path $directory))
{
    Write-Warning "No logs yet: $directory"
    exit 0
}

Invoke-Item $directory
