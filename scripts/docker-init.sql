-- Inicialização idempotente do ambiente Docker local.
USE master;
GO
IF DB_ID(N'GhRotas') IS NULL
    CREATE DATABASE [GhRotas];
GO
USE [GhRotas];
GO
:r /scripts/sqlserver.sql
GO
USE master;
GO
IF SUSER_ID(N'gh_rotas') IS NULL
    CREATE LOGIN [gh_rotas] WITH PASSWORD = '$(APP_DB_PASSWORD)', CHECK_POLICY = ON;
GO
USE [GhRotas];
GO
IF DATABASE_PRINCIPAL_ID(N'gh_rotas') IS NULL
    CREATE USER [gh_rotas] FOR LOGIN [gh_rotas];
GO
GRANT SELECT, INSERT ON OBJECT::dbo.Users TO [gh_rotas];
GO
