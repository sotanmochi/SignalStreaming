# Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

param (
    [int]$processCount,
    [string]$exePath
)

for ($i = 1; $i -le $processCount; $i++) {
    Start-Process -FilePath $exePath
}

Write-Output "Start $exePath"
Write-Output "Process count: $processCount"