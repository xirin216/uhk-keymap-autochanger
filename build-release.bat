@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%" >nul

set "SOLUTION=UhkKeymapAutochanger.sln"
set "PROJECT=src\UhkKeymapAutochanger\UhkKeymapAutochanger.csproj"
set "PUBLISH_DIR=src\UhkKeymapAutochanger\bin\Release\net8.0-windows\win-x64\publish"
set "OUTPUT_EXE=%PUBLISH_DIR%\UhkKeymapAutochanger.exe"
set "DIST_DIR=dist"

echo [1/4] Restoring...
dotnet restore "%SOLUTION%"
if errorlevel 1 goto :error

echo [2/4] Building...
dotnet build "%SOLUTION%" -c Release
if errorlevel 1 goto :error

echo [3/4] Running tests...
dotnet test tests\UhkKeymapAutochanger.Tests\UhkKeymapAutochanger.Tests.csproj -c Release
if errorlevel 1 goto :error

echo [4/4] Publishing single-file EXE...
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 goto :error

if not exist "%OUTPUT_EXE%" (
  echo ERROR: Published exe not found: "%OUTPUT_EXE%"
  goto :error
)

if not exist "%DIST_DIR%" mkdir "%DIST_DIR%"
copy /Y "%OUTPUT_EXE%" "%DIST_DIR%\UhkKeymapAutochanger.exe" >nul
if errorlevel 1 goto :error

echo.
echo Build completed successfully.
echo Published EXE:
echo   %CD%\%OUTPUT_EXE%
echo Copied EXE:
echo   %CD%\%DIST_DIR%\UhkKeymapAutochanger.exe

popd >nul
exit /b 0

:error
echo.
echo Build failed.
popd >nul
exit /b 1
