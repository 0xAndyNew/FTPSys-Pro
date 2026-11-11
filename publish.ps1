<#
.SYNOPSIS
    Script tự động biên dịch, khởi tạo Git, commit và đẩy mã nguồn lên GitHub:
    https://github.com/0xAndyNew/FTPSys-Pro.git
#>

[CmdletBinding()]
param(
    [string]$RemoteUrl = "https://github.com/0xAndyNew/FTPSys-Pro.git",
    [string]$AuthorName = "0xAndyNew",
    [string]$AuthorEmail = "179321164+0xAndyNew@users.noreply.github.com",
    [string]$Version = "v1.0.8"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  FTPSys Pro: Build, Package & Push to GitHub             " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Build & Test
Write-Host "`n[1/4] Bien dich solution va chay test..." -ForegroundColor Yellow
dotnet restore FtpSysPro.sln
dotnet build FtpSysPro.sln -c Release --no-restore
dotnet test tests\FtpSysPro.Core.Tests\FtpSysPro.Core.Tests.csproj -c Release --no-build --verbosity normal
Write-Host "Build & Test thanh cong!" -ForegroundColor Green

# 2. Build Inno Setup Installer if ISCC is installed
Write-Host "`n[2/4] Kiem tra dong goi bo cai dat Inno Setup..." -ForegroundColor Yellow
$iscc = $null
$cmd = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if ($cmd) {
    $iscc = $cmd.Source
}
if (-not $iscc) {
    $paths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe",
        "$env:USERPROFILE\AppData\Local\Programs\Inno Setup 6\ISCC.exe",
        "C:\Users\XXXX\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $paths) {
        if (Test-Path $p) { $iscc = $p; break }
    }
}

if ($iscc) {
    Write-Host "Tim thay Inno Setup: $iscc" -ForegroundColor Gray

    # Don dep cac file cai dat cu de giam dung luong repo
    if (Test-Path "dist") {
        Get-ChildItem -Path "dist" -Filter "*.exe" | Where-Object { $_.Name -ne "FTPSys_Pro_Setup_$Version.exe" } | Remove-Item -Force -ErrorAction SilentlyContinue
    }

    & $iscc "installer\FtpSysPro.iss"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Da tao bo cai dat thanh cong trong dist\:" -ForegroundColor Green
        Get-ChildItem -Path (Join-Path $ScriptDir "dist") -Filter "*.exe" | ForEach-Object {
            Write-Host " -> $($_.FullName) ($([math]::Round($_.Length / 1MB, 2)) MB)" -ForegroundColor Cyan
        }
    } else {
        throw "Loi khi bien dich bo cai dat bang Inno Setup!"
    }
} else {
    Write-Host "Chua tim thay Inno Setup tren may. Vui long cai dat Inno Setup 6!" -ForegroundColor Red
}

# 3. Configure Git & Commit
Write-Host "`n[3/4] Khoi tao va cau hinh Git..." -ForegroundColor Yellow
if (-not (Test-Path ".git")) {
    git init -b main
}

git config user.name "$AuthorName"
git config user.email "$AuthorEmail"

# Set remote origin
$existingRemote = git remote get-url origin 2>$null
if (-not $existingRemote) {
    git remote add origin $RemoteUrl
} else {
    git remote set-url origin $RemoteUrl
}

git add .
$status = git status --porcelain
if ($status) {
    git commit -m "build: release $Version with installer binary

- Built and packaged Inno Setup installer $Version into dist/
- Updated source binaries, configs, and tests

Co-Authored-By: Claude Code <noreply@anthropic.com>"
    Write-Host "Da commit thay doi thanh cong!" -ForegroundColor Green
} else {
    Write-Host "Khong co thay doi moi can commit." -ForegroundColor Gray
}

# Tag version
$existingTag = git tag -l $Version
if (-not $existingTag) {
    git tag -a $Version -m "Release FTPSys Pro $Version"
    Write-Host "Da tao Git tag $Version" -ForegroundColor Green
}

# 4. Push to GitHub
Write-Host "`n[4/4] Day ma nguon va tag len GitHub ($RemoteUrl)..." -ForegroundColor Yellow
git push -u origin main
git push origin --tags

Write-Host "`nHoan tat! Ma nguon da duoc day len $RemoteUrl" -ForegroundColor Green
Write-Host "GitHub Action se tu dong build va tao Release dinh kem bo cai dat tai:" -ForegroundColor Cyan
Write-Host "  https://github.com/0xAndyNew/FTPSys-Pro/releases" -ForegroundColor Cyan
