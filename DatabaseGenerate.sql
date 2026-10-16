
CREATE DATABASE OT_Assessment_DB;
GO

USE OT_Assessment_DB;
GO

CREATE TABLE Player
(
    AccountId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL,
    CountryCode CHAR(2) NULL
);
GO

CREATE TABLE Game
(
    GameId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Provider NVARCHAR(200) NOT NULL,
    Theme NVARCHAR(100) NULL,

    CONSTRAINT UQ_Game_Name_Provider UNIQUE (Name, Provider)
);

CREATE TABLE CasinoWager
(
    WagerId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    AccountId UNIQUEIDENTIFIER NOT NULL,
    GameId INT NOT NULL,
    Amount DECIMAL(19,4) NOT NULL,
    CreatedDateTime DATETIMEOFFSET(7) NOT NULL,
    NumberOfBets INT NOT NULL,
    Duration BIGINT NOT NULL,
    TransactionId UNIQUEIDENTIFIER NOT NULL,

    CONSTRAINT FK_CasinoWager_Player
        FOREIGN KEY (AccountId) REFERENCES Player(AccountId),

    CONSTRAINT FK_CasinoWager_Game
        FOREIGN KEY (GameId) REFERENCES Game(GameId)
);
GO

CREATE INDEX IX_CasinoWager_AccountId_CreatedDateTime
ON CasinoWager(AccountId, CreatedDateTime DESC);
GO

CREATE TABLE PlayerSpendTotal
(
    AccountId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TotalAmountSpend DECIMAL(19,4) NOT NULL DEFAULT 0,

    CONSTRAINT FK_PlayerSpendTotal_Player
        FOREIGN KEY (AccountId) REFERENCES Player(AccountId)
);
GO

CREATE INDEX IX_PlayerSpendTotal_TotalAmountSpend
ON PlayerSpendTotal(TotalAmountSpend DESC);
GO
CREATE TYPE CasinoWagerType AS TABLE
(
    WagerId UNIQUEIDENTIFIER NOT NULL,
    AccountId UNIQUEIDENTIFIER NOT NULL,
    Username NVARCHAR(100) NOT NULL,
    CountryCode CHAR(2) NULL,
    GameName NVARCHAR(200) NOT NULL,
    Provider NVARCHAR(200) NOT NULL,
    Theme NVARCHAR(100) NULL,
    Amount DECIMAL(19,4) NOT NULL,
    CreatedDateTime DATETIMEOFFSET(7) NOT NULL,
    NumberOfBets INT NOT NULL,
    Duration BIGINT NOT NULL,
    TransactionId UNIQUEIDENTIFIER NOT NULL
);
GO

--------------------------------------------GET-SP--------------------------------------------
CREATE PROCEDURE usp_GetPlayerCasinoWagers
    @AccountId UNIQUEIDENTIFIER,
    @Page INT,
    @PageSize INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        w.WagerId,
        g.Name AS Game,
        g.Provider,
        w.Amount,
        w.CreatedDateTime AS CreatedDate,
        COUNT(*) OVER () AS Total
    FROM CasinoWager w
    INNER JOIN Game g ON w.GameId = g.GameId
    WHERE w.AccountId = @AccountId
    ORDER BY w.CreatedDateTime DESC
    OFFSET (@Page - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;
GO

CREATE PROCEDURE usp_GetTopSpenders
    @Count INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Count)
        p.AccountId,
        p.Username,
        t.TotalAmountSpend
    FROM PlayerSpendTotal t
    INNER JOIN Player p ON t.AccountId = p.AccountId
    ORDER BY t.TotalAmountSpend DESC;
END;
GO

CREATE PROCEDURE usp_InsertCasinoWagers
    @Wagers CasinoWagerType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- one row per new wager: drops repeats inside the batch and wagers already stored
    SELECT
        w.WagerId, w.AccountId, w.Username, w.CountryCode,
        w.GameName, w.Provider, w.Theme, w.Amount,
        w.CreatedDateTime, w.NumberOfBets, w.Duration, w.TransactionId
    INTO #NewWagers
    FROM
    (
        SELECT *, ROW_NUMBER() OVER (PARTITION BY WagerId ORDER BY CreatedDateTime) AS RowNum
        FROM @Wagers
    ) w
    WHERE w.RowNum = 1
      AND NOT EXISTS (SELECT 1 FROM CasinoWager c WHERE c.WagerId = w.WagerId);

    -- one row per player (same player can arrive with different country codes)
    INSERT INTO Player (AccountId, Username, CountryCode)
    SELECT n.AccountId, MAX(n.Username), MAX(n.CountryCode)
    FROM #NewWagers n
    WHERE NOT EXISTS
    (
        SELECT 1 FROM Player p WITH (UPDLOCK, HOLDLOCK)
        WHERE p.AccountId = n.AccountId
    )
    GROUP BY n.AccountId;

    INSERT INTO Game (Name, Provider, Theme)
    SELECT n.GameName, n.Provider, MAX(n.Theme)
    FROM #NewWagers n
    WHERE NOT EXISTS
    (
        SELECT 1 FROM Game g WITH (UPDLOCK, HOLDLOCK)
        WHERE g.Name = n.GameName AND g.Provider = n.Provider
    )
    GROUP BY n.GameName, n.Provider;

    INSERT INTO CasinoWager
        (WagerId, AccountId, GameId, Amount, CreatedDateTime, NumberOfBets, Duration, TransactionId)
    SELECT
        n.WagerId, n.AccountId, g.GameId, n.Amount,
        n.CreatedDateTime, n.NumberOfBets, n.Duration, n.TransactionId
    FROM #NewWagers n
    INNER JOIN Game g ON g.Name = n.GameName AND g.Provider = n.Provider;

    UPDATE t
    SET t.TotalAmountSpend = t.TotalAmountSpend + s.Total
    FROM PlayerSpendTotal t
    INNER JOIN
    (
        SELECT AccountId, SUM(Amount) AS Total
        FROM #NewWagers
        GROUP BY AccountId
    ) s ON s.AccountId = t.AccountId;

    INSERT INTO PlayerSpendTotal (AccountId, TotalAmountSpend)
    SELECT s.AccountId, s.Total
    FROM
    (
        SELECT AccountId, SUM(Amount) AS Total
        FROM #NewWagers
        GROUP BY AccountId
    ) s
    WHERE NOT EXISTS
    (
        SELECT 1 FROM PlayerSpendTotal t WITH (UPDLOCK, HOLDLOCK)
        WHERE t.AccountId = s.AccountId
    );

    COMMIT TRANSACTION;
END;
GO
