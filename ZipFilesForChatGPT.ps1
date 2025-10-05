param(
    [Parameter(Position=0)]
    [string]$Root = (Get-Location).Path,

    [Parameter(Position=1)]
    [string]$DestinationZip = "MarvinsAIRARefactored.zip",

    # Optional: put every entry under a top-level folder inside the zip
    [string]$RootFolderName = ""
)

# Include only these extensions
$extensions = @(
    '.cs', '.xaml', '.resx', '.csproj', '.sln',
    '.props', '.targets', '.json', '.xml', '.config',
    '.iss', '.js', '.html', '.css', '.ino', '.py', '.php'
)

# Normalize paths
$rootPath = (Resolve-Path -LiteralPath $Root).Path
if (-not [System.IO.Path]::IsPathRooted($DestinationZip)) {
    $DestinationZip = Join-Path -Path $rootPath -ChildPath $DestinationZip
}

# Ensure destination directory exists
$zipDir = Split-Path -Parent $DestinationZip
if (-not (Test-Path -LiteralPath $zipDir)) {
    New-Item -ItemType Directory -Path $zipDir | Out-Null
}

# Remove existing zip if present
if (Test-Path -LiteralPath $DestinationZip) {
    Remove-Item -LiteralPath $DestinationZip -Force
}

# Collect files, excluding .git/.vs/bin/obj anywhere in the path
$excludeRegex = '(^|[\\/])(\.idea|\.git|\.vs|bin|obj|resx|resx-backups)([\\/]|$)'

$files = Get-ChildItem -LiteralPath $rootPath -Recurse -File -Force |
    Where-Object {
        $extensions -contains $_.Extension.ToLowerInvariant() -and
        ($_.FullName -notmatch $excludeRegex)
    }

if (-not $files) {
    Write-Warning "No matching files found under '$rootPath'."
    exit 0
}

# Load compression APIs
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-RelPath([string]$basePath, [string]$fullPath) {
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        return [System.IO.Path]::GetRelativePath($basePath, $fullPath)
    } else {
        $baseUri = New-Object System.Uri ($basePath.TrimEnd('\') + '\')
        $fullUri = New-Object System.Uri $fullPath
        $rel = $baseUri.MakeRelativeUri($fullUri).ToString()
        return [System.Uri]::UnescapeDataString($rel)
    }
}

$prefix = if ([string]::IsNullOrWhiteSpace($RootFolderName)) { "" } else { ($RootFolderName.TrimEnd('\','/') + "/") }

# Create the zip and add each file with its relative path preserved
$zip = [System.IO.Compression.ZipFile]::Open($DestinationZip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($f in $files) {
        $rel = Get-RelPath $rootPath $f.FullName
        $entryPath = ($prefix + $rel) -replace '\\','/'   # zip uses forward slashes
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip, $f.FullName, $entryPath, [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Created zip:" -ForegroundColor Green
Write-Host "  $DestinationZip"
Write-Host "Files included:" $files.Count
