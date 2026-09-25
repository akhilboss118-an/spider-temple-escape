$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$proj = "C:\Users\akhil\OneDrive\Desktop\Spider-temple-Escape"
$log = "C:\Users\akhil\OneDrive\Desktop\Spider-temple-Escape\Logs\unity_verify_run.log"

$argList = @(
    "-batchmode",
    "-quit",
    "-projectPath", $proj,
    "-executeMethod", "Runner.EditorTools.MenuVerificationTest.VerifyCompilationAndSetup",
    "-logFile", $log
)

Write-Host "Running Unity verification..."
$proc = Start-Process -FilePath $unity -ArgumentList $argList -NoNewWindow -PassThru -Wait
Write-Host "Exit code: $($proc.ExitCode)"
Exit $proc.ExitCode
