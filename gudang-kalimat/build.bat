@echo off
rem Build ulang GudangKalimat.exe memakai compiler C# bawaan Windows (tidak perlu memasang apa pun).
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Compiler .NET Framework 4 tidak ditemukan.
  pause
  exit /b 1
)
if not exist dist mkdir dist
"%CSC%" /nologo /target:winexe /optimize+ /out:dist\GudangKalimat.exe /win32icon:src\ikon.ico ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.Xml.dll /r:System.Core.dll ^
  src\*.cs
if errorlevel 1 (
  echo Build gagal.
) else (
  echo Selesai: dist\GudangKalimat.exe
)
pause
