USE [PS_UserData]
GO

/*
    Plain launcher login support.

    This script is intentionally minimal:
      - It DOES NOT modify PS_Login procedures.
      - It only creates a saved-account table used by the updater UI.

    Login flow:
      PHP API validates UserID/password with dbo.CheckPw_Safe.
      PHP API returns Users_Master.Pw as passwordForGame.
      game.exe receives the DB password as-is.
*/

IF OBJECT_ID(N'dbo.Updater_SavedDeviceAccount', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Updater_SavedDeviceAccount
    (
        DeviceIdHash varbinary(32) NOT NULL,
        UserUID      int NOT NULL,
        UserID       varchar(18) NOT NULL,
        CreatedAt    datetime2(0) NOT NULL CONSTRAINT DF_Updater_SavedDeviceAccount_CreatedAt DEFAULT SYSUTCDATETIME(),
        LastUsedAt   datetime2(0) NULL,
        LastIP       varchar(45) NULL,
        CONSTRAINT PK_Updater_SavedDeviceAccount PRIMARY KEY(DeviceIdHash, UserUID)
    );

    CREATE INDEX IX_Updater_SavedDeviceAccount_UserUID
        ON dbo.Updater_SavedDeviceAccount(UserUID);
END
GO
