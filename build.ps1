# Troop Manager Enhanced Build Script
# Build process:
# 1. Sync version from .csproj into _Module/SubModule.xml (single source of truth = csproj <Version>)
# 2. Clean _Module internal files
# 3. Rebuild the module (triggers post-build copy via .csproj)
# 4. Clean output directory
# 5. Migrate _Module to output/ and rename to final module folder
# 6. Verify artifact completeness
#
# Notes:
# - Run with PowerShell Core (pwsh) on macOS/Linux for cross-platform use.
# - Version in SubModule.xml is automatically kept in sync with the .csproj <Version> property.
# - After running, copy the final folder from ./output/ into your game's Modules/ directory.

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Verbose
)

# Module Identity
$ModuleId = "TroopManagerEnhanced"

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
$moduleDir = Join-Path $projectRoot "_Module"
$moduleBinDir = Join-Path (Join-Path $moduleDir "bin") "Win64_Shipping_Client"
$moduleModuleDataDir = Join-Path $moduleDir "ModuleData"
$projectBinDir = Join-Path (Join-Path $projectRoot "bin") $Configuration
$outputDir = Join-Path $projectRoot "output"

# Expected artifacts
$dllFileName = "$ModuleId.dll"
$pdbFileName = "$ModuleId.pdb"
$moduleName = $ModuleId

Write-Message "========================================" "Info"
Write-Message "Troop Manager Enhanced Build Script" "Info"
Write-Message "========================================" "Info"
Write-Message "Configuration: $Configuration" "Info"
Write-Message "" "Info"

# ============================================
# Version Sync (NEW - keeps SubModule.xml in sync)
# ============================================
Write-Message "[0/6] Syncing version from .csproj to SubModule.xml..." "Info"
$csprojContent = Get-Content $projectFile -Raw
if ($csprojContent -match '<Version>([^<]+)</Version>') {
    $rawVersion = $matches[1].Trim()
    $displayVersion = if ($rawVersion.StartsWith("v")) { $rawVersion } else { "v$rawVersion" }

    $subModulePath = Join-Path $moduleDir "SubModule.xml"
    if (Test-Path $subModulePath) {
        $subContent = Get-Content $subModulePath -Raw
        $updated = $subContent -replace '(<Version value=")[^"]*(")', "`${1}$displayVersion`$2"
        if ($updated -ne $subContent) {
            Set-Content -Path $subModulePath -Value $updated -NoNewline
            Write-Message "  Synced version $displayVersion to _Module/SubModule.xml" "Success"
        } else {
            Write-Message "  Version already in sync ($displayVersion)" "Info"
        }
    } else {
        Write-Message "  WARNING: SubModule.xml not found in _Module/" "Warning"
    }
} else {
    Write-Message "  WARNING: Could not parse <Version> from .csproj" "Warning"
}

# ============================================
# Step 1: Clean _Module internal files
# ============================================
Write-Message "[1/6] Cleaning _Module internal files..." "Warning"
if (Test-Path $moduleBinDir) {
    Get-ChildItem -Path $moduleBinDir -File -Force | Remove-Item -Force -ErrorAction SilentlyContinue
    Write-Message "  Cleaned _Module bin directory" "Success"
}
if (Test-Path $moduleModuleDataDir) {
    Remove-Item -Path $moduleModuleDataDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Message "  Cleaned _Module ModuleData (will be repopulated by build)" "Success"
}

# ============================================
# Step 2: Rebuild the module
# ============================================
Write-Message "[2/6] Building project ($Configuration configuration)..." "Info"
Set-Location $projectRoot
$buildOutput = dotnet build $projectFile --configuration $Configuration 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Message "  Build succeeded!" "Success"
} else {
    Write-Message "  Build failed!" "Error"
    if ($Verbose) {
        Write-Host $buildOutput
    }
    exit 1
}

# ============================================
# Step 3: Clean output directory
# ============================================
Write-Message "[3/6] Preparing output directory..." "Info"
if (Test-Path $outputDir) {
    Remove-Item -Path $outputDir -Recurse -Force
    Write-Message "  Cleaned output directory" "Success"
}

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
    Write-Message "  Created output directory" "Success"
}

# ============================================
# Step 4: Migrate _Module to output and rename
# ============================================
Write-Message "[4/6] Migrating module to output..." "Info"

if (-not (Test-Path $moduleDir)) {
    Write-Message "  ERROR: _Module directory not found!" "Error"
    exit 1
}

# Copy _Module to output
$tempModuleDir = Join-Path $outputDir "_Module"
Copy-Item -Path $moduleDir -Destination $tempModuleDir -Recurse -Force
Write-Message "  Copied _Module structure" "Success"

