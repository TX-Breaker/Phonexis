@echo off
echo ===================================================
echo Phonexis - Script di Pubblicazione
echo ===================================================
echo.

echo Pulizia delle directory di build precedenti...
if exist "bin\Release" rmdir /s /q "bin\Release"
if exist "publish" rmdir /s /q "publish"
mkdir publish

echo.
echo Compilazione e pubblicazione dell'applicazione...
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishReadyToRun=true /p:PublishTrimmed=true

echo.
echo Copia dei file nella directory di pubblicazione...
xcopy "bin\Release\net6.0-windows\win-x64\publish\*.*" "publish\" /E /Y
copy "README.md" "publish\"
copy "DISTRIBUTION.md" "publish\"

echo.
echo Creazione del pacchetto ZIP...
powershell Compress-Archive -Path "publish\*" -DestinationPath "Phonexis-Release.zip" -Force

echo.
echo Pubblicazione completata con successo!
echo Il pacchetto di distribuzione è disponibile in: Phonexis-Release.zip
echo.
echo ===================================================
pause