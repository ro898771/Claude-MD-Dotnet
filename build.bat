@echo off
rem Builds Claude-MD into one self-contained exe (no Visual Studio, no .NET install needed to run it).
rem Output: publish\Claude-MD.exe (icon: icon.ico)   Double-click to build; "build.bat run" also starts the app afterwards.
rem If the old exe is still running, it is renamed (Windows allows renaming a running exe) so the new one can take its place;
rem the running copy keeps working, and the next start uses the new build. Earlier renamed copies are deleted when they are
rem no longer running, so several can exist at once without blocking the build.
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo The .NET SDK was not found. Install the .NET 9 SDK, then run this again.
    pause
    exit /b 1
)

if not exist "%~dp0icon.ico" (
    echo icon.ico was not found next to build.bat - the exe would get the default icon.
    pause
    exit /b 1
)

rem Clean up renamed copies of earlier builds that are not running any more (a running one just stays).
for %%f in ("publish\Claude-MD.old*.exe") do del /f /q "%%f" >nul 2>nul

rem Move the current exe out of the way under a name that is not taken (the old fixed name could still be in use).
rem A just-built exe can be held for a few seconds (antivirus scan, Explorer preview), so retry before giving up.
set "OLDNAME="
set /a TRIES=0
:renameexe
if not exist "publish\Claude-MD.exe" goto renamed
if not defined OLDNAME set "OLDNAME=Claude-MD.old%RANDOM%%RANDOM%.exe"
ren "publish\Claude-MD.exe" "%OLDNAME%" >nul 2>nul
if not exist "publish\Claude-MD.exe" goto renamed
set /a TRIES+=1
if %TRIES% geq 15 goto locked
if %TRIES%==1 echo Waiting for publish\Claude-MD.exe to be released...
timeout /t 1 /nobreak >nul
goto renameexe

:locked
echo.
echo publish\Claude-MD.exe is still locked after 15 seconds and could not be renamed.
tasklist /fi "imagename eq Claude-MD*" 2>nul | find /i "Claude-MD" >nul && echo Claude-MD seems to be running - close it, then run this again.
tasklist /fi "imagename eq Claude-MD*" 2>nul | find /i "Claude-MD" >nul || echo It is not running; something else (antivirus, a file preview) holds it. Wait a moment and run this again.
pause
exit /b 1

:renamed

echo Building... (the first time takes a minute or two)
dotnet publish -c Release -r win-x64 --self-contained true ^
    -p:ApplicationIcon=icon.ico ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
    -o publish -nologo -v q
if errorlevel 1 (
    echo.
    echo BUILD FAILED.
    if defined OLDNAME if exist "publish\%OLDNAME%" if not exist "publish\Claude-MD.exe" ren "publish\%OLDNAME%" "Claude-MD.exe" >nul 2>nul
    pause
    exit /b 1
)

echo.
echo Done: %~dp0publish\Claude-MD.exe
for /f "delims=" %%v in ('powershell -NoProfile -Command "(Get-Item 'publish\Claude-MD.exe').VersionInfo.FileVersion" 2^>nul') do echo Version: %%v   (set in Claude-MD.csproj)
echo (If Claude-MD was open, close it and start it again to use the new build.)
if /i "%~1"=="run" start "" "%~dp0publish\Claude-MD.exe"
if /i not "%~1"=="run" pause
exit /b 0

