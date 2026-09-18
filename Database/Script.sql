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

--CAMARAS-------------------------------------------------------------------------------//
CREATE TABLE Camaras (
    IdInterno           BIGINT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    Nombre              NVARCHAR(100),
    DigestDominio       NVARCHAR(500),
    DigestUsuario       NVARCHAR(100),
    DigestPassword      NVARCHAR(100),
    PTZ                 BIT DEFAULT 0,
    TwoWayAudio         BIT DEFAULT 0
) GO

--DETECCIONES---------------------------------------------------------------------------//
CREATE TABLE Detecciones
(
    IdInterno    BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    Humano       BIT DEFAULT 0,
    Vehiculo     BIT DEFAULT 0,
    Datos        NVARCHAR(100),
    Fecha        DATETIME2(2) NOT NULL DEFAULT GETDATE(),
    Sincronizado BIT DEFAULT 0
) GO

--BLACKLIST ROSTROS---------------------------------------------------------------------//
CREATE TABLE BlacklistRostros
(
    IdInterno       BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    IdExterno       BIGINT NOT NULL,
    Nombre          NVARCHAR(100),
    Sexo            CHAR DEFAULT 'M',
    Fotografia      NVARCHAR(100),
    FechaCreacion   DATETIME2(2) NOT NULL DEFAULT GETDATE(),
) GO

--BLACKLIST VEHICULOS-------------------------------------------------------------------//
CREATE TABLE BlacklistVehiculos
(
    IdInterno       BIGINT IDENTITY (1,1) PRIMARY KEY CLUSTERED,
    IdExterno       BIGINT NOT NULL,
    Placa           NVARCHAR(7),
    NIV             NVARCHAR(17),
    FechaCreacion   DATETIME2(2) NOT NULL DEFAULT GETDATE(),
) GO