# Copy project bin DLL/PDB to module bin
$srcDll = Join-Path $projectBinDir "TroopManagerEnhanced.dll"
$srcPdb = Join-Path $projectBinDir "TroopManagerEnhanced.pdb"
$destBinDir = Join-Path (Join-Path $tempModuleDir "bin") "Win64_Shipping_Client"

if (-not (Test-Path $destBinDir)) {
    New-Item -ItemType Directory -Path $destBinDir -Force | Out-Null
} else {
    # Clean old DLL/PDB files before copying new ones
    Get-ChildItem -Path $destBinDir -Filter "*.dll" -Force -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path $destBinDir -Filter "*.pdb" -Force -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}

if (Test-Path $srcDll) {
    Copy-Item -Path $srcDll -Destination (Join-Path $destBinDir $dllFileName) -Force
    Write-Message "  Copied DLL to module bin" "Success"
} else {
    Write-Message "  ERROR: DLL not found at $srcDll" "Error"
    exit 1
}

if (Test-Path $srcPdb) {
    Copy-Item -Path $srcPdb -Destination (Join-Path $destBinDir $pdbFileName) -Force
    Write-Message "  Copied PDB to module bin" "Success"
}

# Copy ModuleData to module
$sourceModuleDataDir = Join-Path $projectRoot "ModuleData"
$destModuleDataDir = Join-Path $tempModuleDir "ModuleData"

if (Test-Path $sourceModuleDataDir) {
    if (Test-Path $destModuleDataDir) {
        Remove-Item -Path $destModuleDataDir -Recurse -Force
    }
    Copy-Item -Path $sourceModuleDataDir -Destination $destModuleDataDir -Recurse -Force
    Write-Message "  Copied ModuleData files" "Success"
}

# Rename _Module to Module ID
$finalModuleDir = Join-Path $outputDir $moduleName
if (Test-Path $tempModuleDir) {
    Rename-Item -Path $tempModuleDir -NewName $moduleName
    Write-Message "  Renamed module to $moduleName" "Success"
}

# ============================================
# Step 5: Verify artifact completeness
# ============================================
Write-Message "[5/6] Verifying artifact completeness..." "Info"

$expectedDll = Join-Path (Join-Path (Join-Path $finalModuleDir "bin") "Win64_Shipping_Client") $dllFileName
$expectedPdb = Join-Path (Join-Path (Join-Path $finalModuleDir "bin") "Win64_Shipping_Client") $pdbFileName
$expectedModuleData = Join-Path $finalModuleDir "ModuleData"

$allValid = $true

if (Test-Path $expectedDll) {
    $dllSize = (Get-Item $expectedDll).Length / 1KB
    Write-Message "  ✓ DLL found: $dllFileName ($([math]::Round($dllSize, 2)) KB)" "Success"
} else {
    Write-Message "  ✗ DLL missing: $expectedDll" "Error"
    $allValid = $false
}

if (Test-Path $expectedPdb) {
    $pdbSize = (Get-Item $expectedPdb).Length / 1KB
    Write-Message "  ✓ PDB found: $pdbFileName ($([math]::Round($pdbSize, 2)) KB)" "Success"
} else {
    Write-Message "  ✓ PDB optional (not found)" "Warning"
}

if (Test-Path $expectedModuleData) {
    $xmlFiles = Get-ChildItem -Path $expectedModuleData -Filter "*.xml" -Recurse
    if ($xmlFiles.Count -gt 0) {
        Write-Message "  ✓ ModuleData found: $($xmlFiles.Count) XML files" "Success"
    } else {
        Write-Message "  ✗ ModuleData exists but no XML files found" "Error"
        $allValid = $false
    }
} else {
    Write-Message "  ! ModuleData not found (optional)" "Warning"
}

if (-not $allValid) {
    Write-Message "" "Error"
    Write-Message "Verification FAILED - some artifacts are missing!" "Error"
    exit 1
}

# ============================================
# Summary
# ============================================
Write-Message "" "Info"
Write-Message "========================================" "Info"
Write-Message "Build Complete!" "Success"
Write-Message "========================================" "Info"
Write-Message "Output Location: $outputDir" "Info"
Write-Message "Module Folder: $moduleName" "Info"
Write-Message "" "Info"

# List all output files
$outputFiles = Get-ChildItem -Path $outputDir -File -Recurse | Sort-Object FullName
if ($outputFiles.Count -gt 0) {
    Write-Message "Generated Files:" "Info"
    foreach ($file in $outputFiles) {
        $relativePath = $file.FullName -replace [regex]::Escape("$outputDir\"), ""
        $size = $file.Length / 1KB
        Write-Message "  • $relativePath ($([math]::Round($size, 2)) KB)" "Success"
    }
}

Write-Message "" "Info"
Write-Message "Ready for deployment!" "Success"
