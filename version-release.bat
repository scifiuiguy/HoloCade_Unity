@echo off
REM HoloCade Version Release Script
REM Updates package.json and creates a git tag for the release

setlocal

echo ========================================
echo HoloCade Version Release Helper
echo ========================================
echo.

set /p VERSION="Enter version number (e.g., 0.1.0): "
if "%VERSION%"=="" (
    echo Error: Version cannot be empty
    exit /b 1
)

echo.
echo Step 1: Updating package.json version to %VERSION%...
powershell -NoProfile -Command "$content = Get-Content 'Packages\com.ajcampbell.holocade\package.json' -Raw; $content = $content -replace '(\"version\":\s*\")([^\"]+)(\")', '$1%VERSION%$3'; Set-Content 'Packages\com.ajcampbell.holocade\package.json' -Value $content -NoNewline"
if %ERRORLEVEL% NEQ 0 (
    echo Error: Failed to update package.json
    exit /b 1
)
echo   [OK] package.json updated

echo.
echo Step 2: Staging package.json...
git add Packages/com.ajcampbell.holocade/package.json
if %ERRORLEVEL% NEQ 0 (
    echo Warning: Git add failed (package.json may not have changed)
)

echo.
set /p COMMIT_CHANGES="Commit version change? (y/n): "
if /i "%COMMIT_CHANGES%"=="y" (
    git commit -m "Bump version to %VERSION%"
    if %ERRORLEVEL% NEQ 0 (
        echo Warning: Commit failed (may be no changes to commit)
    ) else (
        echo   [OK] Version change committed
    )
)

echo.
set /p CREATE_TAG="Create git tag v%VERSION%? (y/n): "
if /i "%CREATE_TAG%"=="y" (
    set /p TAG_MESSAGE="Tag message (or press Enter for default): "
    if "!TAG_MESSAGE!"=="" set TAG_MESSAGE=Release version %VERSION%
    
    git tag -a v%VERSION% -m "!TAG_MESSAGE!"
    if %ERRORLEVEL% NEQ 0 (
        echo Error: Failed to create tag (tag may already exist)
        exit /b 1
    )
    echo   [OK] Tag v%VERSION% created
)

echo.
echo ========================================
echo Version Release Complete!
echo ========================================
echo.
echo Next steps:
echo   1. Review changes: git log -1
if /i "%CREATE_TAG%"=="y" (
    echo   2. Review tag: git show v%VERSION%
    echo   3. Push commits: git push origin main
    echo   4. Push tag: git push origin v%VERSION%
) else (
    echo   1. Push commits: git push origin main
)
echo.

endlocal



