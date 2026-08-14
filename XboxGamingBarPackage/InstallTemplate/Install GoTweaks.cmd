@echo off
REM GoTweaks S installer launcher (plain script - avoids ps2exe AV false positives).
REM Double-click this file. PowerShell opens, UAC prompts for Admin, then installs.
setlocal EnableExtensions
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install GoTweaks.ps1" %*
exit /b %ERRORLEVEL%
