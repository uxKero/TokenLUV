@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-native.ps1" %*
endlocal
