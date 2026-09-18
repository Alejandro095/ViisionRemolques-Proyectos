# ===================================================================
# Script para Desinstalar el servicio go2rtc con NSSM
# ===================================================================

# --- Bloque de Auto-Elevacion de Permisos ---
# 1. Obtenemos la identidad actual del usuario
$currentUser = New-Object Security.Principal.WindowsPrincipal $([Security.Principal.WindowsIdentity]::GetCurrent())

# 2. Verificamos si el rol es "Administrador"
if (-Not $currentUser.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    # 3. Si no es admin, reiniciamos el script pidiendo elevacion
    $arguments = "& '" + $myinvocation.mycommand.definition + "'"
    Start-Process powershell -Verb RunAs -ArgumentList $arguments
    exit
}
# --- Fin del Bloque de Auto-Elevacion ---


# --- CONFIGURACION ---
# Asegúrate de que esta variable coincida con la del script de instalacion
$serviceName = "ViisionPatrullia Go2RTC"
$nssmExeName = "nssm.exe"       # Nombre del ejecutable de NSSM
# ---------------------


# --- Logica del Script ---
Write-Host "Permisos de Administrador verificados." -ForegroundColor Cyan

# El desinstalador debe ejecutarse desde una carpeta que contenga nssm.exe
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$nssmPath = Join-Path $scriptDir $nssmExeName

# --- Proceso de Desinstalacion ---
Write-Host "Iniciando desinstalacion del servicio '$serviceName'..." -ForegroundColor Yellow

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

# Paso 1: Intentar detener el servicio si existe y esta corriendo
if ($service -and $service.Status -eq 'Running') {
    Write-Host "Paso 1/2: El servicio se esta ejecutando. Intentando detenerlo..."
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 4 # Damos tiempo al proceso para que termine
} else {
     Write-Host "Paso 1/2: El servicio no se esta ejecutando o no se encontro. Omitiendo detencion." -ForegroundColor Green
}

# Paso 2: Intentar eliminar el registro del servicio si existe
if ($service) {
    if (Test-Path $nssmPath) {
        Write-Host "Paso 2/2: Eliminando el registro del servicio con NSSM..."
        & $nssmPath remove $serviceName confirm | Out-Null
        Start-Sleep -Seconds 2
    } else {
        Write-Warning "No se encontro '$nssmExeName' junto al script. Intentando eliminar el servicio con 'sc.exe'."
        sc.exe delete $serviceName | Out-Null
    }
} else {
     Write-Host "Paso 2/2: El registro del servicio no se encontro. Omitiendo eliminacion." -ForegroundColor Green
}

Write-Host "¡Proceso de desinstalacion completado!" -ForegroundColor Green

# Mantiene la ventana abierta por unos segundos antes de cerrar.
Write-Host "Esta ventana se cerrara en 10 segundos..." -ForegroundColor Yellow
Start-Sleep -Seconds 10