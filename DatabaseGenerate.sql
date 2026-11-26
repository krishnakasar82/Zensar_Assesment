SET NOCOUNT ON;
GO

IF DB_ID(N'OT_Assessment_DB') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE OT_Assessment_DB;');
END;
GO

USE OT_Assessment_DB;
GO

IF OBJECT_ID(N'dbo.Players', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Players
    (
        AccountId uniqueidentifier NOT NULL,
        Username nvarchar(100) NOT NULL,
        UpdatedAt datetimeoffset(7) NOT NULL,
        CONSTRAINT PK_Players PRIMARY KEY CLUSTERED (AccountId)
    );
END;
GO

IF OBJECT_ID(N'dbo.CasinoWagers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CasinoWagers
    (
        WagerId uniqueidentifier NOT NULL,
        AccountId uniqueidentifier NOT NULL,
        GameName nvarchar(200) NOT NULL,
        Provider nvarchar(200) NOT NULL,
        Amount decimal(28, 17) NOT NULL,
        CreatedDateTime datetimeoffset(7) NOT NULL,
        CONSTRAINT PK_CasinoWagers PRIMARY KEY CLUSTERED (WagerId),
        CONSTRAINT FK_CasinoWagers_Players
            FOREIGN KEY (AccountId) REFERENCES dbo.Players (AccountId),
        CONSTRAINT CK_CasinoWagers_Amount CHECK (Amount > 0)
    );
END;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.CasinoWagers')
      AND name = N'Amount'
      AND (precision <> 28 OR scale <> 17)
)
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_CasinoWagers_Account_CreatedDateTime'
          AND object_id = OBJECT_ID(N'dbo.CasinoWagers')
    )
    BEGIN
        DROP INDEX IX_CasinoWagers_Account_CreatedDateTime
            ON dbo.CasinoWagers;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_CasinoWagers_Account_Amount'
          AND object_id = OBJECT_ID(N'dbo.CasinoWagers')
    )
    BEGIN
        DROP INDEX IX_CasinoWagers_Account_Amount
            ON dbo.CasinoWagers;
    END;

    ALTER TABLE dbo.CasinoWagers
        ALTER COLUMN Amount decimal(28, 17) NOT NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_CasinoWagers_Account_CreatedDateTime'
      AND object_id = OBJECT_ID(N'dbo.CasinoWagers')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_CasinoWagers_Account_CreatedDateTime
        ON dbo.CasinoWagers (AccountId, CreatedDateTime DESC, WagerId DESC)
        INCLUDE (GameName, Provider, Amount);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_CasinoWagers_Account_Amount'
      AND object_id = OBJECT_ID(N'dbo.CasinoWagers')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_CasinoWagers_Account_Amount
        ON dbo.CasinoWagers (AccountId)
        INCLUDE (Amount);
END;
GO