namespace AlphaSqueeze.Core.Entities;

/// <summary>
/// CB 可轉債數據實體
/// 預留擴充用
/// </summary>
public class CBMarketData
{
    /// <summary>
    /// 主鍵 ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// CB 代號
    /// </summary>
    public string CBTicker { get; set; } = string.Empty;

    /// <summary>
    /// 標的代號
    /// </summary>
    public string UnderlyingTicker { get; set; } = string.Empty;

    /// <summary>
    /// CB 名稱
    /// </summary>
    public string? CBName { get; set; }

    /// <summary>
    /// CB 價格
    /// </summary>
    public decimal? CBPrice { get; set; }

    /// <summary>
    /// 轉換價格
    /// </summary>
    public decimal? ConversionPrice { get; set; }

    /// <summary>
    /// 轉換溢價率 (%)
    /// </summary>
    public decimal? ConversionPremium { get; set; }

    /// <summary>
    /// 殖利率
    /// </summary>
    public decimal? YieldToMaturity { get; set; }

    /// <summary>
    /// 到期日
    /// </summary>
    public DateTime? MaturityDate { get; set; }

    /// <summary>
    /// 資料日期
    /// </summary>
    public DateTime TradeDate { get; set; }

    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTime LastUpdate { get; set; }
}
