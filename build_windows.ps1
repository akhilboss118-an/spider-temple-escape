$unityPath = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$projectPath = "`"C:\Users\akhil\OneDrive\Desktop\New folder`""
$logPath = "`"C:\Users\akhil\OneDrive\Desktop\New folder\Logs\build_windows.log`""

$cmdArgs = "-batchmode -quit -projectPath $projectPath -executeMethod Runner.EditorTools.WindowsBuildHelper.BuildWindowsStandaloneFromCommandLine -logFile $logPath"

Write-Host "Starting Unity Windows Standalone Build with args: $cmdArgs"
$process = Start-Process -FilePath $unityPath -ArgumentList $cmdArgs -NoNewWindow -PassThru -Wait
Write-Host "Unity process exited with code: $($process.ExitCode)"
Exit $process.ExitCode
