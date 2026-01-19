-- Alpha Squeeze Database Setup Script
-- 建立資料庫、使用者和權限
-- =============================================

USE master;
GO

-- =============================================
-- 1. 建立資料庫
-- =============================================
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'AlphaSqueeze')
BEGIN
    CREATE DATABASE AlphaSqueeze;
    PRINT N'資料庫 AlphaSqueeze 建立成功';
END
ELSE
BEGIN
    PRINT N'資料庫 AlphaSqueeze 已存在';
END
GO

-- =============================================
-- 2. 建立 SQL Server 登入
-- =============================================
IF NOT EXISTS (SELECT name FROM sys.server_principals WHERE name = 'leo')
BEGIN
    CREATE LOGIN [leo] WITH PASSWORD = N'1qaz@WSX',
        DEFAULT_DATABASE = [AlphaSqueeze],
        CHECK_EXPIRATION = OFF,
        CHECK_POLICY = OFF;
    PRINT N'登入帳號 leo 建立成功';
END
ELSE
BEGIN
    -- 如果登入已存在，更新密碼
    ALTER LOGIN [leo] WITH PASSWORD = N'1qaz@WSX';
    PRINT N'登入帳號 leo 已存在，已更新密碼';
END
GO

-- =============================================
-- 3. 切換到 AlphaSqueeze 資料庫
-- =============================================
USE AlphaSqueeze;
GO

-- =============================================
-- 4. 建立資料庫使用者
-- =============================================
IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = 'leo')
BEGIN
    CREATE USER [leo] FOR LOGIN [leo];
    PRINT N'資料庫使用者 leo 建立成功';
END
ELSE
BEGIN
    PRINT N'資料庫使用者 leo 已存在';
END
GO

-- =============================================
-- 5. 授予權限 (db_owner 完整權限)
-- =============================================
ALTER ROLE db_owner ADD MEMBER [leo];
PRINT N'已授予 leo db_owner 權限';
GO

-- =============================================
-- 6. 建立 Table Type (用於批量操作)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.types WHERE name = 'DailyStockMetricsType')
BEGIN
    CREATE TYPE dbo.DailyStockMetricsType AS TABLE
    (
        Ticker NVARCHAR(10) NOT NULL,
        TradeDate DATE NOT NULL,
        ClosePrice DECIMAL(18, 2),
        OpenPrice DECIMAL(18, 2),
        HighPrice DECIMAL(18, 2),
        LowPrice DECIMAL(18, 2),
        BorrowingBalance BIGINT,
        BorrowingBalanceChange INT,
        MarginBalance BIGINT,
        ShortBalance BIGINT,
        MarginRatio DECIMAL(18, 4),
        HistoricalVolatility20D DECIMAL(18, 6),
        Volume BIGINT,
        Turnover BIGINT
    );
    PRINT N'Table Type DailyStockMetricsType 建立成功';
END
GO

PRINT N'';
PRINT N'========================================';
PRINT N'資料庫設定完成！';
PRINT N'========================================';
PRINT N'資料庫: AlphaSqueeze';
PRINT N'使用者: leo';
PRINT N'密碼: 1qaz@WSX';
PRINT N'連線字串: Server=localhost;Database=AlphaSqueeze;User Id=leo;Password=1qaz@WSX;TrustServerCertificate=True;';
PRINT N'========================================';
GO
