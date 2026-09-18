# ===================================================================
# Script para Instalar el servicio go2rtc con NSSM
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
$serviceName = "ViisionPatrullia Go2RTC"
$appExe = "go2rtc.exe"          # Nombre del ejecutable de go2rtc
$nssmExeName = "nssm.exe"       # Nombre del ejecutable de NSSM
# ---------------------


# --- Logica del Script ---
Write-Host "Permisos de Administrador verificados." -ForegroundColor Cyan

# Directorio desde donde se ejecuta el script
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$nssmPath = Join-Path $scriptDir $nssmExeName
$exePath = Join-Path $scriptDir $appExe

# --- Limpieza de Instalacion Anterior ---
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Write-Host "Se encontro una instalacion anterior del servicio '$serviceName'. Limpiando..." -ForegroundColor Yellow
    if (Test-Path $nssmPath) {
        & $nssmPath stop $serviceName | Out-Null
        & $nssmPath remove $serviceName confirm | Out-Null
        Start-Sleep -Seconds 2 # Damos un momento para que el servicio se elimine
        Write-Host "Servicio anterior eliminado." -ForegroundColor Green
    } else {
        Write-Warning "No se encontro '$nssmExeName' para limpiar el servicio. Puede que necesites eliminarlo manualmente."
    }
}

# Valida que los archivos de origen existan en el directorio actual
if (-not (Test-Path $exePath)) {
    Write-Error "Error: No se encontro '$appExe' en la misma carpeta que el script."
    Read-Host "Presiona Enter para salir."
    exit 1
}
if (-not (Test-Path $nssmPath)) {
    Write-Error "Error: No se encontro '$nssmExeName'. Asegurate de que este en la misma carpeta que el script."
    Read-Host "Presiona Enter para salir."
    exit 1
}

Write-Host "Todo listo. Iniciando instalacion del servicio '$serviceName' desde '$scriptDir'..." -ForegroundColor Green

# 1. Instala el servicio
Write-Host "Paso 1/5: Creando el servicio..."
& $nssmPath install $serviceName "$exePath"

# 2. Fija el directorio de trabajo
Write-Host "Paso 2/5: Configurando el directorio de trabajo..."
& $nssmPath set $serviceName AppDirectory "$scriptDir"

# 3. Configura el servicio para que inicie automaticamente
Write-Host "Paso 3/5: Estableciendo el inicio automatico..."
& $nssmPath set $serviceName Start SERVICE_AUTO_START

# 4. Configura la recuperacion automatica en caso de error
Write-Host "Paso 4/5: Configurando la recuperacion automatica (reinicio tras 60s)..."
# sc.exe failure [NombreServicio] reset= [PeriodoSegs] actions= [Accion]/[TiempoMs]/[Accion]/[TiempoMs]/...
sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/60000

# 5. Inicia el servicio
Write-Host "Paso 5/5: Iniciando el servicio..."
net start $serviceName

Write-Host "¡Proceso completado! El servicio '$serviceName' ha sido instalado e iniciado." -ForegroundColor Green

# Mantiene la ventana abierta por unos segundos antes de cerrar.
Write-Host "Esta ventana se cerrara en 10 segundos..." -ForegroundColor Yellow
Start-Sleep -Seconds 10