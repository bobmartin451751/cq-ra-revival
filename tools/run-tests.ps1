param(
    [ValidateSet("EditMode", "PlayMode", "All")]
    [string]$Platform = "All"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$unity = if ($env:UNITY_EDITOR) { $env:UNITY_EDITOR } else { "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" }

if (-not (Test-Path $unity)) { throw "Unity editor not found at $unity" }

$tmp = Join-Path $env:LOCALAPPDATA "Temp\opencode"
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

$platforms = if ($Platform -eq "All") { @("EditMode", "PlayMode") } else { @($Platform) }
$failed = $false

foreach ($plat in $platforms) {
    $res = Join-Path $tmp "results-$plat.xml"
    $log = Join-Path $tmp "tests-$plat.log"
    if (Test-Path $res) { Remove-Item $res -Force }

    Write-Host "=== $plat ==="
    $p = Start-Process -FilePath $unity -PassThru -Wait -NoNewWindow -ArgumentList @(
        "-batchmode",
        "-projectPath", $root,
        "-runTests",
        "-testPlatform", $plat,
        "-testResults", $res,
        "-logFile", $log
    )

    if (Test-Path $res) {
        [xml]$xml = Get-Content $res
        $run = $xml.'test-run'
        Write-Host "  total=$($run.total) passed=$($run.passed) failed=$($run.failed)"
        if ([int]$run.failed -gt 0 -or [int]$run.total -eq 0) { $failed = $true }
    }
    else {
        Write-Host "  no results; see $log"
        $failed = $true
    }
}

if ($failed) { Write-Error "Tests failed" }
Write-Host "All tests passed"
