namespace AlphaSqueeze.Core.Entities;

/// <summary>
/// CB 發行資訊實體
/// 對應資料庫 CBIssuance 表
/// </summary>
public class CBIssuance
{
    /// <summary>
    /// 主鍵 ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// CB 代號 (e.g., 23301)
    /// </summary>
    public string CBTicker { get; set; } = string.Empty;

    /// <summary>
    /// 標的股票代號 (e.g., 2330)
    /// </summary>
    public string UnderlyingTicker { get; set; } = string.Empty;

    /// <summary>
    /// CB 名稱
    /// </summary>
    public string? CBName { get; set; }

    /// <summary>
    /// 發行日
    /// </summary>
    public DateTime IssueDate { get; set; }

    /// <summary>
    /// 到期日
    /// </summary>
    public DateTime MaturityDate { get; set; }

    /// <summary>
    /// 初始轉換價
    /// </summary>
    public decimal? InitialConversionPrice { get; set; }

    /// <summary>
    /// 現行轉換價
    /// </summary>
    public decimal? CurrentConversionPrice { get; set; }

    /// <summary>
    /// 發行總額 (億)
    /// </summary>
    public decimal? TotalIssueAmount { get; set; }

    /// <summary>
    /// 流通餘額 (億)
    /// </summary>
    public decimal? OutstandingAmount { get; set; }

    /// <summary>
    /// 贖回觸發門檻 (%) - 預設 130%
    /// </summary>
    public decimal RedemptionTriggerPct { get; set; } = 130.00m;

    /// <summary>
    /// 連續觸發天數門檻 - 預設 30 天
    /// </summary>
    public int RedemptionTriggerDays { get; set; } = 30;

    /// <summary>
    /// 是否流通中
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新時間
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
