$ollamaPath = "ollama"
if (Get-Command "ollama" -ErrorAction SilentlyContinue) {
    Write-Host "Ollama PATH üzerinde bulundu." -ForegroundColor Green
} elseif (Test-Path "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe") {
    Write-Host "Ollama PATH'de yok ama klasörde bulundu." -ForegroundColor Green
    $ollamaPath = "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe"
} else {
    Write-Host "Ollama bulunamadı. Lütfen https://ollama.com/download/windows adresinden indirip kurun." -ForegroundColor Yellow
    Start-Process "https://ollama.com/download/windows"
    return
}

Write-Host "Llama 3 Modeli kontrol ediliyor..." -ForegroundColor Cyan
& $ollamaPath list | Select-String "llama3"

if ($?) {
    Write-Host "Model zaten hazır." -ForegroundColor Green
} else {
    Write-Host "Model indiriliyor (Bu biraz sürebilir)..." -ForegroundColor Yellow
    & $ollamaPath pull llama3
}

Write-Host "Kurulum Tamamlandı! Yapay Zeka Sunucunuz Hazır." -ForegroundColor Green
