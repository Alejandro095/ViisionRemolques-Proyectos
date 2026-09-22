USE master;

IF EXISTS (SELECT name FROM sys.databases WHERE name = 'ViisionRemolques')
BEGIN
    ALTER DATABASE	ViisionRemolques SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE	ViisionRemolques
END
GO

CREATE DATABASE ViisionRemolques;

IF EXISTS (SELECT name FROM sys.databases WHERE name = 'Hangfire')
BEGIN
    ALTER DATABASE	Hangfire SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE	Hangfire;
END
GO

CREATE DATABASE Hangfire;

USE ViisionRemolques;

--CAMARAS-------------------------------------------------------------------------------//
CREATE TABLE Camaras (
    IdInterno                           BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    Nombre                              NVARCHAR(100) NOT NULL,
    Modelo                              NVARCHAR(100) NOT NULL,
    Activo                              BIT NOT NULL DEFAULT 1,
    IP                                  NVARCHAR(45)  NULL,
    Go2Rtc                              NVARCHAR(100) NOT NULL,

    -- Digest
    Digest_Usuario                      NVARCHAR(100) NULL,
    Digest_Contrasena                   NVARCHAR(256) NULL,

    Soporta_PTZ                         BIT NOT NULL DEFAULT 0,
    SoportaAudioBidireccional           BIT NOT NULL DEFAULT 0,

    -- Eventos Smart (Analiticas de Video)
    EventoSmart_DeteccionIntrusiones    BIT NOT NULL DEFAULT 0,
    EventoSmart_DeteccionCruceLinea     BIT NOT NULL DEFAULT 0,
    EventoSmart_DeteccionEntradaArea    BIT NOT NULL DEFAULT 0,
    EventoSmart_DeteccionSalidaArea     BIT NOT NULL DEFAULT 0,
    EventoSmart_EventoCombinado         BIT NOT NULL DEFAULT 0,

    -- Auditoria
    FechaCreacion                       DATETIME NOT NULL DEFAULT GETDATE()
);
GO

INSERT INTO Camaras (
    Nombre,
    Modelo,
    Activo,
    IP,
    Go2Rtc,
    Digest_Usuario,
    Digest_Contrasena,
    Soporta_PTZ,
    SoportaAudioBidireccional,
    EventoSmart_DeteccionIntrusiones,
    EventoSmart_DeteccionCruceLinea,
    EventoSmart_DeteccionEntradaArea,
    EventoSmart_DeteccionSalidaArea,
    EventoSmart_EventoCombinado
)
VALUES
    (
        N'Camara PTZ',
        N'DS2DF8C842IXG1ELWY',
        1,
        N'192.168.50.25',
        N'camara_1',
        N'admin',
        N'Viinsoft+1',
        1,
        0,
        1,
        1,
        1,
        1,
        1
    ),
    (
        N'Camara Bala',
        N'DS2CD3687G3TLIZSU',
        1,
        N'192.168.50.26',
        N'camara_2',
        N'admin',
        N'Viinsoft+1',
        1,
        0,
        1,
        1,
        1,
        1,
        1
    ),
    (
        N'Camara 360',
        N'DS2CD6365G1IVS',
        1,
        N'192.168.50.27',
        N'camara_3',
        N'admin',
        N'Viinsoft+1',
        1,
        0,
        1,
        1,
        1,
        1,
        1
    ),
    (
        N'Camara Radar',
        N'iDSTCM403GIR',
        1,
        N'192.168.50.28',
        N'camara_4',
        N'admin',
        N'Viinsoft+1',
        1,
        0,
        1,
        1,
        1,
        1,
        1
    ),
    (
        N'Camara 180',
        N'DS2CD3T87G3PLISUYSL',
        1,
        N'192.168.50.29',
        N'camara_5',
        N'admin',
        N'Viinsoft+1',
        0,
        1,
        1,
        1,
        1,
        1,
        1
    );
GO

-- SP: Obtener todas las camaras activas
CREATE OR ALTER PROCEDURE sp_Camaras_ObtenerTodas
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        IdInterno,
        Nombre,
        Modelo,
        Activo,
        IP,
        Go2Rtc,
        Digest_Usuario,
        Digest_Contrasena,
        Soporta_PTZ,
        SoportaAudioBidireccional,
        EventoSmart_DeteccionIntrusiones,
        EventoSmart_DeteccionCruceLinea,
        EventoSmart_DeteccionEntradaArea,
        EventoSmart_DeteccionSalidaArea,
        EventoSmart_EventoCombinado
    FROM Camaras
    WHERE Activo = 1
    ORDER BY FechaCreacion DESC;
END;
GO

-- SP: Buscar una unica camara por IdInterno exacto
CREATE OR ALTER PROCEDURE sp_Camaras_BuscarPorIdInterno
    @IdInterno BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        IdInterno,
        Nombre,
        Modelo,
        Activo,
        IP,
        Go2Rtc,
        Digest_Usuario,
        Digest_Contrasena,
        Soporta_PTZ,
        SoportaAudioBidireccional,
        EventoSmart_DeteccionIntrusiones,
        EventoSmart_DeteccionCruceLinea,
        EventoSmart_DeteccionEntradaArea,
        EventoSmart_DeteccionSalidaArea,
        EventoSmart_EventoCombinado
    FROM Camaras
    WHERE IdInterno = @IdInterno
      AND Activo = 1;
