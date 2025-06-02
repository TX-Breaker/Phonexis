@echo off
echo Building YouTube Link Chooser C# application...

REM Check if .NET 6.0 SDK is installed
dotnet --version > nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo Error: .NET SDK not found. Please install .NET 6.0 SDK or later.
    exit /b 1
)

REM Restore NuGet packages
echo Restoring NuGet packages...
dotnet restore

REM Build the application
echo Building application...
dotnet build -c Release

REM Check if build was successful
if %ERRORLEVEL% neq 0 (
    echo Error: Build failed.
    exit /b 1
)

echo Build completed successfully.
echo.
echo You can run the application using:
echo dotnet run -c Release

REM Ask if user wants to run the application
set /p run_app=Do you want to run the application now? (Y/N): 

if /i "%run_app%"=="Y" (
    echo Starting application...
    dotnet run -c Release
)

exit /b 0