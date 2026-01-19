using AlphaSqueeze.Api.Models;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;
using AlphaSqueeze.Shared.Grpc;
using AlphaSqueeze.Shared.Services;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;

namespace AlphaSqueeze.Api.Controllers;

/// <summary>
/// 軋空訊號 API
///
/// 提供軋空潛力分析、候選排行等功能。
/// 當 Python 引擎不可用時，自動進入降級模式。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SqueezeController : ControllerBase
{
    private readonly ISqueezeEngineClient _engineClient;
    private readonly IStockMetricsRepository _metricsRepo;
    private readonly ILogger<SqueezeController> _logger;

    public SqueezeController(
        ISqueezeEngineClient engineClient,
        IStockMetricsRepository metricsRepo,
        ILogger<SqueezeController> logger)
    {
        _engineClient = engineClient;
        _metricsRepo = metricsRepo;
        _logger = logger;
    }

    /// <summary>
    /// 取得今日軋空潛力排行
    /// </summary>
    /// <param name="limit">返回數量上限 (預設 10)</param>
    /// <param name="minScore">最低分數門檻 (預設 60)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>軋空候選清單</returns>
    /// <response code="200">成功返回候選清單</response>
    /// <response code="503">服務暫時不可用</response>
    [HttpGet("top-candidates")]
    [ProducesResponseType(typeof(TopCandidatesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetTopCandidates(
        [FromQuery] int limit = 10,
        [FromQuery] int minScore = 60,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Requesting top {Limit} candidates with min score {MinScore}",
            limit, minScore);

        if (!_engineClient.IsAvailable)
        {
            _logger.LogWarning("Engine unavailable, returning degraded response");
            return await GetDegradedTopCandidates(limit, ct);
        }

        try
        {
            var request = new TopCandidatesRequest
            {
                Limit = limit,
                MinScore = minScore,
                Date = DateTime.Today.ToString("yyyy-MM-dd")
            };

            var response = await _engineClient.GetTopCandidatesAsync(request, ct);

            var result = new TopCandidatesDto
            {
                Candidates = response.Candidates.Select(MapToDto).ToList(),
                AnalysisDate = response.AnalysisDate,
                GeneratedAt = response.GeneratedAt
            };

            _logger.LogInformation("Returning {Count} candidates", result.Candidates.Count);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed, falling back to degraded mode");
            return await GetDegradedTopCandidates(limit, ct);
        }
    }

    /// <summary>
    /// 取得單一標的軋空分析
    /// </summary>
    /// <param name="ticker">股票代號</param>
    /// <param name="date">分析日期 (預設今日)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>軋空訊號詳情</returns>
    /// <response code="200">成功返回分析結果</response>
    /// <response code="404">找不到該標的資料</response>
    [HttpGet("{ticker}")]
    [ProducesResponseType(typeof(SqueezeSignalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSqueezeSignal(
        string ticker,
        [FromQuery] DateTime? date = null,
        CancellationToken ct = default)
    {
        var targetDate = date ?? DateTime.Today;
        ticker = ticker.ToUpperInvariant();

        _logger.LogInformation("Requesting squeeze signal for {Ticker} on {Date}",
            ticker, targetDate.ToString("yyyy-MM-dd"));

        // 從資料庫取得股票指標
        var metric = await _metricsRepo.GetByTickerAndDateAsync(ticker, targetDate);
        if (metric == null)
        {
            _logger.LogWarning("No data found for {Ticker} on {Date}", ticker, targetDate);
            return NotFound(new ErrorResponse
            {
                Message = $"找不到 {ticker} 於 {targetDate:yyyy-MM-dd} 的資料",
                ErrorCode = "NOT_FOUND"
            });
        }

        // 如果引擎不可用，返回降級訊號
        if (!_engineClient.IsAvailable)
        {
            _logger.LogWarning("Engine unavailable for {Ticker}", ticker);
            return Ok(CreateDegradedSignal(metric));
        }

        try
        {
            var request = new SqueezeRequest
            {
                Ticker = ticker,
                BorrowChange = metric.BorrowingBalanceChange ?? 0,
                MarginRatio = (double)(metric.MarginRatio ?? 0),
                Hv20D = (double)(metric.HistoricalVolatility20D ?? 0),
                ClosePrice = (double)(metric.ClosePrice ?? 0),
                Volume = metric.Volume ?? 0
            };

            var response = await _engineClient.GetSqueezeSignalAsync(request, ct);
            return Ok(MapToDto(response));
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Ticker}, returning degraded signal", ticker);
            return Ok(CreateDegradedSignal(metric));
        }
    }

    /// <summary>
    /// 批量分析多個標的
    /// </summary>
    /// <param name="tickers">股票代號列表 (逗號分隔)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>批量分析結果</returns>
    [HttpGet("batch")]
    [ProducesResponseType(typeof(List<SqueezeSignalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatchSignals(
        [FromQuery] string tickers,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tickers))
        {
            return BadRequest(new ErrorResponse
            {
                Message = "請提供股票代號",
                ErrorCode = "MISSING_TICKERS"
            });
        }

        var tickerList = tickers.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (tickerList.Count > 50)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "一次最多查詢 50 個標的",
                ErrorCode = "TOO_MANY_TICKERS"
            });
        }

        _logger.LogInformation("Batch request for {Count} tickers", tickerList.Count);

        var results = new List<SqueezeSignalDto>();

        foreach (var ticker in tickerList)
        {
            var metric = await _metricsRepo.GetByTickerAndDateAsync(ticker, DateTime.Today);
            if (metric == null) continue;

            if (!_engineClient.IsAvailable)
            {
                results.Add(CreateDegradedSignal(metric));
                continue;
            }

            try
            {
                var request = new SqueezeRequest
                {
                    Ticker = ticker,
                    BorrowChange = metric.BorrowingBalanceChange ?? 0,
                    MarginRatio = (double)(metric.MarginRatio ?? 0),
                    Hv20D = (double)(metric.HistoricalVolatility20D ?? 0),
                    ClosePrice = (double)(metric.ClosePrice ?? 0),
                    Volume = metric.Volume ?? 0
                };

                var response = await _engineClient.GetSqueezeSignalAsync(request, ct);
                results.Add(MapToDto(response));
            }
            catch (RpcException)
            {
                results.Add(CreateDegradedSignal(metric));
            }
        }

        return Ok(results.OrderByDescending(r => r.Score).ToList());
    }

    /// <summary>
    /// 取得系統健康狀態
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            Status = _engineClient.IsAvailable ? "Healthy" : "Degraded",
            EngineAvailable = _engineClient.IsAvailable,
            Timestamp = DateTime.Now.ToString("o")
        });
    }

    #region 私有方法

    private async Task<IActionResult> GetDegradedTopCandidates(int limit, CancellationToken ct)
    {
        // 降級模式：直接從資料庫取得高券資比標的
        var metrics = await _metricsRepo.GetByDateAsync(DateTime.Today);
        var topByMarginRatio = metrics
            .Where(m => m.MarginRatio > 10)
            .OrderByDescending(m => m.MarginRatio)
            .Take(limit)
            .Select(m => new SqueezeSignalDto
            {
                Ticker = m.Ticker,
                Score = 0, // 無法計算
                Trend = "DEGRADED",
                Comment = $"量化引擎暫時無法連線，僅顯示基本籌碼數據。券資比: {m.MarginRatio:F2}%",
                Factors = null
            })
            .ToList();

        _logger.LogInformation("Returning {Count} degraded candidates", topByMarginRatio.Count);

        return Ok(new TopCandidatesDto
        {
            Candidates = topByMarginRatio,
            AnalysisDate = DateTime.Today.ToString("yyyy-MM-dd"),
            GeneratedAt = DateTime.Now.ToString("o")
        });
    }

    private static SqueezeSignalDto CreateDegradedSignal(DailyStockMetric metric)
    {
        return new SqueezeSignalDto
        {
            Ticker = metric.Ticker,
            Score = 0,
            Trend = "DEGRADED",
            Comment = $"量化引擎暫時無法連線。券資比: {metric.MarginRatio:F2}%, 借券變化: {metric.BorrowingBalanceChange:+#;-#;0}",
            Factors = null
        };
    }

    private static SqueezeSignalDto MapToDto(SqueezeResponse response)
    {
        return new SqueezeSignalDto
        {
            Ticker = response.Ticker,
            Score = response.Score,
            Trend = response.Trend,
            Comment = response.Comment,
            Factors = response.Factors != null ? new FactorScoresDto
            {
                BorrowScore = response.Factors.BorrowScore,
                GammaScore = response.Factors.GammaScore,
                MarginScore = response.Factors.MarginScore,
                MomentumScore = response.Factors.MomentumScore
            } : null
        };
    }

    #endregion
}