END;
GO

--DETECCIONES---------------------------------------------------------------------------//
CREATE TABLE Detecciones
(
    IdInterno    BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    Humano       BIT DEFAULT 0,
    Vehiculo     BIT DEFAULT 0,
    Datos        NVARCHAR(100),
    Fecha        DATETIME2(2) NOT NULL DEFAULT GETDATE(),
    Sincronizado BIT DEFAULT 0
);
GO

--BLACKLIST ROSTROS---------------------------------------------------------------------//
CREATE TABLE BlacklistRostros
(
    IdInterno       BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    IdExterno       BIGINT NOT NULL,
    Nombre          NVARCHAR(100),
    Sexo            CHAR DEFAULT 'M',
    Fotografia      NVARCHAR(100),
    FechaCreacion   DATETIME2(2) NOT NULL DEFAULT GETDATE(),
);
GO

--BLACKLIST VEHICULOS-------------------------------------------------------------------//
CREATE TABLE BlacklistVehiculos
(
    IdInterno       BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    IdExterno       BIGINT NOT NULL,
    Placa           NVARCHAR(7),
    NIV             NVARCHAR(17),
    FechaCreacion   DATETIME2(2) NOT NULL DEFAULT GETDATE(),
);
GO

--ALERTAS PERIMETRALES------------------------------------------------------------------//
CREATE TABLE EventosPerimetrales (
    IdInterno                           BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    IdExterno                           BIGINT NULL,
    PId                                 VARCHAR(64),
    IPCamara                            VARCHAR(45),
    Evento                              VARCHAR(50) NOT NULL,
    ReglaId                             VARCHAR(64),
    TipoObjetivo                        VARCHAR(30) NULL,
    FechaEvento                         DATETIME2(2) NOT NULL DEFAULT GETDATE(),
    PathImagen                          NVARCHAR(500) NULL,
    Sincronizado                        BIT NOT NULL DEFAULT 0,
    FechaRegistro                       DATETIME NOT NULL DEFAULT GETDATE()
);
GO

CREATE INDEX IX_Eventos_PId ON EventosPerimetrales (PId);
CREATE INDEX IX_Eventos_PendientesSincronizar ON EventosPerimetrales (Sincronizado, FechaRegistro)
WHERE Sincronizado = 0;
GO

CREATE PROCEDURE dbo.sp_EventosPerimetrales_Insertar
    @PId               VARCHAR(64),
    @IPCamara          VARCHAR(45),
    @Evento            VARCHAR(50),
    @ReglaId           VARCHAR(64),
    @TipoObjetivo      VARCHAR(30) = NULL,
    @FechaEvento       DATETIME2(2) = NULL,
    @PathImagen        NVARCHAR(500) = NULL,
    @IdExterno         BIGINT = NULL,
    @Sincronizado      BIT = 0,
    @IdInternoGenerado BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.EventosPerimetrales (
        IdExterno,
        PId,
        IPCamara,
        Evento,
        ReglaId,
        TipoObjetivo,
        FechaEvento,
        PathImagen,
        Sincronizado,
        FechaRegistro
    )
    VALUES (
        @IdExterno,
        @PId,
        @IPCamara,
        @Evento,
        @ReglaId,
        NULLIF(@TipoObjetivo, ''),
        ISNULL(@FechaEvento, SYSDATETIME()),
        @PathImagen,
        @Sincronizado,
        GETDATE()
    );

    SET @IdInternoGenerado = SCOPE_IDENTITY();
END
GO

--ALERTAS DESCONOCIDAS LOG--------------------------------------------------------------//
CREATE TABLE dbo.AlarmasDesconocidasLog (
    IdInterno     BIGINT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    IPCamara      VARCHAR(45) NULL,                   -- IP origen de la cámara/petición
    ContentType   VARCHAR(100) NULL,                  -- application/xml, multipart/form-data, etc.
    Evento        VARCHAR(100) NULL,
    Body          NVARCHAR(MAX) NOT NULL,             -- Payload raw (XML/JSON/Texto) completo
    Motivo        NVARCHAR(250) NOT NULL,             -- Razón: "IP no registrada", "Evento desconocido", "XML inválido"
    Fecha         DATETIME2(2) NOT NULL DEFAULT GETDATE()
);
GO

-- Índices recomendados para búsquedas rápidas por fecha e IP
CREATE INDEX IX_AlarmasNoDetectadas_Fecha ON dbo.AlarmasDesconocidasLog(Fecha DESC);
CREATE INDEX IX_AlarmasNoDetectadas_IPCamara ON dbo.AlarmasDesconocidasLog(IPCamara);
GO

-- SP para registrar alarmas no detectadas
CREATE OR ALTER PROCEDURE dbo.sp_AlarmasDesconocidasLog_Insertar
    @IPCamara          VARCHAR(45) = NULL,
    @ContentType       VARCHAR(100) = NULL,
    @Evento            VARCHAR(100) = NULL,
    @Body              NVARCHAR(MAX),
    @Motivo            NVARCHAR(250)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.AlarmasDesconocidasLog (
        IPCamara,
        ContentType,
        Evento,
        Body,
        Motivo,
        Fecha
    )
    VALUES (
        @IPCamara,
        @ContentType,
        @Evento,
        @Body,
        @Motivo,
        SYSDATETIME()
    );
END;
GO