#requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$LocalPath,

    [Parameter(Mandatory = $true, Position = 1)]
    [string]$MtpPath
)

$ErrorActionPreference = 'Stop'
$DiffDir = "diff"

$shell = New-Object -ComObject Shell.Application

function Write-ProgressBar {
    param(
        [int]$Current,
        [int]$Total,
        [string]$Message = ""
    )

    if ($Total -eq 0) { return }

    $width = 50
    $percentage = [math]::Min(1, [math]::Max(0, $Current / $Total))
    $filled = [int]($percentage * $width)
    $empty = $width - $filled

    $bar = "#" * $filled + "-" * $empty
    $percentText = "{0:P0}" -f $percentage
    $truncatedMessage = if ($Message.Length -gt 40) { $Message.Substring(0, 40) } else { $Message }
    $paddedMessage = $truncatedMessage.PadRight(40)

    Write-Host -NoNewline "`r[$bar] $percentText $paddedMessage"
}

function Get-FirstMtpDevice {
    $pnpDevices = Get-PnpDevice -Class WPD -PresentOnly -ErrorAction SilentlyContinue |
        Where-Object { $_.Status -eq 'OK' -and $_.FriendlyName -notmatch "Bluetooth" }

    if ($pnpDevices) {
        $firstDevice = $pnpDevices | Select-Object -First 1
        return Get-DeviceShellObject -DeviceName $firstDevice.FriendlyName
    }

    return $null
}

function Get-DeviceShellObject {
    param([string]$DeviceName)

    $computer = $shell.Namespace(0x11)
    
    foreach ($item in $computer.Items()) {
        if ($item.Name -eq $DeviceName) {
            return $item
        }
    }

    return $null
}

function Get-MtpFolder {
    param(
        [object]$Device,
        [string]$Path
    )

    $currentFolder = $Device.GetFolder
    $pathParts = $Path.Split('\', [System.StringSplitOptions]::RemoveEmptyEntries)

    foreach ($part in $pathParts) {
        $found = $false
        $items = $currentFolder.Items()
        
        if (-not $items) {
            return $null
        }

        foreach ($item in $items) {
            if ($item.Name -eq $part) {
                if (-not $item.IsFolder) {
                    return $null
                }
                $currentFolder = $item.GetFolder
                $found = $true
                break
            }
        }

        if (-not $found) {
            return $null
        }
    }

    return $currentFolder
}

function Get-MtpFiles {
    param(
        [object]$Folder
    )

    $files = @()
    if (-not $Folder) { return $files }

    $items = $Folder.Items()
    if (-not $items) { return $files }

    foreach ($item in $items) {
        try {
            $itemFolder = $item.GetFolder()
            if (-not $itemFolder) {
                $fileName = $item.Name
                if (-not $fileName.StartsWith('.')) {
                    $files += $item
                }
            }
        } catch {
            $fileName = $item.Name
            if (-not $fileName.StartsWith('.')) {
                $files += $item
            }
        }
    }

    return $files
}

function Move-ToDiff {
    param(
        [string]$SourceDir,
        [string]$TargetDir,
        [string[]]$FilesToMove
    )

    if ($FilesToMove.Count -eq 0) {
        return
    }

    if (-not (Test-Path -Path $TargetDir)) {
        New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
    }

    $movedCount = 0
    foreach ($fileName in $FilesToMove) {
        $sourcePath = Join-Path -Path $SourceDir -ChildPath $fileName
        $destPath = Join-Path -Path $TargetDir -ChildPath $fileName

        try {
            Move-Item -Path $sourcePath -Destination $destPath -Force
            Write-Host "  Moved: $fileName"
            $movedCount++
        } catch {
            Write-Warning "  Failed to move ${fileName}: $_"
        }
    }

    Write-Host "Moved $movedCount file(s) to $TargetDir"
}

function Copy-FromMtp {
    param(
        [object]$MtpFolder,
        [string]$LocalDir,
        [array]$FilesToCopy
    )

    if ($FilesToCopy.Count -eq 0) {
        return
    }

    $totalFiles = $FilesToCopy.Count
    $copiedCount = 0

    Write-ProgressBar -Current 0 -Total $totalFiles -Message "Starting..."

    foreach ($file in $FilesToCopy) {
        $fileName = $file.Name
        $destPath = Join-Path -Path $LocalDir -ChildPath $fileName

        try {
            Write-ProgressBar -Current $copiedCount -Total $totalFiles -Message $fileName

            $destFolder = $shell.Namespace($LocalDir)

            $destFolder.CopyHere($file, 16)  # 16 = no progress dialog

            $copiedCount++
            Write-ProgressBar -Current $copiedCount -Total $totalFiles -Message $fileName
        } catch {
            Write-Host ""
            Write-Warning "Failed to copy ${fileName}: $_"
            Write-ProgressBar -Current $copiedCount -Total $totalFiles -Message ""
        }
    }

    Write-Host ""
    Write-Host "Copied $copiedCount file(s) from MTP"
}


try {
    if (-not (Test-Path -Path $LocalPath)) {
        throw "Local directory does not exist: $LocalPath"
    }

    $LocalPath = (Resolve-Path -Path $LocalPath).Path

    $device = Get-FirstMtpDevice

    if (-not $device) {
        throw "No MTP device found"
    }

    Write-Host "Connected to MTP device: $($device.Name)"
    Write-Host ""

    $mtpFolder = Get-MtpFolder -Device $device -Path $MtpPath

    if (-not $mtpFolder) {
        throw "Path not found on MTP device: $MtpPath"
    }

    Write-Host "Scanning files..."
    $localFileNames = @(Get-ChildItem -Path $LocalPath -File | Select-Object -ExpandProperty Name)
    $localFileSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $localFileNames) {
        $localFileSet.Add($name) | Out-Null
    }

    $mtpFiles = @(Get-MtpFiles -Folder $mtpFolder)
    $mtpFileNames = @($mtpFiles | ForEach-Object { $_.Name })
    $mtpFileSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $mtpFileNames) {
        $mtpFileSet.Add($name) | Out-Null
    }

    $filesToMove = @($localFileNames | Where-Object { -not $mtpFileSet.Contains($_) })
    $filesToCopy = @($mtpFiles | Where-Object { -not $localFileSet.Contains($_.Name) })

    Write-Host "  - Files to move to diff: $($filesToMove.Count)"
    Write-Host "  - Files to copy from MTP: $($filesToCopy.Count)"
    Write-Host ""

    if ($filesToMove.Count -gt 0) {
        Move-ToDiff -SourceDir $LocalPath -TargetDir $DiffDir -FilesToMove $filesToMove
        Write-Host ""
    }

    if ($filesToCopy.Count -gt 0) {
        Copy-FromMtp -MtpFolder $mtpFolder -LocalDir $LocalPath -FilesToCopy $filesToCopy
        Write-Host ""
    }

}  finally {
    if ($shell) {
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
    }
}
