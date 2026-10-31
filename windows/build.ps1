# Builds build\win\ (app, browser helper, extension), the portable zip and the installer .exe.
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
$version = (Get-Content VERSION).Trim()
$out = "build\win"
if (Test-Path build) { Remove-Item build -Recurse -Force }
$common = @("-c", "Release", "-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=true",
            "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:Version=$version", "-o", $out)
dotnet publish windows\App\App.csproj @common
if ($LASTEXITCODE -ne 0) { throw "App build failed" }
dotnet publish windows\Host\Host.csproj @common
if ($LASTEXITCODE -ne 0) { throw "Helper build failed" }
Get-ChildItem $out -Filter *.pdb | Remove-Item
python scripts\build-extensions.py
if ($LASTEXITCODE -ne 0) { throw "Extension build failed" }
Copy-Item build\extension-chrome "$out\Extension" -Recurse
Copy-Item "build\NexonicFirefoxAddon-$version.xpi" "$out\Firefox Add-on.xpi"
# The ndm command runs the helper in command-line mode; the installer puts this folder on the user's PATH.
Set-Content -Path "$out\ndm.cmd" -Value "@echo off`r`n`"%~dp0nexonic-host.exe`" %*" -Encoding ascii -NoNewline
Compress-Archive -Path "$out\*" -DestinationPath "build\NexonicDownloadManager-$version-windows-portable.zip"
$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { choco install innosetup -y --no-progress | Out-Null }
$env:NDM_VERSION = $version
& $iscc windows\installer.iss
if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }
Write-Host "Built build\NexonicDownloadManager-Setup-$version.exe and the portable zip"
