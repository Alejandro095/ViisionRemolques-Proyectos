@echo off

:: ===================================================================
:: Script para Iniciar el Instalador de PowerShell con Permisos
:: ===================================================================

:: 1. Verifica si ya tiene permisos de administrador.
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"

:: 2. Si no los tiene (el 'errorlevel' no es 0), se reinicia pidiendo permisos.
if '%errorlevel%' NEQ '0' (
    echo Solicitando permisos de administrador...
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~s0' -Verb RunAs"
    exit /b
)

:: 3. Si llega aqui, ya tiene permisos. Ejecuta el script de PowerShell.
echo.
echo Iniciando el instalador del servicio (Ejecutando como Administrador)...
powershell.exe -ExecutionPolicy Bypass -File "%~dp0install.ps1"

echo.
echo Proceso finalizado.