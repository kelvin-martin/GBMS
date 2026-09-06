[CmdletBinding()]
param (
    [Parameter(Mandatory = $false, HelpMessage = "The destination directory for the archived files.")]
    [string]$DestinationPath
)

# Use the current working directory if SourcePath is not provided
$resolvedSource = $ExecutionContext.SessionState.Path.CurrentFileSystemLocation.Path

# Prompt for destination if not provided via command line
if ([string]::IsNullOrWhiteSpace($DestinationPath)) {
    $DestinationPath = Read-Host -Prompt "Enter the destination path for the archive"
}

$resolvedDestination = [System.IO.Path]::GetFullPath($DestinationPath)

Write-Host "Archiving .cs, .csproj, and .sln files from current directory: '$resolvedSource'" -ForegroundColor Cyan
Write-Host "Destination: '$resolvedDestination'..." -ForegroundColor Cyan

Get-ChildItem -Path $resolvedSource -Recurse | 
    Where-Object { $_.Extension -match '^\.(cs|csproj|sln)$' -and $_.FullName -notmatch '\\\.git\\' } | 
    ForEach-Object {
        $relativePath = $_.FullName.Substring($resolvedSource.Length)
        $destinationFile = Join-Path -Path $resolvedDestination -ChildPath $relativePath
        $destinationDir = Split-Path -Path $destinationFile -Parent
        
        if (!(Test-Path -Path $destinationDir)) {
            New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
        }
        
        Copy-Item -Path $_.FullName -Destination $destinationFile -Force
        Write-Host "Copied: $relativePath" -ForegroundColor Gray
    }

Write-Host "Archive process complete!" -ForegroundColor Green
