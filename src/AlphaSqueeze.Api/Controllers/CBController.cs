using AlphaSqueeze.Api.Models;
using AlphaSqueeze.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AlphaSqueeze.Api.Controllers;

/// <summary>
/// CB 可轉債預警燈 API
///
/// 提供 CB 強制贖回風險監控功能：
/// - 追蹤股價連續超過轉換價 130% 的天數
/// - 根據連續天數判定預警等級 (SAFE/CAUTION/WARNING/CRITICAL)
/// - 當連續天數達 30 天時觸發 CRITICAL 等級
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CBController : ControllerBase
{
    private readonly ICBRepository _cbRepo;
    private readonly ILogger<CBController> _logger;

    private const int DefaultTriggerDays = 30;

    public CBController(
        ICBRepository cbRepo,
        ILogger<CBController> logger)
    {
        _cbRepo = cbRepo;
        _logger = logger;
    }

    /// <summary>
    /// 取得所有 CB 預警清單
    /// </summary>
    /// <param name="date">查詢日期 (預設: 今日)</param>
    /// <param name="minLevel">最低預警等級 (SAFE/CAUTION/WARNING/CRITICAL)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>CB 預警清單</returns>
    /// <response code="200">成功返回預警清單</response>
    [HttpGet("warnings")]
    [ProducesResponseType(typeof(CBWarningListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllWarnings(
        [FromQuery] DateTime? date = null,
        [FromQuery] string minLevel = "SAFE",
        CancellationToken ct = default)
    {
        var tradeDate = date ?? DateTime.Today;
        _logger.LogInformation("Requesting CB warnings for {Date} with minLevel={MinLevel}",
            tradeDate.ToString("yyyy-MM-dd"), minLevel);

        var warnings = await _cbRepo.GetCBWarningsWithDetailsAsync(tradeDate, minLevel);
        var summary = await _cbRepo.GetWarningSummaryAsync(tradeDate);

        var warningDtos = warnings.Select(w => MapToWarningDto(w)).ToList();

        var response = new CBWarningListResponse
        {
            Warnings = warningDtos,
            AnalysisDate = tradeDate.ToString("yyyy-MM-dd"),
            TotalCount = summary.Total,
            CriticalCount = summary.Critical,
            WarningCount = summary.Warning,
            CautionCount = summary.Caution
        };

        _logger.LogInformation("Returning {Count} CB warnings", warningDtos.Count);
        return Ok(response);
    }

    /// <summary>
    /// 取得單一 CB 預警狀態
    /// </summary>
    /// <param name="cbTicker">CB 代號</param>
    /// <param name="date">查詢日期 (預設: 今日)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>CB 預警詳情</returns>
    /// <response code="200">成功返回預警詳情</response>
    /// <response code="404">找不到該 CB</response>
    [HttpGet("{cbTicker}")]
    [ProducesResponseType(typeof(CBWarningDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWarning(
        string cbTicker,
        [FromQuery] DateTime? date = null,
        CancellationToken ct = default)
    {
        var tradeDate = date ?? DateTime.Today;
        cbTicker = cbTicker.ToUpperInvariant();

        _logger.LogInformation("Requesting CB warning for {CBTicker} on {Date}",
            cbTicker, tradeDate.ToString("yyyy-MM-dd"));

        // 取得 CB 發行資訊
        var cbIssuance = await _cbRepo.GetCBByTickerAsync(cbTicker);
        if (cbIssuance == null)
        {
            _logger.LogWarning("CB {CBTicker} not found", cbTicker);
            return NotFound(new ErrorResponse
            {
                Message = $"找不到 CB {cbTicker}",
                ErrorCode = "CB_NOT_FOUND"
            });
        }

        // 取得追蹤資料
        var tracking = await _cbRepo.GetDailyTrackingByTickerAsync(cbTicker, tradeDate);

        var dto = new CBWarningDto
        {
            CBTicker = cbIssuance.CBTicker,
            UnderlyingTicker = cbIssuance.UnderlyingTicker,
            CBName = cbIssuance.CBName,
            TradeDate = tradeDate,
            CurrentPrice = tracking?.UnderlyingClosePrice ?? 0,
            ConversionPrice = cbIssuance.CurrentConversionPrice ?? 0,
            PriceRatio = tracking?.PriceToConversionRatio ?? 0,
            IsAboveTrigger = tracking?.IsAboveTrigger ?? false,
            ConsecutiveDays = tracking?.ConsecutiveDaysAbove ?? 0,
            DaysRemaining = Math.Max(0, (cbIssuance.RedemptionTriggerDays) - (tracking?.ConsecutiveDaysAbove ?? 0)),
            TriggerProgress = CalculateTriggerProgress(tracking?.ConsecutiveDaysAbove ?? 0, cbIssuance.RedemptionTriggerDays),
            OutstandingBalance = tracking?.OutstandingBalance ?? cbIssuance.OutstandingAmount ?? 0,
            TotalIssueAmount = cbIssuance.TotalIssueAmount,
            BalanceChangePercent = tracking?.BalanceChangePercent,
            WarningLevel = tracking?.WarningLevel ?? "SAFE",
            Comment = GenerateComment(
                tracking?.ConsecutiveDaysAbove ?? 0,
                Math.Max(0, cbIssuance.RedemptionTriggerDays - (tracking?.ConsecutiveDaysAbove ?? 0)),
                tracking?.PriceToConversionRatio ?? 0,
                tracking?.OutstandingBalance ?? 0,
                tracking?.WarningLevel ?? "SAFE"),
            MaturityDate = cbIssuance.MaturityDate
        };

        return Ok(dto);
    }

    /// <summary>
    /// 取得高風險 CB 排行
    /// </summary>
    /// <param name="limit">返回數量 (預設 10)</param>
    /// <param name="minDays">最低連續天數 (預設 15)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>高風險 CB 清單</returns>
    [HttpGet("critical")]
    [ProducesResponseType(typeof(List<CBWarningDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCriticalCBs(
        [FromQuery] int limit = 10,
        [FromQuery] int minDays = 15,
        CancellationToken ct = default)
    {
        var tradeDate = DateTime.Today;
        _logger.LogInformation("Requesting critical CBs with minDays={MinDays}, limit={Limit}",
            minDays, limit);

        var criticalTrackings = await _cbRepo.GetCriticalCBsAsync(tradeDate, minDays, limit);

        var results = new List<CBWarningDto>();
        foreach (var tracking in criticalTrackings)
        {
            var cb = await _cbRepo.GetCBByTickerAsync(tracking.CBTicker);
            if (cb == null) continue;

            results.Add(new CBWarningDto
            {
                CBTicker = tracking.CBTicker,
                UnderlyingTicker = cb.UnderlyingTicker,
                CBName = cb.CBName,
                TradeDate = tracking.TradeDate,
                CurrentPrice = tracking.UnderlyingClosePrice ?? 0,
                ConversionPrice = tracking.ConversionPrice ?? 0,
                PriceRatio = tracking.PriceToConversionRatio ?? 0,
                IsAboveTrigger = tracking.IsAboveTrigger ?? false,
                ConsecutiveDays = tracking.ConsecutiveDaysAbove,
                DaysRemaining = Math.Max(0, cb.RedemptionTriggerDays - tracking.ConsecutiveDaysAbove),
                TriggerProgress = CalculateTriggerProgress(tracking.ConsecutiveDaysAbove, cb.RedemptionTriggerDays),
                OutstandingBalance = tracking.OutstandingBalance ?? 0,
                TotalIssueAmount = cb.TotalIssueAmount,
                BalanceChangePercent = tracking.BalanceChangePercent,
                WarningLevel = tracking.WarningLevel ?? "SAFE",
                Comment = GenerateComment(
                    tracking.ConsecutiveDaysAbove,
                    Math.Max(0, cb.RedemptionTriggerDays - tracking.ConsecutiveDaysAbove),
                    tracking.PriceToConversionRatio ?? 0,
                    tracking.OutstandingBalance ?? 0,
                    tracking.WarningLevel ?? "SAFE"),
                MaturityDate = cb.MaturityDate
            });
        }

        _logger.LogInformation("Returning {Count} critical CBs", results.Count);
        return Ok(results);
    }

    /// <summary>
    /// 取得特定標的的所有 CB 狀態
    /// </summary>
    /// <param name="ticker">標的股票代號</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>相關 CB 清單</returns>
    [HttpGet("by-underlying/{ticker}")]
    [ProducesResponseType(typeof(List<CBWarningDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByUnderlying(string ticker, CancellationToken ct = default)
    {
        ticker = ticker.ToUpperInvariant();
        var tradeDate = DateTime.Today;

        _logger.LogInformation("Requesting CBs for underlying {Ticker}", ticker);

        var cbs = await _cbRepo.GetCBsByUnderlyingAsync(ticker);

        var results = new List<CBWarningDto>();
        foreach (var cb in cbs)
        {
            var tracking = await _cbRepo.GetDailyTrackingByTickerAsync(cb.CBTicker, tradeDate);

            results.Add(new CBWarningDto
            {
                CBTicker = cb.CBTicker,
                UnderlyingTicker = cb.UnderlyingTicker,
                CBName = cb.CBName,
                TradeDate = tradeDate,
                CurrentPrice = tracking?.UnderlyingClosePrice ?? 0,
                ConversionPrice = cb.CurrentConversionPrice ?? 0,
                PriceRatio = tracking?.PriceToConversionRatio ?? 0,
                IsAboveTrigger = tracking?.IsAboveTrigger ?? false,
                ConsecutiveDays = tracking?.ConsecutiveDaysAbove ?? 0,
                DaysRemaining = Math.Max(0, cb.RedemptionTriggerDays - (tracking?.ConsecutiveDaysAbove ?? 0)),
                TriggerProgress = CalculateTriggerProgress(tracking?.ConsecutiveDaysAbove ?? 0, cb.RedemptionTriggerDays),
                OutstandingBalance = tracking?.OutstandingBalance ?? cb.OutstandingAmount ?? 0,
                TotalIssueAmount = cb.TotalIssueAmount,
                BalanceChangePercent = tracking?.BalanceChangePercent,
                WarningLevel = tracking?.WarningLevel ?? "SAFE",
                Comment = GenerateComment(
                    tracking?.ConsecutiveDaysAbove ?? 0,
                    Math.Max(0, cb.RedemptionTriggerDays - (tracking?.ConsecutiveDaysAbove ?? 0)),
                    tracking?.PriceToConversionRatio ?? 0,
                    tracking?.OutstandingBalance ?? 0,
                    tracking?.WarningLevel ?? "SAFE"),
                MaturityDate = cb.MaturityDate
            });
        }

        _logger.LogInformation("Returning {Count} CBs for underlying {Ticker}", results.Count, ticker);
        return Ok(results);
    }

    /// <summary>
    /// 取得所有活躍 CB 發行資訊
    /// </summary>
    [HttpGet("issuances")]
    [ProducesResponseType(typeof(List<CBIssuanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveCBs(CancellationToken ct = default)
    {
        var cbs = await _cbRepo.GetActiveCBsAsync();

        var results = cbs.Select(cb => new CBIssuanceDto
        {
            CBTicker = cb.CBTicker,
            UnderlyingTicker = cb.UnderlyingTicker,
            CBName = cb.CBName,
            IssueDate = cb.IssueDate,
            MaturityDate = cb.MaturityDate,
            CurrentConversionPrice = cb.CurrentConversionPrice ?? 0,
            TotalIssueAmount = cb.TotalIssueAmount ?? 0,
            OutstandingAmount = cb.OutstandingAmount ?? 0,
            RedemptionTriggerPct = cb.RedemptionTriggerPct,
            RedemptionTriggerDays = cb.RedemptionTriggerDays,
            IsActive = cb.IsActive
        }).ToList();

        return Ok(results);
    }

    /// <summary>
    /// 取得 CB 歷史追蹤資料
    /// </summary>
    /// <param name="cbTicker">CB 代號</param>
    /// <param name="days">歷史天數 (預設 30)</param>
    [HttpGet("{cbTicker}/history")]
    [ProducesResponseType(typeof(CBTrackingHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrackingHistory(
        string cbTicker,
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        cbTicker = cbTicker.ToUpperInvariant();

        var cb = await _cbRepo.GetCBByTickerAsync(cbTicker);
        if (cb == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = $"找不到 CB {cbTicker}",
                ErrorCode = "CB_NOT_FOUND"
            });
        }

        var endDate = DateTime.Today;
        var startDate = endDate.AddDays(-days);

        var history = await _cbRepo.GetTrackingHistoryAsync(cbTicker, startDate, endDate);

        var result = new CBTrackingHistoryDto
        {
            CBTicker = cbTicker,
            History = history.Select(h => new CBDailyTrackingDto
            {
                TradeDate = h.TradeDate,
                UnderlyingClosePrice = h.UnderlyingClosePrice,
                PriceRatio = h.PriceToConversionRatio,
                IsAboveTrigger = h.IsAboveTrigger ?? false,
                ConsecutiveDays = h.ConsecutiveDaysAbove,
                OutstandingBalance = h.OutstandingBalance,
                WarningLevel = h.WarningLevel
            }).ToList()
        };

        return Ok(result);
    }

    #region 私有方法

    private static CBWarningDto MapToWarningDto(CBWarningData data)
    {
        var daysRemaining = Math.Max(0, data.RedemptionTriggerDays - data.ConsecutiveDaysAbove);
        var triggerProgress = CalculateTriggerProgress(data.ConsecutiveDaysAbove, data.RedemptionTriggerDays);

        return new CBWarningDto
        {
            CBTicker = data.CBTicker,
            UnderlyingTicker = data.UnderlyingTicker,
            CBName = data.CBName,
            TradeDate = data.TradeDate,
            CurrentPrice = data.UnderlyingClosePrice ?? 0,
            ConversionPrice = data.ConversionPrice ?? 0,
            PriceRatio = data.PriceToConversionRatio ?? 0,
            IsAboveTrigger = data.IsAboveTrigger ?? false,
            ConsecutiveDays = data.ConsecutiveDaysAbove,
            DaysRemaining = daysRemaining,
            TriggerProgress = triggerProgress,
            OutstandingBalance = data.OutstandingBalance ?? 0,
            TotalIssueAmount = data.TotalIssueAmount,
            BalanceChangePercent = data.BalanceChangePercent,
            WarningLevel = data.WarningLevel ?? "SAFE",
            Comment = GenerateComment(
                data.ConsecutiveDaysAbove,
                daysRemaining,
                data.PriceToConversionRatio ?? 0,
                data.OutstandingBalance ?? 0,
                data.WarningLevel ?? "SAFE"),
            MaturityDate = data.MaturityDate
        };
    }

    private static decimal CalculateTriggerProgress(int consecutiveDays, int triggerDays)
    {
        if (triggerDays <= 0) return 0;
        return Math.Min(100, (decimal)consecutiveDays / triggerDays * 100);
    }

    private static string GenerateComment(
        int consecutiveDays,
        int daysRemaining,
        decimal priceRatio,
        decimal outstandingBalance,
        string warningLevel)
    {
        return warningLevel switch
        {
            "CRITICAL" => $"已達強贖門檻！連續 {consecutiveDays} 日超過轉換價 130%，剩餘 {outstandingBalance:F2} 億可能面臨轉換壓力",
            "WARNING" => $"高度警戒：已連續 {consecutiveDays} 日，僅剩 {daysRemaining} 日即觸發強贖，餘額 {outstandingBalance:F2} 億",
            "CAUTION" => $"注意追蹤：連續 {consecutiveDays} 日超標，股價/轉換價 = {priceRatio:F1}%",
            _ => $"安全範圍：股價/轉換價 = {priceRatio:F1}%，無近期強贖風險"
        };
    }

    #endregion
}
