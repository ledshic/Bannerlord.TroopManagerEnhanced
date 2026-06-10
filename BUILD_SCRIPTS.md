# Build Scripts for Troop Manager Enhanced

This directory contains build scripts that compile the mod and organize output artifacts.

## Available Scripts

### Batch Script: `build.bat` (Windows)
Simple batch script for Windows command prompt.

**Usage:**
```batch
build.bat [Debug|Release] [--clean]
```

**Examples:**
```batch
build.bat Release          # Build Release configuration
build.bat Debug            # Build Debug configuration
build.bat Release --clean  # Clean then build Release
build.bat --clean Release  # Clean then build Release
```

### PowerShell Script: `build.ps1` (Advanced)
Advanced PowerShell script with better output formatting and options.

**Usage:**
```powershell
.\build.ps1 -Configuration Release -Verbose -Clean
```

**Parameters:**
- `-Configuration`: `Debug` or `Release` (default: `Release`)
- `-Clean`: Remove output directory before building
- `-Verbose`: Show detailed build information

**Examples:**
```powershell
.\build.ps1                              # Build Release
.\build.ps1 -Configuration Debug         # Build Debug
.\build.ps1 -Configuration Release -Clean # Clean build Release
```

## Output

Both scripts produce artifacts in the `output/` directory:

```
output/
├── TroopManagerEnhanced.dll      # Compiled assembly
├── TroopManagerEnhanced.pdb      # Debug symbols
└── _Module/                      # Complete mod structure
    ├── SubModule.xml
    ├── ModuleData/
    └── bin/
        └── Win64_Shipping_Client/
            ├── TroopManagerEnhanced.dll
            └── TroopManagerEnhanced.pdb
```

The `output/` directory is **not tracked by Git** (see `.gitignore`).

## Deployment

To deploy the mod:

1. Run the build script:
   ```batch
   build.bat Release
   ```

2. Copy the contents of `output/_Module` to your Bannerlord mods directory:
   - Windows: `Documents\Mount and Blade II Bannerlord\Modules\`
   - Or Steam: `steamapps\common\Mount & Blade II Bannerlord\Modules\`

3. Enable the mod in the Bannerlord launcher

## Prerequisites

- .NET SDK (6.0 or later)
- Bannerlord game installed (path configured in `TroopManagerEnhanced.csproj`)
- All dependencies from `SubModule.xml` installed

## Troubleshooting

### Build fails with "Could not find..."
- Update the `GameFolder` path in `TroopManagerEnhanced.csproj`
- Ensure all mod dependencies are installed (Harmony, MCM, ButterLib, UIExtenderEx)

### PowerShell execution policy error
- Run in command prompt using batch script instead:
  ```batch
  build.bat Release
  ```
- Or set execution policy:
  ```powershell
  Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
  ```

### Files not found in output
- Ensure the project builds successfully
- Check `bin/Release/` folder contains `TroopManagerEnhanced.dll`
- Verify `_Module/bin/Win64_Shipping_Client/` has the DLL

## Build Configuration

The build process is configured in `.csproj`:
- **Output**: `bin/$(Configuration)/`
- **Module Structure**: Auto-copied to `_Module/bin/Win64_Shipping_Client/`
- **Scripts**: Copy from `_Module` to `output/` for deployment

See `TroopManagerEnhanced.csproj` for detailed build targets.
