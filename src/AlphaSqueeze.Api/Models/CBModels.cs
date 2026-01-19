namespace AlphaSqueeze.Api.Models;

/// <summary>
/// CB 預警 DTO
/// </summary>
public record CBWarningDto
{
    /// <summary>CB 代號</summary>
    public string CBTicker { get; init; } = string.Empty;

    /// <summary>標的股票代號</summary>
    public string UnderlyingTicker { get; init; } = string.Empty;

    /// <summary>CB 名稱</summary>
    public string? CBName { get; init; }

    /// <summary>交易日期</summary>
    public DateTime TradeDate { get; init; }

    /// <summary>標的收盤價</summary>
    public decimal CurrentPrice { get; init; }

    /// <summary>轉換價</summary>
    public decimal ConversionPrice { get; init; }

    /// <summary>股價/轉換價比率 (%)</summary>
    public decimal PriceRatio { get; init; }

    /// <summary>是否超過觸發門檻</summary>
    public bool IsAboveTrigger { get; init; }

    /// <summary>連續超過天數</summary>
    public int ConsecutiveDays { get; init; }

    /// <summary>距離觸發剩餘天數</summary>
    public int DaysRemaining { get; init; }

    /// <summary>觸發進度 (0-100%)</summary>
    public decimal TriggerProgress { get; init; }

    /// <summary>剩餘餘額 (億)</summary>
    public decimal OutstandingBalance { get; init; }

    /// <summary>發行總額 (億)</summary>
    public decimal? TotalIssueAmount { get; init; }

    /// <summary>餘額變化率 (%)</summary>
    public decimal? BalanceChangePercent { get; init; }

    /// <summary>預警等級 (SAFE/CAUTION/WARNING/CRITICAL)</summary>
    public string WarningLevel { get; init; } = "SAFE";

    /// <summary>風險提示</summary>
    public string Comment { get; init; } = string.Empty;

    /// <summary>到期日</summary>
    public DateTime? MaturityDate { get; init; }
}

/// <summary>
/// CB 預警清單回應 DTO
/// </summary>
public record CBWarningListResponse
{
    /// <summary>預警清單</summary>
    public List<CBWarningDto> Warnings { get; init; } = new();

    /// <summary>分析日期</summary>
    public string AnalysisDate { get; init; } = string.Empty;

    /// <summary>總 CB 數量</summary>
    public int TotalCount { get; init; }

    /// <summary>CRITICAL 等級數量</summary>
    public int CriticalCount { get; init; }

    /// <summary>WARNING 等級數量</summary>
    public int WarningCount { get; init; }

    /// <summary>CAUTION 等級數量</summary>
    public int CautionCount { get; init; }
}

/// <summary>
/// CB 發行資訊 DTO
/// </summary>
public record CBIssuanceDto
{
    /// <summary>CB 代號</summary>
    public string CBTicker { get; init; } = string.Empty;

    /// <summary>標的股票代號</summary>
    public string UnderlyingTicker { get; init; } = string.Empty;

    /// <summary>CB 名稱</summary>
    public string? CBName { get; init; }

    /// <summary>發行日</summary>
    public DateTime IssueDate { get; init; }

    /// <summary>到期日</summary>
    public DateTime MaturityDate { get; init; }

    /// <summary>現行轉換價</summary>
    public decimal CurrentConversionPrice { get; init; }

    /// <summary>發行總額 (億)</summary>
    public decimal TotalIssueAmount { get; init; }

    /// <summary>流通餘額 (億)</summary>
    public decimal OutstandingAmount { get; init; }

    /// <summary>贖回觸發門檻 (%)</summary>
    public decimal RedemptionTriggerPct { get; init; }

    /// <summary>連續觸發天數門檻</summary>
    public int RedemptionTriggerDays { get; init; }

    /// <summary>是否流通中</summary>
    public bool IsActive { get; init; }
}

/// <summary>
/// CB 歷史追蹤 DTO
/// </summary>
public record CBTrackingHistoryDto
{
    /// <summary>CB 代號</summary>
    public string CBTicker { get; init; } = string.Empty;

    /// <summary>歷史追蹤資料</summary>
    public List<CBDailyTrackingDto> History { get; init; } = new();
}

/// <summary>
/// CB 每日追蹤 DTO
/// </summary>
public record CBDailyTrackingDto
{
    /// <summary>交易日期</summary>
    public DateTime TradeDate { get; init; }

    /// <summary>標的收盤價</summary>
    public decimal? UnderlyingClosePrice { get; init; }

    /// <summary>股價/轉換價比率 (%)</summary>
    public decimal? PriceRatio { get; init; }

    /// <summary>是否超過觸發門檻</summary>
    public bool IsAboveTrigger { get; init; }

    /// <summary>連續超過天數</summary>
    public int ConsecutiveDays { get; init; }

    /// <summary>剩餘餘額 (億)</summary>
    public decimal? OutstandingBalance { get; init; }

    /// <summary>預警等級</summary>
    public string? WarningLevel { get; init; }
}
