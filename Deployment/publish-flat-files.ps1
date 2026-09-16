#Requires -Version 5.1

<#
.SYNOPSIS
    Packs Cynicszm.FlatFiles and publishes it to NuGet.

.DESCRIPTION
    Packs the library in Release configuration and pushes exactly the package that was
    produced. The package is left in place if the push fails, so a failed publish can be
    retried or inspected rather than silently discarded.

.PARAMETER ApiKey
    The NuGet API key. Defaults to the NUGET_API_KEY environment variable. The key is never
    written to disk or echoed; supply it per run or set it in the environment.

.PARAMETER Source
    The push source. Defaults to the nuget.org v3 endpoint.

.PARAMETER OutputPath
    Where to place the package. Defaults to an 'artifacts' folder beside this script, which
    is emptied first so a stale package from an earlier version cannot be pushed by mistake.

.EXAMPLE
    .\publish-flat-files.ps1 -ApiKey $key

.EXAMPLE
    .\publish-flat-files.ps1 -WhatIf
    Packs and reports what would be pushed, without pushing.
#>
[CmdletBinding( SupportsShouldProcess = $true )]
param(
    [string] $ApiKey = $env:NUGET_API_KEY,
    [string] $Source = 'https://api.nuget.org/v3/index.json',
    [string] $OutputPath = ( Join-Path $PSScriptRoot 'artifacts' )
)

$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot '..\FlatFiles\FlatFiles.csproj'
if (-not (Test-Path $projectPath))
{
    throw "Could not find the project at $projectPath."
}

# Pack into an empty folder so the push below can pick up the package by what was just
# produced rather than by a wildcard, which would also match a package left over from an
# earlier version.
if (Test-Path $OutputPath)
{
    Remove-Item -Path (Join-Path $OutputPath '*.nupkg') -Force -ErrorAction SilentlyContinue
}
else
{
    # Not gated by -WhatIf: the pack below needs somewhere to write even on a dry run.
    New-Item -ItemType Directory -Path $OutputPath -Force -WhatIf:$false | Out-Null
}

Write-Host "Packing $projectPath"
& dotnet pack $projectPath --configuration Release --output $OutputPath
if ($LASTEXITCODE -ne 0)
{
    throw "dotnet pack failed with exit code $LASTEXITCODE."
}

$package = Get-ChildItem -Path $OutputPath -Filter '*.nupkg' | Sort-Object LastWriteTime | Select-Object -Last 1
if ($null -eq $package)
{
    throw "dotnet pack reported success but produced no package in $OutputPath."
}

Write-Host "Packed $($package.Name) ($([math]::Round($package.Length / 1MB, 2)) MB)"

if (-not $PSCmdlet.ShouldProcess( $package.Name, "Push to $Source" ))
{
    Write-Host 'Nothing pushed. Re-run without -WhatIf to publish.'
    return
}

if ([string]::IsNullOrWhiteSpace( $ApiKey ))
{
    throw 'No API key. Pass -ApiKey, or set the NUGET_API_KEY environment variable.'
}

& dotnet nuget push $package.FullName --source $Source --api-key $ApiKey
if ($LASTEXITCODE -ne 0)
{
    # Deliberately leaving the package in place: a failed push is worth retrying, and a
    # version that is already published fails here rather than being quietly deleted.
    throw "dotnet nuget push failed with exit code $LASTEXITCODE. The package is still at $($package.FullName)."
}

Write-Host "Published $($package.Name) to $Source"
Remove-Item $package.FullName -Force
