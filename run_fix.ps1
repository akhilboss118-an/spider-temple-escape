$unityPath = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$projectPath = "C:\Users\akhil\OneDrive\Desktop\Spider-temple-Escape"
$logPath = "C:\Users\akhil\OneDrive\Desktop\Spider-temple-Escape\Logs\fix_and_snapshot.log"

$cmdArgs = @(
    "-batchmode",
    "-quit",
    "-projectPath", $projectPath,
    "-executeMethod", "Runner.EditorTools.CharacterFixAndSnapshotTool.RunFixAndSnapshots",
    "-logFile", $logPath
)

Write-Host "Starting Unity Fix & Snapshot..."
$process = Start-Process -FilePath $unityPath -ArgumentList $cmdArgs -NoNewWindow -PassThru -Wait
Write-Host "Unity process exited with code: $($process.ExitCode)"
Exit $process.ExitCode
