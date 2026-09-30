# Stop hook: if Core/Tools code changed since the last green run, run the engine tests.
# Red tests block Claude from finishing (exit 2) and the failure summary is fed back.

$ErrorActionPreference = 'Continue'
try { $data = [Console]::In.ReadToEnd() | ConvertFrom-Json } catch { $data = $null }

# Avoid infinite loops: if we already blocked once in this stop cycle, let it stop.
if ($data -and $data.stop_hook_active) { exit 0 }

$proj = $env:CLAUDE_PROJECT_DIR
if (-not $proj) { $proj = (Get-Location).Path }
$flag = Join-Path $proj '.claude/.tests-dirty'
$testProj = Join-Path $proj 'Tools/Engine.Tests'

if (-not (Test-Path $flag)) { exit 0 }
if (-not (Test-Path $testProj)) { Remove-Item $flag -Force -ErrorAction SilentlyContinue; exit 0 }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('test-gate: dotnet SDK not found on PATH; skipping tests.')
    exit 0
}

Push-Location $proj
$out = & dotnet test $testProj --nologo -v q 2>&1 | Out-String
$code = $LASTEXITCODE
Pop-Location

if ($code -eq 0) {
    Remove-Item $flag -Force -ErrorAction SilentlyContinue
    exit 0
}

$lines = $out -split "`r?`n" | Where-Object { $_ -match 'error|Failed|FAIL|Assert|Expected|But was|Exception' } | Select-Object -First 40
if (-not $lines) { $lines = ($out -split "`r?`n") | Select-Object -Last 40 }
[Console]::Error.WriteLine("Engine tests are RED. Fix them before finishing (do not weaken tests):")
[Console]::Error.WriteLine(($lines -join "`n"))
exit 2
