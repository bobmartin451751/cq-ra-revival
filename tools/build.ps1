param(
    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$unity = if ($env:UNITY_EDITOR) { $env:UNITY_EDITOR } else { "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" }

if (-not (Test-Path $unity)) { throw "Unity editor not found at $unity" }

$log = Join-Path $env:LOCALAPPDATA "Temp\opencode\build.log"

Write-Host "Building Windows player..."
$p = Start-Process -FilePath $unity -PassThru -Wait -NoNewWindow -ArgumentList @(
    "-batchmode",
    "-projectPath", $root,
    "-executeMethod", "CQRa.EditorTools.BuildScript.BuildWindows",
    "-logFile", $log
)

$exe = Join-Path $root "Builds\Windows\CQRaRevival.exe"
if (-not (Test-Path $exe)) {
    Write-Error "Build failed. See $log"
}

Write-Host "Built: $exe"

if (-not $NoLaunch) {
    Write-Host "Launching..."
    Start-Process $exe
}

exit $p.ExitCode
