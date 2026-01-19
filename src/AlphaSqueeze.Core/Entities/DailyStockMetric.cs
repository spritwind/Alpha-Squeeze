namespace AlphaSqueeze.Core.Entities;

/// <summary>
/// 股票日指標實體
/// 存儲 FinMind 抓取的籌碼與價格數據
/// </summary>
public class DailyStockMetric
{
    /// <summary>
    /// 主鍵 ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 股票代號
    /// </summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>
    /// 交易日期
    /// </summary>
    public DateTime TradeDate { get; set; }

    /// <summary>
    /// 收盤價
    /// </summary>
    public decimal? ClosePrice { get; set; }

    /// <summary>
    /// 開盤價
    /// </summary>
    public decimal? OpenPrice { get; set; }

    /// <summary>
    /// 最高價
    /// </summary>
    public decimal? HighPrice { get; set; }

    /// <summary>
    /// 最低價
    /// </summary>
    public decimal? LowPrice { get; set; }

    /// <summary>
    /// 借券賣出餘額
    /// </summary>
    public long? BorrowingBalance { get; set; }

    /// <summary>
    /// 借券賣出餘額增減 (核心指標)
    /// 負值表示回補，正值表示增加
    /// </summary>
    public int? BorrowingBalanceChange { get; set; }

    /// <summary>
    /// 融資餘額
    /// </summary>
    public long? MarginBalance { get; set; }

    /// <summary>
    /// 融券餘額
    /// </summary>
    public long? ShortBalance { get; set; }

    /// <summary>
    /// 券資比 (%)
    /// 融券 / 融資 * 100
    /// </summary>
    public decimal? MarginRatio { get; set; }

    /// <summary>
    /// 20日歷史波動率 (HV)
    /// </summary>
    public decimal? HistoricalVolatility20D { get; set; }

    /// <summary>
    /// 成交量
    /// </summary>
    public long? Volume { get; set; }

    /// <summary>
    /// 成交金額
    /// </summary>
    public long? Turnover { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新時間
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
