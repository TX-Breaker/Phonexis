@echo off
REM Builds dist\Phonexis.exe (publish.bat), then the installer dist\Phonexis-Setup-<version>.exe.
REM Needs Inno Setup 6 (https://jrsoftware.org/isdl.php or: winget install JRSoftware.InnoSetup).

cd /d "%~dp0.."

call build\publish.bat
if %errorlevel% neq 0 exit /b %errorlevel%

set "ISCC="
for %%P in ("%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" "%ProgramFiles%\Inno Setup 6\ISCC.exe" "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe") do (
  if exist %%P set "ISCC=%%~P"
)
if not defined ISCC (
  where ISCC.exe >nul 2>nul && set "ISCC=ISCC.exe"
)
if not defined ISCC (
  echo Inno Setup 6 not found. Install it, then run this script again.
  exit /b 1
)

"%ISCC%" /Q build\installer\Phonexis.iss
if %errorlevel% neq 0 (
  echo INSTALLER FAILED
  exit /b %errorlevel%
)

echo.
echo Done: %cd%\dist
dir /b dist\*.exe
