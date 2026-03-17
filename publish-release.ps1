param(
    [string]$Version = "dev",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $repoRoot "native\TokenLuv.WinUI\TokenLuv.WinUI.csproj"
$distRoot = Join-Path $repoRoot "dist"
$publishDir = Join-Path $distRoot "TokenLUV-$Runtime"
$buildOutput = Join-Path $repoRoot "native\TokenLuv.WinUI\bin\x64\Release\net8.0-windows10.0.19041.0\$Runtime"
$zipPath = Join-Path $distRoot "TokenLUV-$Version-$Runtime.zip"
$installNotes = Join-Path $publishDir "INSTALL.txt"
$releaseNotes = Join-Path $publishDir "README.txt"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$runningProcesses = Get-Process -Name "TokenLuv.WinUI" -ErrorAction SilentlyContinue
if ($runningProcesses) {
    Write-Host "Stopping running TokenLUV instances..." -ForegroundColor Yellow
    $runningProcesses | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

Write-Host "Building TokenLUV $Version for $Runtime..." -ForegroundColor Cyan
& dotnet build $projectPath -c Release -p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $buildOutput)) {
    throw "Verified Release output not found at $buildOutput"
}

Write-Host "Copying verified Release output..." -ForegroundColor Cyan
Copy-Item -Path (Join-Path $buildOutput "*") -Destination $publishDir -Recurse -Force

$exePath = Join-Path $publishDir "TokenLuv.WinUI.exe"
if (-not (Test-Path $exePath)) {
    throw "Release executable not found after copy."
}

@"
TokenLUV $Version

1. Extract this folder anywhere on your Windows machine.
2. Run TokenLuv.WinUI.exe.
3. If Windows warns about an unknown publisher, choose More info and Run anyway for unsigned builds.
4. Configure providers from Settings.

Notes:
- This build is distributed outside the Microsoft Store.
- This release lane packages the verified Release build output.
- End users need the .NET 8 Desktop Runtime and Windows App Runtime installed.
- OAuth-based providers still complete sign-in in the browser or their CLI.
"@ | Set-Content -Path $installNotes -Encoding UTF8

@"
TokenLUV release package

Main executable:
TokenLuv.WinUI.exe

This package was built from the verified WinUI Release output.
"@ | Set-Content -Path $releaseNotes -Encoding UTF8

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Publish folder: $publishDir" -ForegroundColor Green
Write-Host "Zip package:   $zipPath" -ForegroundColor Green
