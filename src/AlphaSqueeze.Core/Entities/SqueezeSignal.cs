namespace AlphaSqueeze.Core.Entities;

/// <summary>
/// 軋空訊號實體
/// 存儲每日計算的軋空分數
/// </summary>
public class SqueezeSignal
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
    /// 訊號日期
    /// </summary>
    public DateTime SignalDate { get; set; }

    /// <summary>
    /// 總分 (0-100)
    /// </summary>
    public int SqueezeScore { get; set; }

    /// <summary>
    /// 法人空頭分數 (F_B)
    /// 權重: 35%
    /// </summary>
    public decimal? BorrowScore { get; set; }

    /// <summary>
    /// Gamma效應分數 (F_G)
    /// 權重: 25%
    /// </summary>
    public decimal? GammaScore { get; set; }

    /// <summary>
    /// 散戶燃料分數 (F_M)
    /// 權重: 20%
    /// </summary>
    public decimal? MarginScore { get; set; }

    /// <summary>
    /// 價量動能分數 (F_V)
    /// 權重: 20%
    /// </summary>
    public decimal? MomentumScore { get; set; }

    /// <summary>
    /// 趨勢預判 (BULLISH/NEUTRAL/BEARISH)
    /// </summary>
    public string? Trend { get; set; }

    /// <summary>
    /// 戰術建議
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// 是否已發送通知
    /// </summary>
    public bool NotificationSent { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 趨勢類型列舉
/// </summary>
public enum TrendType
{
    /// <summary>
    /// 看漲 (Score >= 70)
    /// </summary>
    Bullish,

    /// <summary>
    /// 中性 (40 < Score < 70)
    /// </summary>
    Neutral,

    /// <summary>
    /// 看跌 (Score <= 40)
    /// </summary>
    Bearish,

    /// <summary>
    /// 降級模式 (引擎無法連線)
    /// </summary>
    Degraded
}
