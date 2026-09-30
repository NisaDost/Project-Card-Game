# PreToolUse hook (Edit|Write|MultiEdit)
# 1) Blocks writes to Unity-generated folders and .meta files.
# 2) Blocks forbidden APIs in the pure C# Core engine.
# 3) Marks Core/Tools as dirty so the Stop hook runs the tests.
# Exit 2 = block, stderr is shown to Claude.

$ErrorActionPreference = 'Stop'
try { $data = [Console]::In.ReadToEnd() | ConvertFrom-Json } catch { exit 0 }

$ti = $data.tool_input
if (-not $ti -or -not $ti.file_path) { exit 0 }
$path = ($ti.file_path -replace '\\', '/')

$proj = $env:CLAUDE_PROJECT_DIR
if (-not $proj) { $proj = (Get-Location).Path }
$projN = ($proj -replace '\\', '/').TrimEnd('/')
$rel = $path
if ($rel.StartsWith($projN, [StringComparison]::OrdinalIgnoreCase)) { $rel = $rel.Substring($projN.Length).TrimStart('/') }

# 1) Generated / engine-owned locations
if ($rel -match '^(Library|Temp|Logs|obj|Build|Builds|UserSettings)/' -or $rel -match '/(bin|obj)/') {
    [Console]::Error.WriteLine("Blocked: '$rel' is a generated folder. Do not edit it.")
    exit 2
}
if ($rel -match '\.meta$') {
    [Console]::Error.WriteLine("Blocked: '$rel' is a Unity .meta file. Unity manages GUIDs; never edit .meta files by hand.")
    exit 2
}

# 2) Core purity
$isCore = $rel -match '^Assets/_Project/Core/.*\.cs$'
if ($isCore) {
    $text = ''
    if ($ti.content)    { $text += [string]$ti.content }
    if ($ti.new_string) { $text += [string]$ti.new_string }
    if ($ti.edits)      { foreach ($e in $ti.edits) { $text += [string]$e.new_string } }

    $rules = @(
        @{ p = '\bUnityEngine\b|\bUnityEditor\b'; m = 'UnityEngine/UnityEditor is not allowed in Core (noEngineReferences).' },
        @{ p = 'System\.Random|\bnew\s+Random\s*\('; m = 'System.Random is not allowed in Core. Use the deterministic Rng stored in GameState.' },
        @{ p = '\bDateTime\.(Now|UtcNow|Today)\b|\bEnvironment\.TickCount\b|\bStopwatch\b'; m = 'Wall-clock time is not allowed in Core (determinism).' },
        @{ p = '\bGuid\.NewGuid\b'; m = 'Guid.NewGuid is not allowed in Core. Use sequential ids.' }
    )
    foreach ($r in $rules) {
        if ($text -match $r.p) {
            [Console]::Error.WriteLine("Blocked edit to ${rel}: " + $r.m + " See .claude/rules/core-engine.md")
            exit 2
        }
    }
}

# 3) Dirty flag for the Stop hook
if ($rel -match '^(Assets/_Project/Core/|Tools/).*\.(cs|csproj)$') {
    $flag = Join-Path $proj '.claude/.tests-dirty'
    try { New-Item -ItemType File -Path $flag -Force | Out-Null } catch { }
}

exit 0
