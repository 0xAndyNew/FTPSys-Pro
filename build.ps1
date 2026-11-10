<#
.SYNOPSIS
    Script biên dịch và đóng gói bộ cài đặt FtpSysPro.
.DESCRIPTION
    1. Chạy dotnet restore và build Release
    2. Chạy bộ unit tests
    3. Đóng gói bộ cài đặt bằng Inno Setup (nếu có ISCC.exe)
#>

[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  FtpSysPro - Build & Packaging Script    " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Restore & Build
Write-Host "`n[1/3] Restoring packages and building solution (Release)..." -ForegroundColor Yellow
dotnet restore FtpSysPro.sln
dotnet build FtpSysPro.sln -c Release --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}
Write-Host "Build succeeded!" -ForegroundColor Green

# 2. Run Tests
if (-not $SkipTests) {
    Write-Host "`n[2/3] Running unit tests..." -ForegroundColor Yellow
    dotnet test tests\FtpSysPro.Core.Tests\FtpSysPro.Core.Tests.csproj -c Release --no-build --verbosity normal

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed!"
        exit $LASTEXITCODE
    }
    Write-Host "All tests passed!" -ForegroundColor Green
} else {
    Write-Host "`n[2/3] Skipping tests." -ForegroundColor DarkGray
}

# 3. Package with Inno Setup
if (-not $SkipInstaller) {
    Write-Host "`n[3/3] Packaging installer..." -ForegroundColor Yellow

    # Search for ISCC.exe
    $isccPaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
    )

    $iscc = $null
    foreach ($p in $isccPaths) {
        if (Test-Path $p) {
            $iscc = $p
            break
        }
    }

    if ($null -eq $iscc) {
        $iscc = (Get-Command "ISCC.exe" -ErrorAction SilentlyContinue)?.Source
    }

    if ($null -ne $iscc) {
        Write-Host "Found Inno Setup compiler: $iscc" -ForegroundColor Gray
        $issFile = Join-Path $ScriptDir "installer\FtpSysPro.iss"
        & $iscc $issFile

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Installer package created successfully in .\dist\" -ForegroundColor Green
            Get-ChildItem -Path (Join-Path $ScriptDir "dist") -Filter "*.exe" | ForEach-Object {
                Write-Host " -> $($_.FullName) ($([math]::Round($_.Length / 1MB, 2)) MB)" -ForegroundColor Cyan
            }
        } else {
            Write-Warning "Inno Setup compiler returned exit code $LASTEXITCODE"
        }
    } else {
        Write-Warning "Inno Setup 6 (ISCC.exe) was not found on this machine."
        Write-Host "To create the setup executable, please install Inno Setup 6:" -ForegroundColor Yellow
        Write-Host "  winget install JRSoftware.InnoSetup" -ForegroundColor White
        Write-Host "Then re-run this script." -ForegroundColor Yellow
        Write-Host "Binaries are ready at: src\FtpSysPro.Manager\bin\Release\net48\" -ForegroundColor Gray
    }
}

Write-Host "`nDone!" -ForegroundColor Green
