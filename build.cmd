@echo off
rem Builds "dist\World Map Converter.exe" and "dist\World Map Generator.exe" with the C# compiler that ships
rem with Windows (.NET Framework 4). / Compile le convertisseur et le generateur avec le compilateur C# de Windows.
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo .NET Framework 4 compiler not found: %CSC%
    exit /b 1
)
if not exist dist mkdir dist
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 ^
    /win32icon:src\world-maps.ico /win32manifest:src\app.manifest ^
    "/out:dist\World Map Converter.exe" /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    src\*.cs
if errorlevel 1 exit /b 1
echo Built: dist\World Map Converter.exe
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 ^
    /win32icon:src\world-maps.ico /win32manifest:src\app.manifest ^
    "/out:dist\World Map Generator.exe" /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    generator\*.cs src\Pe.cs src\ExeInfo.cs
if errorlevel 1 exit /b 1
echo Built: dist\World Map Generator.exe
