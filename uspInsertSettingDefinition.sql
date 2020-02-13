USE [master]
GO

/****** Object:  StoredProcedure [dbo].[uspInsertSettingDefinition]    Script Date: 2/12/2020 8:48:27 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[uspInsertSettingDefinition]
	@ClientId varchar(25),
	@SettingLevel varchar(25),
	@SettingName varchar(50),
	@SettingType varchar(10),
	@SettingValue varchar(100),
	@IsEncrypted bit
	
AS
BEGIN TRY
	BEGIN TRANSACTION
	IF NOT EXISTS(SELECT * FROM dbo.ConfigSettings WHERE clientid = @ClientId AND settingName = @settingName)
		BEGIN
			Insert Into dbo.ConfigSettings(clientId, settingLevel, settingName, settingType, settingValue, isEncrypted)
			VALUES(@ClientId, @SettingLevel, @SettingName, @SettingType, @SettingValue, @IsEncrypted)
		END
	ELSE
		BEGIN
			UPDATE dbo.ConfigSettings
			SET clientId = @ClientId, settingLevel = @SettingLevel, settingName = @SettingName, settingType = @SettingType, settingValue = @SettingValue, isEncrypted = @IsEncrypted
			WHERE clientid = @ClientId AND settingName = @settingName
		END
	COMMIT
END TRY
BEGIN CATCH
	DECLARE @ErrorMessage varchar(MAX)
	SET @ErrorMessage = ERROR_MESSAGE()
	RAISERROR(@ErrorMessage, 1, 1)
	ROLLBACK
END CATCH
GO

