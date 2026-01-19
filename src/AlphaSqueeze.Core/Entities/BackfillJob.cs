namespace AlphaSqueeze.Core.Entities;

/// <summary>
/// 資料回補任務實體
/// 對應資料庫 BackfillJobs 表
/// </summary>
public class BackfillJob
{
    /// <summary>任務 ID</summary>
    public int Id { get; set; }

    /// <summary>任務類型 (STOCK_METRICS/WARRANT_DATA/FULL/GAP_FILL)</summary>
    public string JobType { get; set; } = "STOCK_METRICS";

    /// <summary>開始日期</summary>
    public DateTime StartDate { get; set; }

    /// <summary>結束日期</summary>
    public DateTime EndDate { get; set; }

    /// <summary>狀態 (PENDING/RUNNING/COMPLETED/FAILED)</summary>
    public string Status { get; set; } = "PENDING";

    /// <summary>總股票數量</summary>
    public int TotalTickers { get; set; }

    /// <summary>已處理數量</summary>
    public int ProcessedTickers { get; set; }

    /// <summary>失敗數量</summary>
    public int FailedTickers { get; set; }

    /// <summary>錯誤訊息</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>開始時間</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>完成時間</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>建立時間</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>建立者</summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 計算進度百分比
    /// </summary>
    public double ProgressPercent =>
        TotalTickers > 0 ? Math.Round((double)ProcessedTickers / TotalTickers * 100, 1) : 0;

    /// <summary>
    /// 是否正在執行
    /// </summary>
    public bool IsRunning => Status == "RUNNING";

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted => Status == "COMPLETED" || Status == "FAILED";
}

/// <summary>
/// 回補任務狀態
/// </summary>
public static class BackfillStatus
{
    public const string Pending = "PENDING";
    public const string Running = "RUNNING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
}

/// <summary>
/// 回補任務類型
/// </summary>
public static class BackfillJobType
{
    public const string StockMetrics = "STOCK_METRICS";
    public const string WarrantData = "WARRANT_DATA";
    public const string Full = "FULL";
    public const string GapFill = "GAP_FILL";
}
