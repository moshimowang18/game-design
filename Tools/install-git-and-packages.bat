@echo off
setlocal
cd /d "%~dp0.."

where git >nul 2>&1
if errorlevel 1 (
    echo Installing Git for Windows...
    winget install --id Git.Git -e --source winget --accept-package-agreements --accept-source-agreements
    echo After Git install completes, close this window, restart Unity Hub, then run Tools\setup-upm-packages.ps1
    pause
    exit /b 0
)

powershell -ExecutionPolicy Bypass -File "%~dp0setup-upm-packages.ps1"
pause
