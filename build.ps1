<#
.SYNOPSIS
    Сборка ScreenApp в один самодостаточный .exe (компилятор → exe).

.DESCRIPTION
    Прогоняет тесты и публикует single-file self-contained приложение под Windows x64.
    На целевом ПК .NET ставить не нужно — всё внутри .exe.

.PARAMETER SkipTests
    Пропустить запуск тестов.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build.ps1
#>
param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

Write-Host "== ScreenApp build ==" -ForegroundColor Cyan

# 1. Иконка (на случай отсутствия Resources/app.ico).
$icon = Join-Path $root 'ScreenApp\Resources\app.ico'
if (-not (Test-Path $icon)) {
    Write-Host "Генерирую иконку..." -ForegroundColor Yellow
    powershell -ExecutionPolicy Bypass -File (Join-Path $root 'tools\make-icon.ps1')
}

# 2. Тесты.
if (-not $SkipTests) {
    Write-Host "Запуск тестов..." -ForegroundColor Yellow
    dotnet test (Join-Path $root 'ScreenApp.sln') -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "Тесты не прошли." }
}

# 3. Публикация одного .exe.
Write-Host "Публикация single-file .exe..." -ForegroundColor Yellow
dotnet publish (Join-Path $root 'ScreenApp\ScreenApp.csproj') /p:PublishProfile=win-x64
if ($LASTEXITCODE -ne 0) { throw "Публикация не удалась." }

$exe = Join-Path $root 'ScreenApp\bin\Release\net8.0-windows\win-x64\publish\ScreenApp.exe'
if (-not (Test-Path $exe)) { throw "Не найден $exe" }

# 4. Копируем готовый .exe в dist/.
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item $exe $dist -Force

Write-Host ""
Write-Host "Готово: $(Join-Path $dist 'ScreenApp.exe')" -ForegroundColor Green
