# Troop Manager Enhanced Build Script
# This script builds the mod and copies the output to the output/ directory

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Clean,
    [switch]$Verbose
)

# Module Identity
$ModuleId = "TroopsManagerEnhanced"

# Colors for output
$colors = @{
    Success = "Green"
    Error = "Red"
    Warning = "Yellow"
    Info = "Cyan"
}

function Write-Message {
    param([string]$Message, [ValidateSet("Success", "Error", "Warning", "Info")]$Type = "Info")
    Write-Host $Message -ForegroundColor $colors[$Type]
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = $scriptDir
$projectFile = Join-Path $projectRoot "TroopManagerEnhanced.csproj"
$outputDir = Join-Path $projectRoot "output"
$binOutputDir = Join-Path (Join-Path $projectRoot "bin") $Configuration
$moduleOutputDir = Join-Path (Join-Path (Join-Path $projectRoot "_Module") "bin") "Win64_Shipping_Client"

# Output artifact names
$dllFileName = "$ModuleId.dll"
$pdbFileName = "$ModuleId.pdb"

Write-Message "========================================" "Info"
Write-Message "Troop Manager Enhanced Build Script" "Info"
Write-Message "========================================" "Info"
Write-Message "Configuration: $Configuration" "Info"
Write-Message "Project Root: $projectRoot" "Info"
Write-Message "Output Directory: $outputDir" "Info"
Write-Message "" "Info"

# Always clean output directory before building
Write-Message "Cleaning output directory before build..." "Warning"
if (Test-Path $outputDir) {
    Remove-Item -Path $outputDir -Recurse -Force
    Write-Message "Cleaned output directory" "Success"
}

# Clean step (for bin directory if requested)
if ($Clean) {
    Write-Message "Cleaning bin directory..." "Warning"
    if (Test-Path $binOutputDir) {
        Remove-Item -Path $binOutputDir -Recurse -Force
        Write-Message "Cleaned bin directory" "Success"
    }
}

# Create output directory
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
    Write-Message "Created output directory: $outputDir" "Success"
}

# Build project
Write-Message "Building project with dotnet build ($Configuration)..." "Info"
Set-Location $projectRoot
$buildOutput = dotnet build $projectFile --configuration $Configuration 2>&1

# Check if build succeeded
if ($LASTEXITCODE -eq 0) {
    Write-Message "Build succeeded!" "Success"
} else {
    Write-Message "Build failed!" "Error"
    Write-Host $buildOutput
    exit 1
}

# Copy DLL and PDB to output directory with Module ID name
Write-Message "Copying build artifacts to output directory..." "Info"

$dllFile = Join-Path $binOutputDir "TroopManagerEnhanced.dll"
$pdbFile = Join-Path $binOutputDir "TroopManagerEnhanced.pdb"

$outputDllFile = Join-Path $outputDir $dllFileName
$outputPdbFile = Join-Path $outputDir $pdbFileName

if (Test-Path $dllFile) {
    Copy-Item -Path $dllFile -Destination $outputDllFile -Force
    Write-Message "Copied DLL: $dllFileName" "Success"
} else {
    Write-Message "DLL not found: $dllFile" "Error"
    exit 1
}

if (Test-Path $pdbFile) {
    Copy-Item -Path $pdbFile -Destination $outputPdbFile -Force
    Write-Message "Copied PDB: $pdbFileName" "Success"
} else {
    Write-Message "Warning: PDB not found" "Warning"
}

# Copy entire _Module structure
Write-Message "Copying _Module directory structure..." "Info"
$sourceModuleDir = Join-Path $projectRoot "_Module"
$outputModuleDir = Join-Path $outputDir "_Module"

if (Test-Path $sourceModuleDir) {
    if (Test-Path $outputModuleDir) {
        Remove-Item -Path $outputModuleDir -Recurse -Force
    }
    Copy-Item -Path $sourceModuleDir -Destination $outputModuleDir -Recurse -Force
    Write-Message "Copied _Module directory structure" "Success"
} else {
    Write-Message "Warning: _Module directory not found" "Warning"
}

# Summary
Write-Message "" "Info"
Write-Message "========================================" "Info"
Write-Message "Build Complete!" "Success"
Write-Message "========================================" "Info"
Write-Message "Output Location: $outputDir" "Info"

# List output files
$outputFiles = Get-ChildItem -Path $outputDir -File -Recurse
if ($outputFiles.Count -gt 0) {
    Write-Message "" "Info"
    Write-Message "Output Files:" "Info"
    $outputFiles | ForEach-Object {
        $size = $_.Length / 1KB
        Write-Message "  - $($_.Name) ($([math]::Round($size, 2)) KB)" "Success"
    }
}

Write-Message "" "Info"
Write-Message "Ready for deployment!" "Success"
