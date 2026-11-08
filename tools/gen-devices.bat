@echo off
rem Regenerates docs/DEVICES.md from src/DeviceTable.cs.
rem Run this after adding or renaming a model, and commit the result.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find csc.exe
  exit /b 1
)

"%CSC%" /nologo /target:exe /platform:anycpu /out:tools\DevicesDoc.exe ^
  /r:System.dll ^
  src\HidNative.cs src\Protocols.cs src\ProtocolsVendor.cs src\DeviceTable.cs ^
  tools\DevicesDoc.cs
if errorlevel 1 exit /b 1

tools\DevicesDoc.exe docs\DEVICES.md
set RC=%errorlevel%
del /q tools\DevicesDoc.exe
exit /b %RC%
