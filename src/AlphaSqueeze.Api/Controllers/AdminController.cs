using AlphaSqueeze.Api.Models;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AlphaSqueeze.Api.Controllers;

/// <summary>
/// 管理功能 API
///
/// 提供資料回補、追蹤股票管理等系統管理功能。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IBackfillJobRepository _backfillRepo;
    private readonly ITrackedTickerRepository _tickerRepo;
    private readonly IStockMetricsRepository _metricsRepo;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IBackfillJobRepository backfillRepo,
        ITrackedTickerRepository tickerRepo,
        IStockMetricsRepository metricsRepo,
        ILogger<AdminController> logger)
    {
        _backfillRepo = backfillRepo;
        _tickerRepo = tickerRepo;
        _metricsRepo = metricsRepo;
        _logger = logger;
    }

    #region 資料回補

    /// <summary>
    /// 建立資料回補任務
    /// </summary>
    /// <remarks>
    /// 建立任務後，需由 Python Worker 執行實際的資料抓取工作。
    /// 可透過 GET /api/admin/backfill/{id} 查詢任務進度。
    /// </remarks>
    /// <param name="request">回補請求</param>
    /// <returns>建立的任務資訊</returns>
    [HttpPost("backfill")]
    [ProducesResponseType(typeof(BackfillJobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateBackfillJob([FromBody] CreateBackfillRequest request)
    {
        _logger.LogInformation(
            "Creating backfill job: {StartDate} to {EndDate}, Type={JobType}",
            request.StartDate, request.EndDate, request.JobType);

        // 驗證日期格式
        if (!DateTime.TryParse(request.StartDate, out var startDate))
        {
            return BadRequest(new ErrorResponse
            {
                Message = "開始日期格式無效，請使用 YYYY-MM-DD 格式",
                ErrorCode = "INVALID_START_DATE"
            });
        }

        if (!DateTime.TryParse(request.EndDate, out var endDate))
        {
            return BadRequest(new ErrorResponse
            {
                Message = "結束日期格式無效，請使用 YYYY-MM-DD 格式",
                ErrorCode = "INVALID_END_DATE"
            });
        }

        if (startDate > endDate)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "開始日期不可晚於結束日期",
                ErrorCode = "INVALID_DATE_RANGE"
            });
        }

        // 檢查是否有正在執行的任務
        var runningJobs = await _backfillRepo.GetRunningJobsAsync();
        if (runningJobs.Any())
        {
            return BadRequest(new ErrorResponse
            {
                Message = "已有回補任務正在執行中，請等待完成後再試",
                ErrorCode = "JOB_ALREADY_RUNNING"
            });
        }

        // 取得股票數量
        int totalTickers;
        if (request.Tickers != null && request.Tickers.Count > 0)
        {
            totalTickers = request.Tickers.Count;
        }
        else
        {
            var activeTickers = await _tickerRepo.GetActiveTickersAsync();
            totalTickers = activeTickers.Count();
        }

        if (totalTickers == 0)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "沒有可回補的股票，請先新增追蹤股票",
                ErrorCode = "NO_TICKERS"
            });
        }

        // 建立任務
        var jobId = await _backfillRepo.CreateJobAsync(
            request.JobType,
            startDate,
            endDate,
            totalTickers,
            "API");

        var job = await _backfillRepo.GetByIdAsync(jobId);

        _logger.LogInformation("Created backfill job #{JobId} for {TotalTickers} tickers", jobId, totalTickers);

        return CreatedAtAction(
            nameof(GetBackfillJob),
            new { id = jobId },
            MapToDto(job!));
    }

    /// <summary>
    /// 取得回補任務詳情
    /// </summary>
    /// <param name="id">任務 ID</param>
    /// <returns>任務詳情</returns>
    [HttpGet("backfill/{id}")]
    [ProducesResponseType(typeof(BackfillJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBackfillJob(int id)
    {
        var job = await _backfillRepo.GetByIdAsync(id);

        if (job == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = $"找不到任務 #{id}",
                ErrorCode = "JOB_NOT_FOUND"
            });
        }

        return Ok(MapToDto(job));
    }

    /// <summary>
    /// 取得最近的回補任務列表
    /// </summary>
    /// <param name="limit">返回數量</param>
    /// <returns>任務列表</returns>
    [HttpGet("backfill")]
    [ProducesResponseType(typeof(IEnumerable<BackfillJobDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentBackfillJobs([FromQuery] int limit = 10)
    {
        var jobs = await _backfillRepo.GetRecentJobsAsync(limit);
        return Ok(jobs.Select(MapToDto));
    }

    #endregion

    #region 追蹤股票管理

    /// <summary>
    /// 取得所有追蹤股票
    /// </summary>
    /// <returns>股票列表</returns>
    [HttpGet("tickers")]
    [ProducesResponseType(typeof(IEnumerable<TrackedTickerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrackedTickers()
    {
        var tickers = await _tickerRepo.GetAllAsync();
        return Ok(tickers.Select(MapToDto));
    }

    /// <summary>
    /// 取得啟用中的股票代號列表
    /// </summary>
    /// <returns>股票代號列表</returns>
    [HttpGet("tickers/active")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveTickers()
    {
        var tickers = await _tickerRepo.GetActiveTickersAsync();
        return Ok(tickers);
    }

    /// <summary>
    /// 新增追蹤股票
    /// </summary>
    /// <param name="request">新增請求</param>
    /// <returns>新增結果</returns>
    [HttpPost("tickers")]
    [ProducesResponseType(typeof(TrackedTickerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddTrackedTicker([FromBody] AddTrackedTickerRequest request)
    {
        _logger.LogInformation("Adding tracked ticker: {Ticker}", request.Ticker);

        var ticker = new TrackedTicker
        {
            Ticker = request.Ticker.ToUpperInvariant(),
            TickerName = request.TickerName,
            Category = request.Category,
            IsActive = true,
            Priority = request.Priority,
            Notes = request.Notes
        };

        var success = await _tickerRepo.AddAsync(ticker);

        if (!success)
        {
            return BadRequest(new ErrorResponse
            {
                Message = $"股票 {request.Ticker} 已存在",
                ErrorCode = "TICKER_EXISTS"
            });
        }

        var created = await _tickerRepo.GetByTickerAsync(ticker.Ticker);
        return CreatedAtAction(nameof(GetTrackedTickers), MapToDto(created!));
    }

    /// <summary>
    /// 更新追蹤股票
    /// </summary>
    /// <param name="ticker">股票代號</param>
    /// <param name="request">更新請求</param>
    /// <returns>更新結果</returns>
    [HttpPut("tickers/{ticker}")]
    [ProducesResponseType(typeof(TrackedTickerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTrackedTicker(string ticker, [FromBody] AddTrackedTickerRequest request)
    {
        var existing = await _tickerRepo.GetByTickerAsync(ticker.ToUpperInvariant());

        if (existing == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = $"找不到股票 {ticker}",
                ErrorCode = "TICKER_NOT_FOUND"
            });
        }

        existing.TickerName = request.TickerName ?? existing.TickerName;
        existing.Category = request.Category ?? existing.Category;
        existing.Priority = request.Priority;
        existing.Notes = request.Notes ?? existing.Notes;

        await _tickerRepo.UpdateAsync(existing);

        return Ok(MapToDto(existing));
    }

    /// <summary>
    /// 啟用/停用追蹤股票
    /// </summary>
    /// <param name="ticker">股票代號</param>
    /// <param name="active">是否啟用</param>
    /// <returns>更新結果</returns>
    [HttpPatch("tickers/{ticker}/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetTickerActive(string ticker, [FromQuery] bool active)
    {
        var success = await _tickerRepo.SetActiveAsync(ticker.ToUpperInvariant(), active);

        if (!success)
        {
            return NotFound(new ErrorResponse
            {
                Message = $"找不到股票 {ticker}",
                ErrorCode = "TICKER_NOT_FOUND"
            });
        }

        _logger.LogInformation("Ticker {Ticker} active status set to {Active}", ticker, active);
        return NoContent();
    }

    /// <summary>
    /// 移除追蹤股票
    /// </summary>
    /// <param name="ticker">股票代號</param>
    /// <returns>移除結果</returns>
    [HttpDelete("tickers/{ticker}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTrackedTicker(string ticker)
    {
        var success = await _tickerRepo.RemoveAsync(ticker.ToUpperInvariant());

        if (!success)
        {
            return NotFound(new ErrorResponse
            {
                Message = $"找不到股票 {ticker}",
                ErrorCode = "TICKER_NOT_FOUND"
            });
        }

        _logger.LogInformation("Removed tracked ticker: {Ticker}", ticker);
        return NoContent();
    }

    #endregion

    #region 資料狀態

    /// <summary>
    /// 取得資料狀態摘要
    /// </summary>
    /// <returns>狀態摘要</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(DataStatusSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDataStatus()
    {
        var allTickers = await _tickerRepo.GetAllAsync();
        var tickerList = allTickers.ToList();

        var activeTickers = await _tickerRepo.GetActiveTickersAsync();
        var activeCount = activeTickers.Count();

        var latestMetrics = await _metricsRepo.GetByDateAsync(DateTime.Today);
        var latestDate = latestMetrics.Any() ? DateTime.Today : (DateTime?)null;

        // 如果今天沒資料，找最近有資料的日期
        if (!latestDate.HasValue)
        {
            for (int i = 1; i <= 7; i++)
            {
                var checkDate = DateTime.Today.AddDays(-i);
                var metrics = await _metricsRepo.GetByDateAsync(checkDate);
                if (metrics.Any())
                {
                    latestDate = checkDate;
                    break;
                }
            }
        }

        var recentJobs = await _backfillRepo.GetRecentJobsAsync(1);
        var latestJob = recentJobs.FirstOrDefault();

        return Ok(new DataStatusSummaryDto
        {
            TotalTrackedTickers = tickerList.Count,
            ActiveTickers = activeCount,
            LatestDataDate = latestDate,
            TotalRecords = latestMetrics.Count(),
            LatestBackfillJob = latestJob != null ? MapToDto(latestJob) : null
        });
    }

    #endregion

    #region 私有方法

    private static BackfillJobDto MapToDto(BackfillJob job)
    {
        return new BackfillJobDto
        {
            Id = job.Id,
            JobType = job.JobType,
            StartDate = job.StartDate,
            EndDate = job.EndDate,
            Status = job.Status,
            TotalTickers = job.TotalTickers,
            ProcessedTickers = job.ProcessedTickers,
            FailedTickers = job.FailedTickers,
            ProgressPercent = job.ProgressPercent,
            ErrorMessage = job.ErrorMessage,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            CreatedAt = job.CreatedAt
        };
    }

    private static TrackedTickerDto MapToDto(TrackedTicker ticker)
    {
        return new TrackedTickerDto
        {
            Ticker = ticker.Ticker,
            TickerName = ticker.TickerName,
            Category = ticker.Category,
            IsActive = ticker.IsActive,
            Priority = ticker.Priority,
            AddedAt = ticker.AddedAt,
            Notes = ticker.Notes
        };
    }

    #endregion
}
