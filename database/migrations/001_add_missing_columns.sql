-- Migration: Add missing columns to DailyStockMetrics
-- Version: 001
-- Date: 2026-01-19

-- Add OpenPrice column if not exists
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DailyStockMetrics') AND name = 'OpenPrice')
BEGIN
    ALTER TABLE DailyStockMetrics ADD OpenPrice DECIMAL(18, 2);
    PRINT 'Added OpenPrice column';
END
GO

-- Add HighPrice column if not exists
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DailyStockMetrics') AND name = 'HighPrice')
BEGIN
    ALTER TABLE DailyStockMetrics ADD HighPrice DECIMAL(18, 2);
    PRINT 'Added HighPrice column';
END
GO

-- Add LowPrice column if not exists
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DailyStockMetrics') AND name = 'LowPrice')
BEGIN
    ALTER TABLE DailyStockMetrics ADD LowPrice DECIMAL(18, 2);
    PRINT 'Added LowPrice column';
END
GO

-- Add Turnover column if not exists
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DailyStockMetrics') AND name = 'Turnover')
BEGIN
    ALTER TABLE DailyStockMetrics ADD Turnover BIGINT;
    PRINT 'Added Turnover column';
END
GO

PRINT 'Migration 001 completed successfully.';
GO
