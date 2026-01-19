namespace AlphaSqueeze.Core.Entities;

/// <summary>
/// 追蹤股票實體
/// 對應資料庫 TrackedTickers 表
/// </summary>
public class TrackedTicker
{
    /// <summary>股票代號</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>股票名稱</summary>
    public string? TickerName { get; set; }

    /// <summary>類股分類</summary>
    public string? Category { get; set; }

    /// <summary>是否啟用追蹤</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>優先級 (數字越小越優先)</summary>
    public int Priority { get; set; } = 100;

    /// <summary>加入時間</summary>
    public DateTime AddedAt { get; set; }

    /// <summary>備註</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// 股票類股分類
/// </summary>
public static class TickerCategory
{
    public const string Semiconductor = "半導體";
    public const string Electronics = "電子";
    public const string Financial = "金融";
    public const string Telecom = "電信";
    public const string Petrochemical = "塑化";
    public const string Steel = "鋼鐵";
    public const string Automotive = "汽車";
    public const string Retail = "零售";
    public const string Other = "其他";
}
