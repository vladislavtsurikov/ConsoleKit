[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'ConsoleKit.slnx'

if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue))
{
    throw 'The .NET SDK is required to test ConsoleKit.'
}

& dotnet test $solutionPath --configuration $Configuration
exit $LASTEXITCODE
