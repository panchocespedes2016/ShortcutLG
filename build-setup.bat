@echo off
setlocal
cd /d "%~dp0"

call build.bat build-auto
if errorlevel 1 (
    echo.
    echo No se pudo compilar ShortcutLG.exe.
    if "%~1"=="" pause
    exit /b 1
)

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo No se encontro el compilador de .NET Framework 4.8.
    if "%~1"=="" pause
    exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /out:ShortcutLG-Setup.exe ^
    /win32icon:app.ico /win32manifest:app.manifest ^
    /resource:ShortcutLG.exe,ShortcutLG.exe ^
    /resource:LEEME.txt,LEEME.txt ^
    /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    installer\Program.cs installer\AppPaths.cs installer\Shortcut.cs ^
    installer\InstallOperations.cs installer\SetupForm.cs installer\InstallerInfo.cs

if errorlevel 1 (
    echo.
    echo La compilacion del instalador fallo.
    if "%~1"=="" pause
    exit /b 1
)

echo.
echo Instalador compilado correctamente:
echo   %CD%\ShortcutLG-Setup.exe
if "%~1"=="" pause
endlocal
