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
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet (Join-Path $testOutput 'EousGate.Tests.dll') --file-hop-smoke (Join-Path $artifactRoot 'file-hop-visual')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet (Join-Path $testOutput 'EousGate.Tests.dll') --clipboard-smoke (Join-Path $artifactRoot 'clipboard-visual')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet (Join-Path $testOutput 'EousGate.Tests.dll') --window-switching-smoke
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet (Join-Path $testOutput 'EousGate.Tests.dll') --file-drop-permission-smoke
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet (Join-Path $testOutput 'EousGate.Tests.dll') --folder-dialog-smoke
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet (Join-Path $testOutput 'EousGate.Tests.dll') --assistant-smoke (Join-Path $artifactRoot 'assistant-visual')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet (Join-Path $testOutput 'EousGate.Tests.dll') --workbench-smoke (Join-Path $artifactRoot 'workbench-visual')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$fixtureOutput = Join-Path $artifactRoot 'external-fixture'
dotnet (Join-Path $testOutput 'EousGate.Tests.dll') --custom-program-smoke
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet (Join-Path $testOutput 'EousGate.Tests.dll') --external-monitor-lifecycle-smoke
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build (Join-Path $repoRoot 'Tests\Fixtures\ExternalProcessFixture.csproj') --configuration $Configuration -p:OutputPath="$fixtureOutput\"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet (Join-Path $testOutput 'EousGate.Tests.dll') --external-process-smoke $fixtureOutput
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet (Join-Path $testOutput 'EousGate.Tests.dll') --launcher-process-smoke $fixtureOutput
exit $LASTEXITCODE
