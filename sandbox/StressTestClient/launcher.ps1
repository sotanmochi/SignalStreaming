# Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

param (
    [string]$exePath,
    [int]$processCount,
    [string]$server,
    [int]$port
)

Write-Output "----------------------------------------"
Write-Output "StressTestClient Launcher"
Write-Output "----------------------------------------"

$arguments = @()

if ($server) {
    $arguments += "--server $server"
}

if ($port) {
    $arguments += "--port $port"
}

Write-Output "Exe: $exePath"
Write-Output "Parameters: $($arguments -join ' ')"
Write-Output ""
Write-Output "Toral process count: $processCount"
Write-Output ""

for ($i = 1; $i -le $processCount; $i++) {
    if ($arguments.Count -gt 0) {
        Start-Process -FilePath $exePath -ArgumentList $arguments
    } else {
        Start-Process -FilePath $exePath
    }

    Write-Output "Start process[$i]"
    Start-Sleep -Seconds 1
}

Write-Output ""
Write-Output "Toral process count: $processCount"
Write-Output "----------------------------------------"