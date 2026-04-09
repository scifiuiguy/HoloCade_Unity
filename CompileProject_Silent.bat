@echo off
REM HoloCade Unity Compilation Check (Silent/Non-Interactive)
REM For automated testing and AI assistants
REM Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

REM Find Unity executable
set UNITY_PATH=""
if exist "C:\Program Files\Unity\Hub\Editor\6000.0.60f1\Editor\Unity.exe" (
    set UNITY_PATH="C:\Program Files\Unity\Hub\Editor\6000.0.60f1\Editor\Unity.exe"
) else if exist "C:\Program Files\Unity\Hub\Editor\2022.3.19f1\Editor\Unity.exe" (
    set UNITY_PATH="C:\Program Files\Unity\Hub\Editor\2022.3.19f1\Editor\Unity.exe"
) else (
    echo ERROR: Unity executable not found
    exit /b 1
)

REM Get project path
set PROJECT_PATH=%~dp0
set PROJECT_PATH=%PROJECT_PATH:~0,-1%

REM Delete old report files to force fresh compilation check
if exist "%PROJECT_PATH%\Temp\CompilationErrors.log" del "%PROJECT_PATH%\Temp\CompilationErrors.log"
if exist "%PROJECT_PATH%\Temp\UnityBatchCompile.log" del "%PROJECT_PATH%\Temp\UnityBatchCompile.log"

REM Start Unity without -quit flag
start "HoloCade Unity Compiler" /B %UNITY_PATH% -batchmode -nographics -projectPath "%PROJECT_PATH%" -executeMethod HoloCade.Editor.CompilationReporterCLI.CompileAndExit -logFile "%PROJECT_PATH%\Temp\UnityBatchCompile.log"

REM Wait for report (3 minute timeout - increased to account for initial Unity compilation)
set TIMEOUT_COUNTER=0
set MAX_TIMEOUT=180

:WAIT_FOR_REPORT
if exist "%PROJECT_PATH%\Temp\CompilationErrors.log" goto REPORT_FOUND
powershell -NoProfile -Command "Start-Sleep -Seconds 1" >nul
set /a TIMEOUT_COUNTER+=1
if %TIMEOUT_COUNTER% GEQ %MAX_TIMEOUT% goto TIMEOUT_REACHED
goto WAIT_FOR_REPORT

:TIMEOUT_REACHED
echo TIMEOUT: Compilation report not generated
taskkill /IM Unity.exe /F >nul 2>&1
exit /b 1

:REPORT_FOUND
REM Give Unity time to finish writing and compilation
REM Check log file for completion message
set WAIT_COMPLETE=0
set MAX_WAIT_COMPLETE=30
:WAIT_FOR_COMPLETE
findstr /C:"[HoloCade AUTO-COMPILE] Compilation complete" "%PROJECT_PATH%\Temp\UnityBatchCompile.log" >nul 2>&1
if %errorlevel% NEQ 0 findstr /C:"Compilation finished" "%PROJECT_PATH%\Temp\UnityBatchCompile.log" >nul 2>&1
if %errorlevel% NEQ 0 findstr /C:"Report generated successfully" "%PROJECT_PATH%\Temp\UnityBatchCompile.log" >nul 2>&1
if %errorlevel% EQU 0 goto COMPILATION_COMPLETE
powershell -NoProfile -Command "Start-Sleep -Seconds 1" >nul
set /a WAIT_COMPLETE+=1
if %WAIT_COMPLETE% GEQ %MAX_WAIT_COMPLETE% goto COMPILATION_COMPLETE
goto WAIT_FOR_COMPLETE

:COMPILATION_COMPLETE
REM Give Unity a moment to finish writing the report
powershell -NoProfile -Command "Start-Sleep -Seconds 2" >nul

REM Kill Unity
taskkill /IM Unity.exe /F >nul 2>&1

REM Check report status
findstr /C:"Status: SUCCESS" "%PROJECT_PATH%\Temp\CompilationErrors.log" >nul
if %errorlevel% EQU 0 (
    echo SUCCESS: Project compiled with no errors
    exit /b 0
) else (
    echo FAILED: Compilation errors detected
    exit /b 1
)




