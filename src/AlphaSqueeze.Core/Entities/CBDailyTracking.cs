namespace AlphaSqueeze.Core.Entities;

/// <summary>
/// CB 每日追蹤實體
/// 對應資料庫 CBDailyTracking 表
/// </summary>
public class CBDailyTracking
{
    /// <summary>
    /// 主鍵 ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// CB 代號
    /// </summary>
    public string CBTicker { get; set; } = string.Empty;

    /// <summary>
    /// 交易日期
    /// </summary>
    public DateTime TradeDate { get; set; }

    /// <summary>
    /// 標的收盤價
    /// </summary>
    public decimal? UnderlyingClosePrice { get; set; }

    /// <summary>
    /// 轉換價
    /// </summary>
    public decimal? ConversionPrice { get; set; }

    /// <summary>
    /// 股價/轉換價 比率 (%)
    /// </summary>
    public decimal? PriceToConversionRatio { get; set; }

    /// <summary>
    /// 是否超過觸發門檻
    /// </summary>
    public bool? IsAboveTrigger { get; set; }

    /// <summary>
    /// 連續超過天數
    /// </summary>
    public int ConsecutiveDaysAbove { get; set; }

    /// <summary>
    /// 剩餘餘額 (億)
    /// </summary>
    public decimal? OutstandingBalance { get; set; }

    /// <summary>
    /// 餘額變化率 (%)
    /// </summary>
    public decimal? BalanceChangePercent { get; set; }

    /// <summary>
    /// 預警等級 (SAFE/CAUTION/WARNING/CRITICAL)
    /// </summary>
    public string? WarningLevel { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
