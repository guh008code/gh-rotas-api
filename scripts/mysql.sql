-- MySQL 8+: selecione o banco de dados antes de executar.
CREATE TABLE IF NOT EXISTS Users (
    Id char(36) CHARACTER SET ascii NOT NULL PRIMARY KEY,
    Name varchar(150) NOT NULL,
    Email varchar(254) NOT NULL,
    NormalizedEmail varchar(254) COLLATE utf8mb4_0900_bin NOT NULL,
    Phone varchar(25) NOT NULL,
    PasswordHash varchar(512) NOT NULL,
    BirthDate date NOT NULL,
    CreatedAtUtc datetime(6) NOT NULL,
    CONSTRAINT UQ_Users_NormalizedEmail UNIQUE (NormalizedEmail)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
