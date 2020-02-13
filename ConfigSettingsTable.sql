USE [master]
GO

/****** Object:  Table [dbo].[ConfigSettings]    Script Date: 2/12/2020 8:47:45 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[ConfigSettings](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[clientId] [varchar](25) NOT NULL,
	[settingName] [varchar](50) NOT NULL,
	[settingType] [varchar](10) NOT NULL,
	[settingValue] [varchar](100) NOT NULL,
	[isEncrypted] [bit] NULL,
	[settingLevel] [varchar](25) NULL
) ON [PRIMARY]
GO

