@echo off
REM GoTweaks S installer - use this file (not the .ps1 directly).
setlocal EnableExtensions
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -NoLogo -File "%~dp0Elevate-And-Run.ps1" %*
exit /b %ERRORLEVEL%
