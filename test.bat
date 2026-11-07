@echo off
setlocal

rem Builds and runs the battery filter tests. No test framework is involved:
rem FilterTests.cs has its own Main and returns a non-zero exit code on failure.

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Could not find csc.exe. Install .NET Framework 4.x or build with Visual Studio.
    exit /b 1
)

echo Building FilterTests.exe ...
"%CSC%" /nologo /target:exe /platform:anycpu /out:FilterTests.exe ^
    /r:System.dll ^
    src\HidNative.cs src\Filter.cs src\Protocols.cs test\FilterTests.cs

if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

FilterTests.exe
set RESULT=%ERRORLEVEL%
del /q FilterTests.exe 2>nul
if not "%RESULT%"=="0" (
    echo Tests failed.
    exit /b 1
)

echo All tests passed.
endlocal
