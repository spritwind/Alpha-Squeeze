namespace AlphaSqueeze.Core.Entities;

/// <summary>
/// 用戶追蹤清單
/// 紀錄用戶勾選後，系統必須執行「深度爬蟲」的標的
/// </summary>
public class UserWatchList
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string? TickerName { get; set; }
    public DateTime AddedTime { get; set; }
    public string AddedBy { get; set; } = "WebUI";
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 100;
    public DateTime? LastDeepScrapedTime { get; set; }
    public int? LastSqueezeScore { get; set; }
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; }
}
