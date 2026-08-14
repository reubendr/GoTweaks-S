@echo off
REM GoTweaks S - double-click this file to install.
setlocal EnableExtensions
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -NoLogo -File "%~dp0_install\InstallGoTweaks.ps1" %*
exit /b %ERRORLEVEL%
