using System.ComponentModel.DataAnnotations;

namespace AlphaSqueeze.Api.Models;

/// <summary>
/// 回補任務 DTO
/// </summary>
public class BackfillJobDto
{
    /// <summary>任務 ID</summary>
    public int Id { get; set; }

    /// <summary>任務類型</summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>開始日期</summary>
    public DateTime StartDate { get; set; }

    /// <summary>結束日期</summary>
    public DateTime EndDate { get; set; }

    /// <summary>狀態</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>總股票數量</summary>
    public int TotalTickers { get; set; }

    /// <summary>已處理數量</summary>
    public int ProcessedTickers { get; set; }

    /// <summary>失敗數量</summary>
    public int FailedTickers { get; set; }

    /// <summary>進度百分比</summary>
    public double ProgressPercent { get; set; }

    /// <summary>錯誤訊息</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>開始時間</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>完成時間</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>建立時間</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 建立回補任務請求 DTO
/// </summary>
public class CreateBackfillRequest
{
    /// <summary>開始日期 (YYYY-MM-DD)</summary>
    [Required(ErrorMessage = "開始日期為必填")]
    public string StartDate { get; set; } = string.Empty;

    /// <summary>結束日期 (YYYY-MM-DD)</summary>
    [Required(ErrorMessage = "結束日期為必填")]
    public string EndDate { get; set; } = string.Empty;

    /// <summary>指定股票代號 (選填，空白則使用所有追蹤股票)</summary>
    public List<string>? Tickers { get; set; }

    /// <summary>任務類型</summary>
    public string JobType { get; set; } = "STOCK_METRICS";
}

/// <summary>
/// 追蹤股票 DTO
/// </summary>
public class TrackedTickerDto
{
    /// <summary>股票代號</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>股票名稱</summary>
    public string? TickerName { get; set; }

    /// <summary>類股分類</summary>
    public string? Category { get; set; }

    /// <summary>是否啟用</summary>
    public bool IsActive { get; set; }

    /// <summary>優先級</summary>
    public int Priority { get; set; }

    /// <summary>加入時間</summary>
    public DateTime AddedAt { get; set; }

    /// <summary>備註</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// 新增追蹤股票請求 DTO
/// </summary>
public class AddTrackedTickerRequest
{
    /// <summary>股票代號</summary>
    [Required(ErrorMessage = "股票代號為必填")]
    [StringLength(10, MinimumLength = 4, ErrorMessage = "股票代號長度必須為 4-10 字元")]
    public string Ticker { get; set; } = string.Empty;

    /// <summary>股票名稱</summary>
    public string? TickerName { get; set; }

    /// <summary>類股分類</summary>
    public string? Category { get; set; }

    /// <summary>優先級 (數字越小越優先)</summary>
    [Range(1, 1000, ErrorMessage = "優先級必須在 1-1000 之間")]
    public int Priority { get; set; } = 100;

    /// <summary>備註</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// 資料狀態摘要 DTO
/// </summary>
public class DataStatusSummaryDto
{
    /// <summary>追蹤股票總數</summary>
    public int TotalTrackedTickers { get; set; }

    /// <summary>啟用中股票數</summary>
    public int ActiveTickers { get; set; }

    /// <summary>最新資料日期</summary>
    public DateTime? LatestDataDate { get; set; }

    /// <summary>資料筆數</summary>
    public int TotalRecords { get; set; }

    /// <summary>最近的回補任務</summary>
    public BackfillJobDto? LatestBackfillJob { get; set; }
}
