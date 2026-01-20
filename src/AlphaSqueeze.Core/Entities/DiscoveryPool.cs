namespace AlphaSqueeze.Core.Entities;

/// <summary>
/// 潛在標的池
/// 紀錄每日雷達掃描後的初步建議名單
/// </summary>
public class DiscoveryPool
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string? TickerName { get; set; }
    public string? Industry { get; set; }
    public decimal? ClosePrice { get; set; }
    public long? Volume { get; set; }
    public long? AvgVolume5D { get; set; }
    public decimal? VolMultiplier { get; set; }
    public long? ShortSellingBalance { get; set; }
    public long? SharesOutstanding { get; set; }
    public decimal? ShortRatio { get; set; }
    public decimal? MarginRatio { get; set; }
    public bool HasCB { get; set; }
    public string? CBTicker { get; set; }
    public decimal? CBPriceRatio { get; set; }
    public int? SqueezeScore { get; set; }
    public DateTime ScanDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
