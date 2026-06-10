@echo off
REM Troop Manager Enhanced Build Script (Batch Version)
REM Usage: build.bat [Debug|Release] [--clean]

setlocal enabledelayedexpansion

set CONFIG=Release
set CLEAN=0

if not "%1"=="" (
    if /i "%1"=="Debug" set CONFIG=Debug
    if /i "%1"=="Release" set CONFIG=Release
    if /i "%1"=="--clean" set CLEAN=1
)

if not "%2"=="" (
    if /i "%2"=="Debug" set CONFIG=Debug
    if /i "%2"=="Release" set CONFIG=Release
    if /i "%2"=="--clean" set CLEAN=1
)

set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%
set PROJECT_FILE=%PROJECT_ROOT%TroopManagerEnhanced.csproj
set OUTPUT_DIR=%PROJECT_ROOT%output
set BIN_OUTPUT_DIR=%PROJECT_ROOT%bin\%CONFIG%
set MODULE_OUTPUT_DIR=%PROJECT_ROOT%_Module\bin\Win64_Shipping_Client

echo.
echo ========================================
echo Troop Manager Enhanced Build Script
echo ========================================
echo Configuration: %CONFIG%
echo Project Root: %PROJECT_ROOT%
echo Output Directory: %OUTPUT_DIR%
echo.

REM Clean step
if %CLEAN% equ 1 (
    echo Cleaning previous builds...
    if exist "%OUTPUT_DIR%" (
        rmdir /s /q "%OUTPUT_DIR%"
        echo Cleaned output directory
    )
    if exist "%BIN_OUTPUT_DIR%" (
        rmdir /s /q "%BIN_OUTPUT_DIR%"
        echo Cleaned bin directory
    )
)

REM Create output directory
if not exist "%OUTPUT_DIR%" (
    mkdir "%OUTPUT_DIR%"
    echo Created output directory
)

REM Build project
echo Building project with dotnet build (%CONFIG%)...
cd /d "%PROJECT_ROOT%"
call dotnet build "%PROJECT_FILE%" --configuration %CONFIG%

if errorlevel 1 (
    echo Build failed!
    exit /b 1
)

echo Build succeeded!

REM Copy DLL and PDB to output directory
echo Copying build artifacts to output directory...

set DLL_FILE=%BIN_OUTPUT_DIR%\TroopManagerEnhanced.dll
set PDB_FILE=%BIN_OUTPUT_DIR%\TroopManagerEnhanced.pdb

if exist "%DLL_FILE%" (
    copy "%DLL_FILE%" "%OUTPUT_DIR%" /y
    echo Copied DLL: TroopManagerEnhanced.dll
) else (
    echo Error: DLL not found: %DLL_FILE%
    exit /b 1
)

if exist "%PDB_FILE%" (
    copy "%PDB_FILE%" "%OUTPUT_DIR%" /y
    echo Copied PDB: TroopManagerEnhanced.pdb
) else (
    echo Warning: PDB not found
)

REM Copy entire _Module structure
echo Copying _Module directory structure...

set SOURCE_MODULE_DIR=%PROJECT_ROOT%_Module
set OUTPUT_MODULE_DIR=%OUTPUT_DIR%\_Module

if exist "%SOURCE_MODULE_DIR%" (
    if exist "%OUTPUT_MODULE_DIR%" (
        rmdir /s /q "%OUTPUT_MODULE_DIR%"
    )
    xcopy "%SOURCE_MODULE_DIR%" "%OUTPUT_MODULE_DIR%" /e /i /y >nul
    echo Copied _Module directory structure
) else (
    echo Warning: _Module directory not found
)

echo.
echo ========================================
echo Build Complete!
echo ========================================
echo Output Location: %OUTPUT_DIR%
echo Ready for deployment!
echo.

endlocal
