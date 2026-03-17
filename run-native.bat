@echo off
setlocal
set REPO_ROOT=%~dp0
set PROJECT=%REPO_ROOT%native\TokenLuv.WinUI\TokenLuv.WinUI.csproj
set CONFIG=Debug

if /I "%1"=="--release" set CONFIG=Release

taskkill /IM TokenLuv.WinUI.exe /F >nul 2>nul
dotnet build "%PROJECT%" -c %CONFIG% -p:Platform=x64
if errorlevel 1 exit /b %errorlevel%

start "" "%REPO_ROOT%native\TokenLuv.WinUI\bin\x64\%CONFIG%\net8.0-windows10.0.19041.0\win-x64\TokenLuv.WinUI.exe"
endlocal
