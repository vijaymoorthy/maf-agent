Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & $script:DotNetPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

try {
    $dotnet = Get-Command dotnet -CommandType Application
    $script:DotNetPath = $dotnet.Source

    $sdkVersion = & $DotNetPath --version
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet --version failed with exit code $LASTEXITCODE."
    }

    $dotnetInfo = & $DotNetPath --info
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet --info failed with exit code $LASTEXITCODE."
    }

    $sdkPathMatch = $dotnetInfo | Select-String -Pattern '^\s*Base Path:\s*(.+)$' | Select-Object -First 1
    $sdkPath = if ($null -ne $sdkPathMatch) { $sdkPathMatch.Matches[0].Groups[1].Value.Trim() } else { '<unavailable>' }

    Write-Host "Resolved .NET SDK path: $sdkPath"
    Write-Host "Resolved .NET SDK version: $sdkVersion"

    if ($sdkVersion -notmatch '^9\.') {
        throw "The resolved .NET SDK must be 9.x, but version $sdkVersion was selected."
    }

    Push-Location (Split-Path -Parent $PSScriptRoot)
    try {
        Invoke-DotNet -Arguments @('build', '--configuration', 'Release')
        Invoke-DotNet -Arguments @('test', '--configuration', 'Release', '--no-build')
    }
    finally {
        Pop-Location
    }
}
catch {
    Write-Error $_
    exit 1
}
