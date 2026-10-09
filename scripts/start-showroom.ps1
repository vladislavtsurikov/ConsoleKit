[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $ShowroomArguments
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$showroomProject = Join-Path $repositoryRoot 'Showroom\VladislavTsurikov.ConsoleKit.Showroom\VladislavTsurikov.ConsoleKit.Showroom.csproj'

if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue))
{
    throw 'The .NET SDK is required to start the ConsoleKit Showroom.'
}

& dotnet run --project $showroomProject --configuration $Configuration -- $ShowroomArguments
exit $LASTEXITCODE
