param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'artifacts\EousGate-win-x64'),
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

function Invoke-Checked([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed (exit code $LASTEXITCODE): $FilePath $($Arguments -join ' ')"
    }
}

$project = Join-Path $PSScriptRoot 'EousGate.csproj'
$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
$releaseVersion = @($projectXml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })[0]
if ([string]::IsNullOrWhiteSpace($releaseVersion) -or $releaseVersion -notmatch '^[0-9A-Za-z.-]+$') {
    throw "EousGate.csproj must define a release-safe Version value."
}
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$workspaceRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)

if (-not $resolvedOutput.StartsWith($workspaceRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish directory must be inside the project: $resolvedOutput"
}

if (Test-Path -LiteralPath $resolvedOutput) {
    $lockedByEousGate = @(
        Get-Process -Name 'EousGate' -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                if ($_.Path -and $_.Path.StartsWith($resolvedOutput + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) { $_ }
            }
            catch { }
        }
    )
    if ($lockedByEousGate.Count -gt 0) {
        throw "Publish directory is locked by a running EousGate process: $resolvedOutput"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

if (-not $SkipTests) {
    & (Join-Path $PSScriptRoot 'Tests\run-tests.ps1') -Configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Release tests failed."
    }
}

# Publish assets are runtime-specific, so restore the win-x64 target explicitly.
Invoke-Checked 'dotnet' @('restore', $project, '--runtime', 'win-x64')

$publishDirArgument = '-p:PublishDir=' + $resolvedOutput + [System.IO.Path]::DirectorySeparatorChar
$publishArgs = @(
    'publish', $project,
    '--configuration', 'Release',
    '--framework', 'net8.0-windows',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '--no-restore',
    '-p:PublishSingleFile=false',
    '-p:IncludeNativeLibrariesForSelfExtract=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    $publishDirArgument
)
Invoke-Checked 'dotnet' $publishArgs

$exe = Join-Path $resolvedOutput 'EousGate.exe'
$runtimeConfig = Join-Path $resolvedOutput 'EousGate.runtimeconfig.json'
$deps = Join-Path $resolvedOutput 'EousGate.deps.json'
if (-not (Test-Path -LiteralPath $exe)) { throw "Published output is missing EousGate.exe." }
if (-not (Test-Path -LiteralPath $runtimeConfig)) { throw "Published output is missing runtimeconfig." }
if (-not (Test-Path -LiteralPath $deps)) { throw "Published output is missing deps file." }

$runtimeText = Get-Content -LiteralPath $runtimeConfig -Raw
if ($runtimeText -match '"frameworks"\s*:') {
    throw "Published output is framework-dependent, not self-contained."
}

$fileCount = (Get-ChildItem -LiteralPath $resolvedOutput -Recurse -File).Count
$sizeMb = [Math]::Round(((Get-ChildItem -LiteralPath $resolvedOutput -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB), 1)
$archivePath = Join-Path (Split-Path -Parent $resolvedOutput) ("EousGate-v$releaseVersion-win-x64.zip")
$hashPath = $archivePath + '.sha256'
if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
if (Test-Path -LiteralPath $hashPath) {
    Remove-Item -LiteralPath $hashPath -Force
}
Compress-Archive -Path (Join-Path $resolvedOutput '*') -DestinationPath $archivePath -CompressionLevel Optimal
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
$archiveHash | ForEach-Object { "$_  $(Split-Path -Leaf $archivePath)" } | Set-Content -LiteralPath $hashPath -Encoding ASCII
Write-Output "Release package generated: $resolvedOutput"
Write-Output "Files: $fileCount; Size: $sizeMb MB"
Write-Output "Archive: $archivePath"
Write-Output "Checksum: $hashPath"
Write-Output "SHA-256: $archiveHash"
