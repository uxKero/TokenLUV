param(
    [switch]$Release
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $repoRoot 'native\TokenLuv.WinUI\TokenLuv.WinUI.csproj'
$configuration = if ($Release) { 'Release' } else { 'Debug' }
$targetFramework = 'net8.0-windows10.0.19041.0'
$processName = 'TokenLuv.WinUI'

$runningProcesses = Get-Process -Name $processName -ErrorAction SilentlyContinue
if ($runningProcesses) {
    Write-Host "Stopping running TokenLUV instances..." -ForegroundColor Yellow
    $runningProcesses | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

Write-Host "Building TokenLUV WinUI ($configuration)..." -ForegroundColor Cyan
& dotnet build $projectPath -c $configuration -p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

$exePath = Join-Path $repoRoot "native\TokenLuv.WinUI\bin\x64\$configuration\$targetFramework\win-x64\TokenLuv.WinUI.exe"
if (-not (Test-Path $exePath)) {
    throw "Could not find the app executable at $exePath"
}

Write-Host "Launching $exePath" -ForegroundColor Green
Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Parent $exePath)
