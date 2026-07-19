$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
$tunix = Join-Path $repo "Sirketler\Tunahan-Rustix\Tunix"
$ilos = Join-Path $repo "Sirketler\Ilayda-Python\IlosTech"

Write-Host "Tunix test ve sunucusu ayrı terminalde başlatılıyor..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-ExecutionPolicy", "Bypass",
    "-Command",
    "Set-Location '$tunix'; cargo test; if (`$LASTEXITCODE -eq 0) { cargo run }"
)

Write-Host "İlos Tech test ve sunucusu ayrı terminalde başlatılıyor..." -ForegroundColor Magenta
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-ExecutionPolicy", "Bypass",
    "-Command",
    "Set-Location '$ilos'; .\run.ps1"
)

Write-Host "Motor, 8080 borsa ve 8090 yönetim merkezi başlatılıyor..." -ForegroundColor Green
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-ExecutionPolicy", "Bypass",
    "-Command",
    "Start-Sleep -Seconds 4; Set-Location '$repo'; dotnet run --project .\SirketMotoru\SirketMotoru.csproj"
)

Write-Host ""
Write-Host "Açılacak adresler:" -ForegroundColor Yellow
Write-Host "  Yazılım Borsası : http://localhost:8080/"
Write-Host "  Şirket Yönetimi : http://localhost:8090/"
Write-Host ""
Write-Host "Mudaf ve Ugax farklı bilgisayarlarda ayrıca açık olmalıdır."
