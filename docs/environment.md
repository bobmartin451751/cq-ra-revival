# Development environment

## Editor

| | |
|---|---|
| Version | Unity **6.3 LTS** `6000.3.28f1` |
| Path | `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe` |
| Modules | Windows Build Support (Mono + IL2CPP) |
| Override | set `$env:UNITY_EDITOR` to use a different install |

## Licence workaround (important)

The Unity Hub is installed as an **MSIX package** from `UnityHubSetup-x64.msix`. MSIX
virtualises `%LOCALAPPDATA%`, so the Hub stores its entitlement licence here:

```
%LOCALAPPDATA%\Packages\UnityTechnologies.UnityHub_2vrhnee42bhxm\LocalCache\Local\Unity\licenses\
```

A standalone `Unity.exe` launched from `C:\Program Files\...` reads the **non-virtualised**
path instead and therefore sees no licence:

```
%LOCALAPPDATA%\Unity\licenses\UnityEntitlementLicense.xml
```

Copying the file into the real path fixes headless activation. If Unity ever starts
failing with `No valid Unity Editor license found` or exit code `198`, re-run:

```powershell
$src = "$env:LOCALAPPDATA\Packages\UnityTechnologies.UnityHub_2vrhnee42bhxm\LocalCache\Local\Unity\licenses"
$dst = "$env:LOCALAPPDATA\Unity\licenses"
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item "$src\UnityEntitlementLicense.xml" "$dst\UnityEntitlementLicense.xml" -Force
Get-Process Unity.Licensing.Client -ErrorAction SilentlyContinue | Stop-Process -Force
```

Do this while the Hub is signed in and the Personal licence is active.

## Headless commands

All tooling lives in `../tools`:

```powershell
.\tools\screenshot.ps1            # -> .temp/screens/phase1.png
.\tools\run-tests.ps1             # EditMode + PlayMode
.\tools\build.ps1 -NoLaunch       # -> Builds/Windows/CQRaRevival.exe
```

## Known non-issues

- `ArgumentOutOfRangeException` from `UnityEditor.Search.SearchDatabase` at startup is a
  built-in editor bug in the search index. Harmless, ignore it.
- `CreateDirectory 'C:/Users/<user>/AppData/Local/Unity/Caches' failed` appears when the
  licence directory is missing; the copy above resolves it.
