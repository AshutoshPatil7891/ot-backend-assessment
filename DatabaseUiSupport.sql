-- Extra database objects for the login, games list and admin pages of the UI.
-- Run it AFTER DatabaseGenerate.sql, for example:  sqlcmd -S localhost -E -C -b -i DatabaseUiSupport.sql
-- It only adds things and is safe to re-run. Nothing from DatabaseGenerate.sql is changed.

USE OT_Assessment_DB;
GO


-- two new nullable columns on Player, so every existing row and the Consumer keep working
-- (Username already exists and is the display name, so the login name is a separate column)
IF COL_LENGTH('dbo.Player', 'LoginName') IS NULL
    ALTER TABLE Player ADD LoginName NVARCHAR(100) NULL;
GO

IF COL_LENGTH('dbo.Player', 'PasswordHash') IS NULL
    ALTER TABLE Player ADD PasswordHash NVARCHAR(200) NULL;
GO

-- An earlier version of this script created a UNIQUE FILTERED index here. A filtered index makes
-- every write to Player need QUOTED_IDENTIFIER ON, which the Consumer's existing procedure does not
-- use, so it broke saving bets. Remove it if it is there; uniqueness is checked in usp_RegisterPlayer.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Player_LoginName' AND object_id = OBJECT_ID('dbo.Player'))
    DROP INDEX UX_Player_LoginName ON Player;
GO

-- plain (not filtered, not unique) index, only to make the login lookup fast
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Player_LoginName' AND object_id = OBJECT_ID('dbo.Player'))
    CREATE INDEX IX_Player_LoginName ON Player(LoginName);
GO

-- a few games, so the player has something to pick before any bet has been saved
-- (the Consumer reuses a game with the same name and provider, so no duplicates come from it)
INSERT INTO Game (Name, Provider, Theme)
SELECT v.Name, v.Provider, v.Theme
FROM (VALUES
    (N'Starburst',        N'NetEnt',        N'space'),
    (N'Book of Dead',     N'Play n GO',     N'egypt'),
    (N'Gonzo''s Quest',   N'NetEnt',        N'adventure'),
    (N'Sweet Bonanza',    N'Pragmatic Play', N'candy'),
    (N'Mega Moolah',      N'Microgaming',   N'safari')
) AS v(Name, Provider, Theme)
WHERE NOT EXISTS (SELECT 1 FROM Game g WHERE g.Name = v.Name AND g.Provider = v.Provider);
GO

-- returns 1 when the player was created, 0 when the login name is already used
CREATE OR ALTER PROCEDURE usp_RegisterPlayer
    @AccountId UNIQUEIDENTIFIER,
    @LoginName NVARCHAR(100),
    @PasswordHash NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    -- check and insert in one transaction with locks held, so two people cannot take the same name
    BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM Player WITH (UPDLOCK, HOLDLOCK) WHERE LoginName = @LoginName)
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT 0;
        RETURN;
    END;

    INSERT INTO Player (AccountId, Username, CountryCode, LoginName, PasswordHash)
    VALUES (@AccountId, @LoginName, NULL, @LoginName, @PasswordHash);

    COMMIT TRANSACTION;

    SELECT 1;
END;
GO

CREATE OR ALTER PROCEDURE usp_GetPlayerLogin
    @LoginName NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT AccountId, Username, PasswordHash
    FROM Player
    WHERE LoginName = @LoginName;
END;
GO

-- players that have a login, filtered by part of the login name (empty = all), with what they spent
CREATE OR ALTER PROCEDURE usp_GetRegisteredPlayers
    @Search NVARCHAR(100),
    @Count INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Count)
        p.AccountId,
        p.Username,
        ISNULL(t.TotalAmountSpend, 0) AS TotalAmountSpend
    FROM Player p
    LEFT JOIN PlayerSpendTotal t ON t.AccountId = p.AccountId
    WHERE p.LoginName IS NOT NULL
      AND (@Search = N'' OR p.LoginName LIKE N'%' + @Search + N'%')
    ORDER BY p.LoginName;
END;
GO

CREATE OR ALTER PROCEDURE usp_GetGames
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Name AS GameName, Provider, Theme
    FROM Game
    ORDER BY Name, Provider;
END;
GO
