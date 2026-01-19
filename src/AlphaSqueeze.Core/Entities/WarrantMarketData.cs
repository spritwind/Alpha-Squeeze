namespace AlphaSqueeze.Core.Entities;

/// <summary>
/// 權證實時數據實體
/// 存儲 Scraper 抓取的權證端數據
/// </summary>
public class WarrantMarketData
{
    /// <summary>
    /// 主鍵 ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 標的代號
    /// </summary>
    public string UnderlyingTicker { get; set; } = string.Empty;

    /// <summary>
    /// 權證代號
    /// </summary>
    public string WarrantTicker { get; set; } = string.Empty;

    /// <summary>
    /// 權證名稱
    /// </summary>
    public string? WarrantName { get; set; }

    /// <summary>
    /// 發行商 (元大, 統一, etc.)
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// 權證類型 (Call/Put)
    /// </summary>
    public string? WarrantType { get; set; }

    /// <summary>
    /// 隱含波動率 (IV)
    /// </summary>
    public decimal? ImpliedVolatility { get; set; }

    /// <summary>
    /// 實質槓桿
    /// </summary>
    public decimal? EffectiveLeverage { get; set; }

    /// <summary>
    /// 差槓比
    /// </summary>
    public decimal? SpreadRatio { get; set; }

    /// <summary>
    /// 履約價
    /// </summary>
    public decimal? StrikePrice { get; set; }

    /// <summary>
    /// 到期日
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// 剩餘天數
    /// </summary>
    public int? DaysToExpiry { get; set; }

    /// <summary>
    /// Delta 值
    /// </summary>
    public decimal? Delta { get; set; }

    /// <summary>
    /// Gamma 值
    /// </summary>
    public decimal? Gamma { get; set; }

    /// <summary>
    /// Theta 值
    /// </summary>
    public decimal? Theta { get; set; }

    /// <summary>
    /// Vega 值
    /// </summary>
    public decimal? Vega { get; set; }

    /// <summary>
    /// 資料日期
    /// </summary>
    public DateTime TradeDate { get; set; }

    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTime LastUpdate { get; set; }
}
