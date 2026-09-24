param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path ([System.IO.Path]::GetTempPath()) "EousGate-test-$Configuration"
$appOutput = Join-Path $artifactRoot 'app'
$testOutput = Join-Path $artifactRoot 'tests'

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

dotnet restore (Join-Path $repoRoot 'Tests\EousGate.Tests.csproj')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build (Join-Path $repoRoot 'EousGate.csproj') --no-restore --configuration $Configuration -p:UseAppHost=false -p:OutputPath="$appOutput\"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build (Join-Path $repoRoot 'Tests\EousGate.Tests.csproj') --no-restore --configuration $Configuration -p:UseAppHost=false -p:OutputPath="$testOutput\"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet (Join-Path $testOutput 'EousGate.Tests.dll')
exit $LASTEXITCODE
