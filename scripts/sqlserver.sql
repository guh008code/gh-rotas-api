-- GH Rotas API - SQL Server
-- Selecione o banco da aplicação no SSMS antes de executar.

-- A estrutura atual utiliza somente dbo.Users.
-- Não remove dados nem altera tabelas existentes.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 50001, N'Selecione o banco da aplicação antes de executar este script.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Users
        (
            Id              char(36)      NOT NULL,
            Name            nvarchar(150) NOT NULL,
            Email           nvarchar(254) NOT NULL,
            NormalizedEmail nvarchar(254) COLLATE Latin1_General_100_BIN2 NOT NULL,
            Phone           nvarchar(25)  NOT NULL,
            PasswordHash    nvarchar(512) NOT NULL,
            BirthDate       date          NOT NULL,
            CreatedAtUtc    datetime2     NOT NULL,

            CONSTRAINT PK_Users PRIMARY KEY (Id),
            CONSTRAINT UQ_Users_NormalizedEmail UNIQUE (NormalizedEmail)
        );

        PRINT N'Tabela dbo.Users criada.';
    END
    ELSE
    BEGIN
        PRINT N'Tabela dbo.Users já existe. Nenhuma alteração foi aplicada.';
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
