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

    PlataformaVCA                       TINYINT NOT NULL,

    -- Digest
    Digest_Usuario                      NVARCHAR(100) NULL,
    Digest_Contrasena                   NVARCHAR(256) NULL,

    -- Funcionalidades
    Soporta_AudioBidireccional          BIT NOT NULL DEFAULT 0,

    Soporta_PtzMovimiento               BIT NOT NULL DEFAULT 0,
    Soporta_PtzZoom                     BIT NOT NULL DEFAULT 0,
    Soporta_PtzEnfoque                  BIT NOT NULL DEFAULT 0,
    Soporta_PtzIris                     BIT NOT NULL DEFAULT 0,
    Soporta_PtzLuz                      BIT NOT NULL DEFAULT 0,
    Soporta_PtzEscobilla                BIT NOT NULL DEFAULT 0,
    Soporta_PtzEnfoqueAuxiliar          BIT NOT NULL DEFAULT 0,
    Soporta_PtzInicializacionObjetivo   BIT NOT NULL DEFAULT 0,
    Soporta_PtzCalibracionZoom          BIT NOT NULL DEFAULT 0,
    
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
    PlataformaVCA,
    Digest_Usuario,
    Digest_Contrasena,
    Soporta_AudioBidireccional,
    Soporta_PtzMovimiento,
    Soporta_PtzZoom,
    Soporta_PtzEnfoque,
    Soporta_PtzIris,
    Soporta_PtzEscobilla,
    Soporta_PtzEnfoqueAuxiliar,
    Soporta_PtzInicializacionObjetivo,
    Soporta_PtzCalibracionZoom
)
VALUES
    (
        N'Cámara PTZ',
        N'DS-2DF8C842IXG1-ELWY',
        1,
        N'192.168.50.25',
        N'camara_1',
        2,
        N'admin',
        N'Viinsoft+1',
        0,
        1,
        1,
        1,
        0,
        1,
        0,
        0,
        0
    ),
    (
        N'Cámara Bala',
        N'DS-2CD3687G3T-LIZSU',
        1,
        N'192.168.50.26',
        N'camara_2',
        3,
        N'admin',
        N'Viinsoft+1',
        1,
        0,
        1,
        1,
        0,
        0,
        1,
        1,
        0
    ),
    (
        N'Cámara Ojo de Pez',
        N'DS-2CD6365G1-IVS',
        1,
        N'192.168.50.27',
        N'camara_3',
        3,
        N'admin',
        N'Viinsoft+1',
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0
    ),
    (
        N'Cámara Radar',
        N'iDS-TCM403-GIR',
        1,
        N'192.168.100.28',
        N'camara_4',
        1,
        N'admin',
        N'Viinsoft+1',
        0,
        1,
        1,
        1,
        1,
        0,
        1,
        1,
        1
    ),
    (
        N'Cámara 180 grados',
        N'DS-2CD3T87G3P-LIHSUY/SL',
        1,
        N'192.168.50.29',
        N'camara_5',
        2,
        N'admin',
        N'Viinsoft+1',
        1,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0
    );
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

CREATE NONCLUSTERED INDEX IX_EventoDetallesAlarmaRecuentoPersonas_EventoIdInterno
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

CREATE TABLE EventoDetallesCapturaFacial(
    IdInterno                           BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    EventoIdInterno                     BIGINT NOT NULL,

    RegionCoordenadas                  NVARCHAR(MAX) NULL,

    CONSTRAINT FK_EventoDetallesCapturaFacial_Eventos
        FOREIGN KEY (EventoIdInterno)
        REFERENCES Eventos(IdInterno)
        ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_EventoDetallesCapturaFacial_EventoIdInterno
ON EventoDetallesCapturaFacial(EventoIdInterno);
GO

CREATE TABLE EventoDetallesDeteccionTipoMultiobjetivo(
    IdInterno                           BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    EventoIdInterno                     BIGINT NOT NULL,

    Tipo                                NVARCHAR(100) NULL,
    Puntuacion                          DECIMAL(10, 2) NULL,
    -- Tipo:Humano
    HumanoEdad                          INT NULL,
    HumanoExpresionFacial               NVARCHAR(100) NULL,
    HumanoColorChaqueta                 NVARCHAR(100) NULL,
    HumanoLentes                        NVARCHAR(100) NULL,
    HumanoGenero                        NVARCHAR(100) NULL,
    HumanoBolso                         NVARCHAR(100) NULL,
    HumanoSombrero                      NVARCHAR(100) NULL,
    HumanoTipoChaqueta                  NVARCHAR(100) NULL,
    HumanoMascarilla                    NVARCHAR(100) NULL,
    HumanoEstiloCabello                 NVARCHAR(100) NULL,
    HumanoGrupoEdad                     NVARCHAR(100) NULL,
    HumanoObjetos                       NVARCHAR(100) NULL,
    HumanoColorPantalon                 NVARCHAR(100) NULL,
    HumanoTipoPantalon                  NVARCHAR(100) NULL,
    HumanoDireccion                     NVARCHAR(100) NULL,
    HumanoDeteccionFacial               NVARCHAR(100) NULL,
    HumanoDeteccionFacialId             NVARCHAR(MAX) NULL,
    -- Tipo:Vehiculo
    VehiculoMatricula                   NVARCHAR(100) NULL,
    VehiculoTipo                        NVARCHAR(100) NULL,
    VehiculoColor                       NVARCHAR(100) NULL,
    VehiculoLogo                        NVARCHAR(100) NULL

    CONSTRAINT FK_EventoDetallesDeteccionTipoMultiobjetivo_Eventos
        FOREIGN KEY (EventoIdInterno)
        REFERENCES Eventos(IdInterno)
        ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_EventoDetallesDeteccionTipoMultiobjetivo_EventoIdInterno
ON EventoDetallesDeteccionTipoMultiobjetivo(EventoIdInterno);
GO

CREATE TABLE EventoDetallesRecuentoPersonas(
    IdInterno                           BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    EventoIdInterno                     BIGINT NOT NULL,

    TodasRegionesEntradas               INT NULL,
    TodasRegionesSalidas                INT NULL,
    TodasRegionesTranseuntes            INT NULL,
    TodasRegionesDuplicados             INT NULL,

    RegionId                            INT NULL,
    RegionEntradas                      INT NULL,
    RegionSalidas                       INT NULL,
    RegionTranseuntes                   INT NULL,

    CONSTRAINT FK_EventoDetallesRecuentoPersonas_Eventos
        FOREIGN KEY (EventoIdInterno)
        REFERENCES Eventos(IdInterno)
        ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_EventoDetallesRecuentoPersonas_EventoIdInterno
ON EventoDetallesRecuentoPersonas(EventoIdInterno);
GO



CREATE TABLE EventoDetallesTraficoRodado(
    IdInterno                           BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    EventoIdInterno                     BIGINT NOT NULL,

    VehiculosTotal                      INT NULL,
    VehiculosFlujoAscendente            INT NULL,
    VehiculosFlujoDescendente           INT NULL,

    MotocicletasTotal                   INT NULL,
    MotocicletasFlujoAscendente         INT NULL,
    MotocicletasFlujoDescendente        INT NULL,

    CONSTRAINT FK_EventoDetallesTraficoRodado_Eventos
        FOREIGN KEY (EventoIdInterno)
        REFERENCES Eventos(IdInterno)
        ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_EventoDetallesTraficoRodado_EventoIdInterno
ON EventoDetallesTraficoRodado(EventoIdInterno);
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