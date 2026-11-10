@echo off
chcp 65001 >nul
title FTPSys-Pro - Build, Commit & Push to GitHub
echo ==========================================================
echo   FTPSys-Pro: Build, Package ^& Push to GitHub
echo ==========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Qua trinh thuc hien gap loi (Exit code: %ERRORLEVEL%).
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [SUCCESS] Qua trinh day len GitHub hoan tat thanh cong!
pause
