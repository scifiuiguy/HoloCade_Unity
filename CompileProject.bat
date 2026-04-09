@echo off
REM HoloCade Unity Compilation Check
REM Launches Unity in batch mode, compiles project, and outputs errors to log
REM Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

echo ========================================
echo HoloCade Unity Compilation Check
echo ========================================
echo.

REM Find Unity executable
set UNITY_PATH=""
if exist "C:\Program Files\Unity\Hub\Editor\6000.0.60f1\Editor\Unity.exe" (
    set UNITY_PATH="C:\Program Files\Unity\Hub\Editor\6000.0.60f1\Editor\Unity.exe"
) else if exist "C:\Program Files\Unity\Hub\Editor\2022.3.19f1\Editor\Unity.exe" (
    set UNITY_PATH="C:\Program Files\Unity\Hub\Editor\2022.3.19f1\Editor\Unity.exe"
) else if exist "C:\Program Files\Unity\Hub\Editor\6.0.27f1\Editor\Unity.exe" (
    set UNITY_PATH="C:\Program Files\Unity\Hub\Editor\6.0.27f1\Editor\Unity.exe"
) else if exist "C:\Program Files\Unity\Hub\Editor\2022.3.54f1\Editor\Unity.exe" (
    set UNITY_PATH="C:\Program Files\Unity\Hub\Editor\2022.3.54f1\Editor\Unity.exe"
) else (
    echo ERROR: Unity executable not found
    echo Please edit this script to set UNITY_PATH manually
    echo.
    echo Common paths:
    echo   C:\Program Files\Unity\Hub\Editor\[VERSION]\Editor\Unity.exe
    echo   C:\Program Files\Unity\Editor\Unity.exe
    echo.
    pause
    exit /b 1
)

echo Unity Path: %UNITY_PATH%
echo Project Path: %~dp0
echo.

REM Get project path (remove trailing backslash)
set PROJECT_PATH=%~dp0
set PROJECT_PATH=%PROJECT_PATH:~0,-1%

REM Run Unity in batch mode to compile
echo Starting Unity compilation (this may take 1-2 minutes)...
echo Please wait...
echo.

REM Start Unity without -quit flag so reporter can write before exit
start "HoloCade Unity Compiler" /B %UNITY_PATH% -batchmode -nographics -projectPath "%PROJECT_PATH%" -executeMethod HoloCade.Editor.CompilationReporterCLI.CompileAndExit -logFile "%PROJECT_PATH%\Temp\UnityBatchCompile.log"

echo Waiting for Unity to finish compilation and generate report...
echo (This will take 30-60 seconds on first run)
echo.

REM Wait for compilation report to be generated (with timeout)
set TIMEOUT_COUNTER=0
set MAX_TIMEOUT=120

:WAIT_FOR_REPORT
if exist "%PROJECT_PATH%\Temp\CompilationErrors.log" goto REPORT_FOUND
timeout /t 1 /nobreak >nul
set /a TIMEOUT_COUNTER+=1
if %TIMEOUT_COUNTER% GEQ %MAX_TIMEOUT% goto TIMEOUT_REACHED
goto WAIT_FOR_REPORT

:TIMEOUT_REACHED
echo WARNING: Compilation report not generated within 2 minutes
echo Unity may still be compiling - check Unity log
goto KILL_UNITY

:REPORT_FOUND
echo Report generated successfully!
echo.

:KILL_UNITY
REM Give Unity a moment to finish writing
timeout /t 2 /nobreak >nul

REM Kill Unity process
echo Shutting down Unity...
taskkill /FI "WINDOWTITLE eq HoloCade Unity Compiler*" /F >nul 2>&1
taskkill /IM Unity.exe /F >nul 2>&1

set UNITY_EXIT_CODE=0

echo.
echo ========================================
echo Unity Compilation Complete
echo ========================================
echo Exit Code: %UNITY_EXIT_CODE%
echo.

REM Check if compilation report was generated
if exist "%PROJECT_PATH%\Temp\CompilationErrors.log" (
    echo Compilation Report:
    echo -------------------------------------------
    type "%PROJECT_PATH%\Temp\CompilationErrors.log"
    echo -------------------------------------------
    echo.
    echo Full report saved to: Temp\CompilationErrors.log
    echo Unity log saved to: Temp\UnityBatchCompile.log
) else (
    echo WARNING: Compilation report not found
    echo Check Unity log for details: Temp\UnityBatchCompile.log
)

echo.
if %UNITY_EXIT_CODE% EQU 0 (
    echo ✓ COMPILATION SUCCESSFUL
) else (
    echo ✗ COMPILATION FAILED
)
echo.

pause
exit /b %UNITY_EXIT_CODE%

