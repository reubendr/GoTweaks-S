@echo off
REM GoTweaks S installer — launches PowerShell with Bypass, auto-elevates via UAC, pauses on error.
setlocal EnableExtensions
cd /d "%~dp0"

echo.
echo  GoTweaks S Installer
echo  If prompted, click Yes on the UAC (Administrator) dialog.
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -NoLogo -File "%~dp0Install GoTweaks.ps1" %*
set "ERR=%ERRORLEVEL%"

if not "%ERR%"=="0" (
    echo.
    echo  Install exited with error code %ERR%.
    echo  Make sure you extracted the full zip and run Install GoTweaks.cmd from that folder.
    echo.
    pause
)

exit /b %ERR%
