namespace AlphaSqueeze.Api.Models;

/// <summary>
/// 軋空訊號 DTO
/// </summary>
public record SqueezeSignalDto
{
    /// <summary>股票代號</summary>
    public string Ticker { get; init; } = string.Empty;

    /// <summary>軋空分數 (0-100)</summary>
    public int Score { get; init; }

    /// <summary>趨勢判定 (BULLISH/NEUTRAL/BEARISH/DEGRADED)</summary>
    public string Trend { get; init; } = string.Empty;

    /// <summary>戰術建議</summary>
    public string Comment { get; init; } = string.Empty;

    /// <summary>各維度分數</summary>
    public FactorScoresDto? Factors { get; init; }
}

/// <summary>
/// 各維度分數 DTO
/// </summary>
public record FactorScoresDto
{
    /// <summary>法人空頭分數 (F_B) - 權重 35%</summary>
    public double BorrowScore { get; init; }

    /// <summary>Gamma 效應分數 (F_G) - 權重 25%</summary>
    public double GammaScore { get; init; }

    /// <summary>散戶燃料分數 (F_M) - 權重 20%</summary>
    public double MarginScore { get; init; }

    /// <summary>價量動能分數 (F_V) - 權重 20%</summary>
    public double MomentumScore { get; init; }
}

/// <summary>
/// 熱門候選標的 DTO
/// </summary>
public record TopCandidatesDto
{
    /// <summary>候選清單</summary>
    public List<SqueezeSignalDto> Candidates { get; init; } = new();

    /// <summary>分析日期</summary>
    public string AnalysisDate { get; init; } = string.Empty;

    /// <summary>產生時間</summary>
    public string GeneratedAt { get; init; } = string.Empty;
}

/// <summary>
/// 股票日指標 DTO
/// </summary>
public record StockMetricDto
{
    /// <summary>股票代號</summary>
    public string Ticker { get; init; } = string.Empty;

    /// <summary>交易日期</summary>
    public DateTime TradeDate { get; init; }

    /// <summary>收盤價</summary>
    public decimal? ClosePrice { get; init; }

    /// <summary>開盤價</summary>
    public decimal? OpenPrice { get; init; }

    /// <summary>最高價</summary>
    public decimal? HighPrice { get; init; }

    /// <summary>最低價</summary>
    public decimal? LowPrice { get; init; }

    /// <summary>借券餘額變化</summary>
    public int? BorrowingBalanceChange { get; init; }

    /// <summary>券資比 (%)</summary>
    public decimal? MarginRatio { get; init; }

    /// <summary>20日歷史波動率</summary>
    public decimal? HistoricalVolatility20D { get; init; }

    /// <summary>成交量</summary>
    public long? Volume { get; init; }
}

/// <summary>
/// API 錯誤回應
/// </summary>
public record ErrorResponse
{
    /// <summary>錯誤訊息</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>錯誤代碼</summary>
    public string? ErrorCode { get; init; }

    /// <summary>詳細資訊</summary>
    public string? Details { get; init; }
}
