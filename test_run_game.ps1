$exe = "C:\Users\akhil\OneDrive\Desktop\Spider-temple-Escape\Builds\Windows\SpiderTempleEscape.exe"
Write-Host "Launching $exe..."
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 6
$status = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
if ($status) {
    Write-Host "Game is RUNNING perfectly!"
    Write-Host "Process ID: $($status.Id)"
    Write-Host "Responding: $($status.Responding)"
    Write-Host "Memory: $([math]::Round($status.WorkingSet64 / 1MB, 2)) MB"
    Stop-Process -Id $proc.Id -Force
    Write-Host "Game closed cleanly after test run."
} else {
    Write-Host "Process not running."
}
