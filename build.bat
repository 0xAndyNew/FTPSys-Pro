@echo off
setlocal enabledelayedexpansion
title FTPSys Pro: Build, Package Installer and Push to Git

echo ====================================================
echo   FTPSys Pro: Build, Package Installer and Push
echo ====================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" %*
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build hoac publish that bai (Ma loi: %ERRORLEVEL%).
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [THANH CONG] Da dong goi bo cai dat va day len GitHub!
pause
