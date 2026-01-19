using AlphaSqueeze.Api.Models;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AlphaSqueeze.Api.Controllers;

/// <summary>
/// 股票指標 API
///
/// 提供股票每日籌碼數據的查詢功能。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MetricsController : ControllerBase
{
    private readonly IStockMetricsRepository _repo;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        IStockMetricsRepository repo,
        ILogger<MetricsController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// 取得指定日期的所有股票指標
    /// </summary>
    /// <param name="date">交易日期 (預設今日)</param>
    /// <returns>股票指標列表</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StockMetricDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByDate([FromQuery] DateTime? date = null)
    {
        var targetDate = date ?? DateTime.Today;
        _logger.LogInformation("Fetching metrics for {Date}", targetDate.ToString("yyyy-MM-dd"));

        var metrics = await _repo.GetByDateAsync(targetDate);
        var result = metrics.Select(MapToDto).ToList();

        _logger.LogInformation("Returning {Count} metrics for {Date}",
            result.Count, targetDate.ToString("yyyy-MM-dd"));

        return Ok(result);
    }

    /// <summary>
    /// 取得單一標的指標
    /// </summary>
    /// <param name="ticker">股票代號</param>
    /// <param name="date">交易日期 (預設今日)</param>
    /// <returns>股票指標</returns>
    [HttpGet("{ticker}")]
    [ProducesResponseType(typeof(StockMetricDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByTicker(
        string ticker,
        [FromQuery] DateTime? date = null)
    {
        var targetDate = date ?? DateTime.Today;
        ticker = ticker.ToUpperInvariant();

        var metric = await _repo.GetByTickerAndDateAsync(ticker, targetDate);
        if (metric == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = $"找不到 {ticker} 於 {targetDate:yyyy-MM-dd} 的資料",
                ErrorCode = "NOT_FOUND"
            });
        }

        return Ok(MapToDto(metric));
    }

    /// <summary>
    /// 取得單一標的歷史資料
    /// </summary>
    /// <param name="ticker">股票代號</param>
    /// <param name="days">查詢天數 (預設 30)</param>
    /// <returns>歷史資料列表</returns>
    [HttpGet("{ticker}/history")]
    [ProducesResponseType(typeof(IEnumerable<StockMetricDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        string ticker,
        [FromQuery] int days = 30)
    {
        ticker = ticker.ToUpperInvariant();

        if (days < 1 || days > 365)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "天數必須介於 1-365 之間",
                ErrorCode = "INVALID_DAYS"
            });
        }

        var endDate = DateTime.Today;
        var startDate = endDate.AddDays(-days);

        _logger.LogInformation("Fetching {Days} days history for {Ticker}", days, ticker);

        var history = await _repo.GetHistoryAsync(ticker, startDate, endDate);
        var result = history.Select(MapToDto).ToList();

        _logger.LogInformation("Returning {Count} history records for {Ticker}",
            result.Count, ticker);

        return Ok(result);
    }

    /// <summary>
    /// 取得高券資比標的
    /// </summary>
    /// <param name="date">交易日期 (預設今日)</param>
    /// <param name="minRatio">最低券資比門檻 (預設 10%)</param>
    /// <param name="limit">返回數量上限 (預設 20)</param>
    /// <returns>高券資比標的列表</returns>
    [HttpGet("high-margin-ratio")]
    [ProducesResponseType(typeof(IEnumerable<StockMetricDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHighMarginRatio(
        [FromQuery] DateTime? date = null,
        [FromQuery] decimal minRatio = 10m,
        [FromQuery] int limit = 20)
    {
        var targetDate = date ?? DateTime.Today;

        var metrics = await _repo.GetByDateAsync(targetDate);
        var result = metrics
            .Where(m => m.MarginRatio >= minRatio)
            .OrderByDescending(m => m.MarginRatio)
            .Take(limit)
            .Select(MapToDto)
            .ToList();

        _logger.LogInformation(
            "Returning {Count} stocks with margin ratio >= {MinRatio}%",
            result.Count, minRatio);

        return Ok(result);
    }

    /// <summary>
    /// 取得大量回補標的 (借券餘額減少)
    /// </summary>
    /// <param name="date">交易日期 (預設今日)</param>
    /// <param name="limit">返回數量上限 (預設 20)</param>
    /// <returns>大量回補標的列表</returns>
    [HttpGet("short-covering")]
    [ProducesResponseType(typeof(IEnumerable<StockMetricDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShortCovering(
        [FromQuery] DateTime? date = null,
        [FromQuery] int limit = 20)
    {
        var targetDate = date ?? DateTime.Today;

        var metrics = await _repo.GetByDateAsync(targetDate);
        var result = metrics
            .Where(m => m.BorrowingBalanceChange < 0)
            .OrderBy(m => m.BorrowingBalanceChange) // 最負的在前（回補最多）
            .Take(limit)
            .Select(MapToDto)
            .ToList();

        _logger.LogInformation("Returning {Count} stocks with short covering", result.Count);

        return Ok(result);
    }

    #region 私有方法

    private static StockMetricDto MapToDto(DailyStockMetric m) => new()
    {
        Ticker = m.Ticker,
        TradeDate = m.TradeDate,
        ClosePrice = m.ClosePrice,
        OpenPrice = m.OpenPrice,
        HighPrice = m.HighPrice,
        LowPrice = m.LowPrice,
        BorrowingBalanceChange = m.BorrowingBalanceChange,
        MarginRatio = m.MarginRatio,
        HistoricalVolatility20D = m.HistoricalVolatility20D,
        Volume = m.Volume
    };

    #endregion
}
