@echo off
REM Builds the single-file, self-contained Phonexis executable.
REM Output: dist\Phonexis.exe (no .NET installation required on the target PC;
REM the in-app preview uses Microsoft Edge WebView2, preinstalled on Windows 10/11).

cd /d "%~dp0.."

if exist dist rmdir /s /q dist

dotnet publish src\Phonexis\Phonexis.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:EnableCompressionInSingleFile=true ^
  /p:DebugType=None ^
  /p:DebugSymbols=false ^
  -o dist

if %errorlevel% neq 0 (
  echo BUILD FAILED
  exit /b %errorlevel%
)

REM Package documentation files (WebView2 *.xml) are not needed next to the exe.
del /q dist\*.xml 2>nul

echo.
echo Done: %cd%\dist\Phonexis.exe
