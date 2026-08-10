param(
    [string]$Root = "D:\SteamLibrary\steamapps\common\Nuclear Option"
)
$ErrorActionPreference = "Stop"

dotnet build NoModBar.csproj -c Debug
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$scriptsDir = Join-Path $Root "BepInEx\scripts"
$pluginsDir = Join-Path $Root "BepInEx\plugins"
$outDir = Join-Path $PSScriptRoot "bin\Debug"

Copy-Item -LiteralPath (Join-Path $outDir "NoModBar.dll") -Destination $scriptsDir -Force
Copy-Item -LiteralPath (Join-Path $outDir "NoModBar.pdb") -Destination $scriptsDir -Force
Copy-Item -LiteralPath (Join-Path $outDir "NoModBar.Core.dll") -Destination $pluginsDir -Force

Remove-Item -LiteralPath (Join-Path $scriptsDir "NoModBar.Core.dll") -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $pluginsDir "NoModBar.dll") -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Deployed NoModBar.dll -> scripts/ (ScriptEngine hot reload)"
Write-Host "Deployed NoModBar.Core.dll -> plugins/ (stable registry, never reloaded)"
Write-Host ""
Write-Host "Hot reload the bar: edit code, run this script, then press Insert in-game (ScriptEngine watcher also auto-reloads after ~3s)."
