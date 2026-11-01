@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo No se encontro el compilador de .NET Framework 4.8.
    echo Instala .NET Framework 4.8 y vuelve a intentarlo.
    if "%~1"=="" pause
    exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /out:ShortcutLG.exe ^
    /win32icon:app.ico /win32manifest:app.manifest ^
    /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll ^
    /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Management.dll ^
    Hotkey.cs AppConfig.cs LgTvClient.cs MonitorInfo.cs RemoteForm.cs ^
    TvSettingsForm.cs HotkeyForm.cs Program.cs TrayContext.cs AssemblyInfo.cs

if errorlevel 1 (
    echo.
    echo La compilacion fallo.
    if "%~1"=="" pause
    exit /b 1
)

echo.
echo Compilado correctamente:
echo   %CD%\ShortcutLG.exe
if "%~1"=="" pause
endlocal
