@echo off
REM HoloCade Unity Dedicated Server Launcher
REM Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

echo ========================================
echo HoloCade Unity Dedicated Server Launcher
echo ========================================
echo.

REM Package root (Builds/, package.json) — parent of BuildTooling~/
set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%.." >nul 2>&1
set "PACKAGE_ROOT=%CD%"
popd >nul 2>&1

REM Default configuration
set EXPERIENCE_TYPE=AIFacemask
set SCENE_NAME=HoloCadeScene
set PORT=7777
set MAX_PLAYERS=4

REM Parse command-line arguments
:parse_args
if "%~1"=="" goto :start_server
if /i "%~1"=="-experience" (
    set EXPERIENCE_TYPE=%~2
    shift
    shift
    goto :parse_args
)
if /i "%~1"=="-port" (
    set PORT=%~2
    shift
    shift
    goto :parse_args
)
if /i "%~1"=="-maxplayers" (
    set MAX_PLAYERS=%~2
    shift
    shift
    goto :parse_args
)
shift
goto :parse_args

:start_server
echo Starting HoloCade Unity Dedicated Server...
echo Experience Type: %EXPERIENCE_TYPE%
echo Scene: %SCENE_NAME%
echo Port: %PORT%
echo Max Players: %MAX_PLAYERS%
echo.

REM Builds/ lives at package root (parent of BuildTooling~/)
set SERVER_PATH="%PACKAGE_ROOT%\Builds\Server\HoloCade_UnityServer.exe"

REM Check if server executable exists
if not exist %SERVER_PATH% (
    echo ERROR: Server executable not found at %SERVER_PATH%
    echo.
    echo Please build the dedicated server target first:
    echo 1. Open Unity
    echo 2. File ^> Build Settings
    echo 3. Select "Dedicated Server" platform
    echo 4. Click "Build" and save to Builds/Server/
    echo.
    pause
    exit /b 1
)

REM Launch the dedicated server
echo Launching server...
start "HoloCade Unity Dedicated Server" %SERVER_PATH% -batchmode -nographics -port %PORT% -scene %SCENE_NAME% -experienceType %EXPERIENCE_TYPE% -maxPlayers %MAX_PLAYERS% -logFile ServerLog.txt

echo.
echo Server launched successfully!
echo.
echo To stop the server, close the server window or use Task Manager.
echo.
pause















