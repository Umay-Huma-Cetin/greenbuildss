@echo off
rem EcoBuildss: testleri calistirir, programi tek dosya olarak yayinlar ve kurulum dosyasini (setup) uretir.
rem Gerekenler: .NET 8 SDK, internet (ilk seferde .NET calisma zamani paketleri indirilir), Inno Setup 6.3+ (setup icin).
setlocal
cd /d "%~dp0.."

echo [1/3] Testler calisiyor...
dotnet run --project tests\EcoBuildss.Tests -c Release
if errorlevel 1 goto hata

echo [2/3] Program yayinlaniyor (kendi .NET'iyle, tek exe)...
if exist kurulum\cikti rmdir /s /q kurulum\cikti
dotnet publish src\EcoBuildss.App\EcoBuildss.App.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:RestoreSources=https://api.nuget.org/v3/index.json -o kurulum\cikti
if errorlevel 1 goto hata
if not exist kurulum\cikti\veri\referans\BUTUNLUK.sha256 (
  echo Referans veri yayina kopyalanmadi.
  goto hata
)

echo [3/3] Kurulum dosyasi uretiliyor...
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
  echo Inno Setup 6 bulunamadi: https://jrsoftware.org/isdl.php adresinden kurun.
  echo Program yine de kurulum\cikti klasorunde hazir; EcoBuildss.exe dogrudan calisir.
  goto son
)
"%ISCC%" kurulum\EcoBuildss.iss
if errorlevel 1 goto hata
echo.
echo Kurulum dosyasi: kurulum\kurulum_cikti\EcoBuildss-Kurulum-3.0.0.exe
goto son

:hata
echo.
echo HATA: islem durduruldu.
exit /b 1

:son
endlocal
