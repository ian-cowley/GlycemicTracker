param (
    [Parameter(Mandatory=$false)]
    [ValidateSet("install", "uninstall", "restart")]
    [string]$Action = "install"
)

# Enforce Administrator privileges
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be run as an Administrator. Please run PowerShell as Administrator and try again."
    exit 1
}

$ServiceName = "GlycemicTracker"
$DisplayName = "Glycemic Glucose Tracker Service"
$PublishDir = Join-Path $PSScriptRoot "publish"
$ExePath = Join-Path $PublishDir "GlycemicTracker.exe"
$ProjectFile = Join-Path $PSScriptRoot "src\GlycemicTracker\GlycemicTracker.csproj"
$ConnectionString = "Server=192.168.1.108;Database=CarbTracker;User Id=sa;Password=Passw0rd1$;TrustServerCertificate=True;MultipleActiveResultSets=true;"

if ($Action -eq "uninstall") {
    Write-Host "Stopping and removing the Windows Service..."
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -eq 'Running') {
            Stop-Service -Name $ServiceName -Force
        }
        Remove-Service -Name $ServiceName
        Write-Host "Service '$ServiceName' removed successfully."
    } else {
        Write-Host "Service '$ServiceName' does not exist."
    }

    Write-Host "Removing system-wide connection string environment variable..."
    [Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $null, "Machine")
    Write-Host "System environment variable removed."
    exit 0
}

if ($Action -eq "restart") {
    Write-Host "Restarting service..."
    Restart-Service -Name $ServiceName
    Write-Host "Service restarted."
    exit 0
}

# Default Action: install
Write-Host "Publishing the application..."
dotnet publish $ProjectFile -c Release -o $PublishDir
if (-not $?) {
    Write-Error "Failed to publish the application. Aborting service installation."
    exit 1
}

Write-Host "Configuring system-wide environment variable for connection string..."
[Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $ConnectionString, "Machine")
Write-Host "Environment variable set successfully."

Write-Host "Checking for existing service..."
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Service already exists. Uninstalling..."
    if ($existing.Status -eq 'Running') {
        Stop-Service -Name $ServiceName -Force
    }
    Remove-Service -Name $ServiceName
    Start-Sleep -Seconds 2
}

Write-Host "Registering service '$ServiceName'..."
New-Service -Name $ServiceName `
            -BinaryPathName $ExePath `
            -DisplayName $DisplayName `
            -Description "Runs GlycemicTracker as a background Windows Service." `
            -StartupType Automatic

Write-Host "Starting service '$ServiceName'..."
Start-Service -Name $ServiceName

Write-Host "`nInstallation Completed Successfully!"
Write-Host "The service is configured to start automatically and run in the background."
Write-Host "You can access the web application at http://localhost:5014"
