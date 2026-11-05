param(
    [string]$Out = ".temp/screens/phase1.png"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$unity = if ($env:UNITY_EDITOR) { $env:UNITY_EDITOR } else { "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" }

if (-not (Test-Path $unity)) { throw "Unity editor not found at $unity" }

$png = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $root $Out }
$log = Join-Path $env:LOCALAPPDATA "Temp\opencode\screenshot.log"

if (Test-Path $png) { Remove-Item $png -Force }

Write-Host "Rendering $png"
$p = Start-Process -FilePath $unity -PassThru -Wait -NoNewWindow -ArgumentList @(
    "-batchmode",
    "-projectPath", $root,
    "-executeMethod", "CQRa.EditorTools.PlayModeCapture.Run",
    "-screenshot", $png,
    "-logFile", $log
)

if (-not (Test-Path $png)) {
    Write-Error "Screenshot was not produced. See $log"
}

Write-Host "OK: $png ($((Get-Item $png).Length) bytes)"
exit $p.ExitCode
