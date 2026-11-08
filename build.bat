@echo off
setlocal

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Could not find csc.exe. Install .NET Framework 4.x or build with Visual Studio.
    exit /b 1
)

echo Building Volt.exe ...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
    /win32icon:app.ico /out:Volt.exe ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    src\HidNative.cs src\Protocols.cs src\ProtocolsVendor.cs src\DeviceTable.cs ^
    src\Filter.cs src\Scanner.cs src\Theme.cs src\TrayIcon.cs src\MainForm.cs ^
    src\Program.cs

if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

echo Built Volt.exe
endlocal
