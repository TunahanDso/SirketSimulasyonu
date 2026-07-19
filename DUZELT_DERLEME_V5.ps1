param(
    [switch]$Commit,
    [switch]$Push
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path

function Replace-Exactly {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$Old,
        [Parameter(Mandatory)] [string]$New,
        [Parameter(Mandatory)] [int]$ExpectedOldCount
    )

    $fullPath = Join-Path $repo $Path
    if (-not (Test-Path $fullPath)) {
        throw "Dosya bulunamadı: $fullPath"
    }

    $text = [System.IO.File]::ReadAllText($fullPath)
    $oldCount = ([regex]::Matches($text, [regex]::Escape($Old))).Count
    $newCount = ([regex]::Matches($text, [regex]::Escape($New))).Count

    if ($oldCount -eq 0 -and $newCount -ge $ExpectedOldCount) {
        Write-Host "Zaten düzeltilmiş: $Path" -ForegroundColor DarkGray
        return
    }

    if ($oldCount -ne $ExpectedOldCount) {
        throw "Beklenen ifade sayısı uyuşmuyor: $Path | Beklenen: $ExpectedOldCount | Bulunan: $oldCount"
    }

    $patched = $text.Replace($Old, $New)
    [System.IO.File]::WriteAllText(
        $fullPath,
        $patched,
        [System.Text.UTF8Encoding]::new($false)
    )
    Write-Host "Düzeltildi: $Path" -ForegroundColor Green
}

Replace-Exactly `
    -Path "SirketMotoru\Isletim\EkosistemYoneticisi.cs" `
    -Old ".Select(Str)" `
    -New ".Select(x => Str(x))" `
    -ExpectedOldCount 2

Replace-Exactly `
    -Path "SirketMotoru\Isletim\SirketIsletimYoneticisiV2.cs" `
    -Old "tutar += durum.TeknikBorc * 14m + durum.BakimBaskisi * 8m;" `
    -New "tutar += (decimal)durum.TeknikBorc * 14m + (decimal)durum.BakimBaskisi * 8m;" `
    -ExpectedOldCount 1

Write-Host "Kaynak düzeltmeleri tamamlandı. Derleme başlıyor..." -ForegroundColor Cyan
& dotnet clean (Join-Path $repo "SirketMotoru\SirketMotoru.csproj")
if ($LASTEXITCODE -ne 0) { throw "dotnet clean başarısız oldu." }

& dotnet build (Join-Path $repo "SirketMotoru\SirketMotoru.csproj")
if ($LASTEXITCODE -ne 0) { throw "dotnet build hâlâ hata veriyor." }

if ($Commit) {
    & git -C $repo add -- \
        "SirketMotoru/Isletim/EkosistemYoneticisi.cs" \
        "SirketMotoru/Isletim/SirketIsletimYoneticisiV2.cs"
    if ($LASTEXITCODE -ne 0) { throw "git add başarısız oldu." }

    & git -C $repo commit -m "fix: resolve v5 ecosystem and operating compile errors"
    if ($LASTEXITCODE -ne 0) { throw "git commit başarısız oldu veya commitlenecek değişiklik yok." }

    if ($Push) {
        & git -C $repo push origin HEAD:agent/tunix-matematik-topla
        if ($LASTEXITCODE -ne 0) { throw "git push başarısız oldu." }
    }
}

Write-Host "V5 derleme düzeltmesi başarıyla tamamlandı." -ForegroundColor Green
