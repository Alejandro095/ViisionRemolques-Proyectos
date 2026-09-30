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







-----------------------------------------------------------------------------------
---- EVENTOS
-----------------------------------------------------------------------------------

CREATE TABLE Eventos (
    IdInterno                           BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    IdExterno                           BIGINT NULL,
    CamaraIP                            NVARCHAR(45) NULL,
    CamaraMAC                           NVARCHAR(17) NULL,
    VCA                                 NVARCHAR(100) NULL,
    Evento                              NVARCHAR(100) NULL,
    Prioridad                           INT NOT NULL DEFAULT 5,

    -- Auditoria
    FechaEvento                         DATETIME NOT NULL DEFAULT GETDATE(),
    Payload                             NVARCHAR(MAX) NULL
);
GO

CREATE TABLE EventoDetallesSmart (
    IdInterno                           BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    EventoIdInterno                     BIGINT NOT NULL,
    RegionId                            INT NULL,
    ObjetivoDetectadoTipo               NVARCHAR(100) NULL,
    RegionCoordenadas                   NVARCHAR(MAX) NULL,

    CONSTRAINT FK_EventoDetallesSmart_Eventos
        FOREIGN KEY (EventoIdInterno)
        REFERENCES Eventos(IdInterno)
        ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_EventoDetallesSmart_EventoIdInterno
ON EventoDetallesSmart (EventoIdInterno);
GO


CREATE TABLE EventoDetallesAlarmaRecuentoPersonas (
    IdInterno                           BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    EventoIdInterno                     BIGINT NOT NULL,

    ObjetivoDetectadoTipo               NVARCHAR(100) NULL,
    Algoritmo                           NVARCHAR(100) NULL,
    RegionId                            INT NULL,
    RegionCoordenadas                   NVARCHAR(MAX) NULL,
    ValorCausaEvento                    INT NULL,
    OperadorCausaEvento                 NVARCHAR(100) NULL,
    CantidadPersonas                    INT NULL,
    NivelDensidad                       INT NULL,
    NombreNivelDensidad                 NVARCHAR(100) NULL,
    DireccionCambioDensidad             NVARCHAR(100) NULL

    CONSTRAINT FK_EventoDetallesAlarmaRecuentoPersonas_Eventos
        FOREIGN KEY (EventoIdInterno)
        REFERENCES Eventos(IdInterno)
        ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_EventoDetallesSmart_EventoIdInterno
ON EventoDetallesAlarmaRecuentoPersonas (EventoIdInterno);
GO


CREATE TABLE EventoDetallesANPR (
    IdInterno                           BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    EventoIdInterno                     BIGINT NOT NULL,

    Matricula                           NVARCHAR(100) NULL,
    VehiculoDosRuedas                   NVARCHAR(100) NULL,
    VehiculoTresRuedas                  NVARCHAR(100) NULL,
    VehiculoTipo                        NVARCHAR(100) NULL,
    VehiculoColor                       NVARCHAR(100) NULL,
    Direccion                           NVARCHAR(100) NULL,
    NumeroCarril                        INT NULL,
    NombreLista                         NVARCHAR(100) NULL,
    Radar                               BIT DEFAULT 0 NOT NULL,
    Velocidad                           INT NULL,

    CONSTRAINT FK_EventoDetallesANPR_Eventos
        FOREIGN KEY (EventoIdInterno)
        REFERENCES Eventos(IdInterno)
        ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_EventoDetallesANPR_EventoIdInterno
ON EventoDetallesANPR (EventoIdInterno);
GO

CREATE TABLE EventoDetallesArmadoPistaPersona (
    IdInterno                           BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    EventoIdInterno                     BIGINT NOT NULL,

    Edad                                INT NULL,
    GrupoEdad                           NVARCHAR(100) NULL,
    Genero                              NVARCHAR(100) NULL,
    Lentes                              NVARCHAR(100) NULL,
    Mascara                             NVARCHAR(100) NULL,
    ExpresionFacial                     NVARCHAR(100) NULL,
    Sombrero                            NVARCHAR(100) NULL,
    DeteccionFacial                     BIT DEFAULT 0 NOT NULL,
    DeteccionFacialId                   NVARCHAR(MAX) NULL,

    CONSTRAINT FK_EventoDetallesArmadoPistaPersona_Eventos
        FOREIGN KEY (EventoIdInterno)
        REFERENCES Eventos(IdInterno)
        ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_EventoDetallesArmadoPistaPersona_EventoIdInterno
ON EventoDetallesArmadoPistaPersona (EventoIdInterno);
GO



--TABLA Imagenes ---------------------------------------------------------//
CREATE TABLE Imagenes (
    IdInterno           BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    EventoIdInterno     BIGINT,
    Path                NVARCHAR(MAX) NOT NULL,
    Sincronizado        BIT NOT NULL DEFAULT 0,
    FechaCreacion       DATETIME2(2) NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_Imagenes_Eventos
        FOREIGN KEY (EventoIdInterno)
        REFERENCES Eventos(IdInterno)
        ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_Imagenes_EventoIdInterno
ON Imagenes (EventoIdInterno);
GO