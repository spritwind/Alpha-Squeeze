using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AlphaSqueeze.Api.Controllers;

/// <summary>
/// 雷達掃描 Discovery API
/// 提供全市場掃描結果與用戶追蹤清單管理
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DiscoveryController : ControllerBase
{
    private readonly IDiscoveryRepository _discoveryRepo;
    private readonly IUserWatchListRepository _watchListRepo;
    private readonly ILogger<DiscoveryController> _logger;

    public DiscoveryController(
        IDiscoveryRepository discoveryRepo,
        IUserWatchListRepository watchListRepo,
        ILogger<DiscoveryController> logger)
    {
        _discoveryRepo = discoveryRepo;
        _watchListRepo = watchListRepo;
        _logger = logger;
    }

    #region Discovery Pool

    /// <summary>
    /// 取得最新掃描結果
    /// </summary>
    [HttpGet("pool")]
    [ProducesResponseType(typeof(DiscoveryPoolResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLatestPool([FromQuery] int limit = 100)
    {
        var results = await _discoveryRepo.GetLatestAsync(limit);
        var resultList = results.ToList();

        return Ok(new DiscoveryPoolResponse
        {
            Items = resultList.Select(MapToDto).ToList(),
            ScanDate = resultList.FirstOrDefault()?.ScanDate.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd"),
            TotalCount = resultList.Count
        });
    }

    /// <summary>
    /// 依條件篩選掃描結果
    /// </summary>
    [HttpGet("pool/filter")]
    [ProducesResponseType(typeof(DiscoveryPoolResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> FilterPool([FromQuery] DiscoveryFilterRequest request)
    {
        var results = await _discoveryRepo.FilterAsync(
            scanDate: request.ScanDate,
            minShortRatio: request.MinShortRatio,
            minVolMultiplier: request.MinVolMultiplier,
            minPrice: request.MinPrice,
            minVolume: request.MinVolume,
            hasCB: request.HasCB,
            minScore: request.MinScore,
            limit: request.Limit);

        var resultList = results.ToList();

        return Ok(new DiscoveryPoolResponse
        {
            Items = resultList.Select(MapToDto).ToList(),
            ScanDate = resultList.FirstOrDefault()?.ScanDate.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd"),
            TotalCount = resultList.Count
        });
    }

    /// <summary>
    /// 取得掃描參數配置
    /// </summary>
    [HttpGet("config")]
    [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig()
    {
        var config = await _discoveryRepo.GetConfigAsync();
        return Ok(config);
    }

    /// <summary>
    /// 更新掃描參數配置
    /// </summary>
    [HttpPut("config/{key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateConfig(string key, [FromBody] DiscoveryConfigUpdateRequest request)
    {
        await _discoveryRepo.UpdateConfigAsync(key, request.Value);
        _logger.LogInformation("Discovery config updated: {Key} = {Value}", key, request.Value);
        return NoContent();
    }

    #endregion

    #region User Watch List

    /// <summary>
    /// 取得用戶追蹤清單
    /// </summary>
    [HttpGet("watchlist")]
    [ProducesResponseType(typeof(IEnumerable<UserWatchListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWatchList()
    {
        var items = await _watchListRepo.GetAllAsync();
        return Ok(items.Select(MapToDto));
    }

    /// <summary>
    /// 取得啟用中的追蹤清單
    /// </summary>
    [HttpGet("watchlist/active")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveWatchList()
    {
        var tickers = await _watchListRepo.GetActiveTickersAsync();
        return Ok(tickers);
    }

    /// <summary>
    /// 新增至追蹤清單
    /// </summary>
    [HttpPost("watchlist")]
    [ProducesResponseType(typeof(UserWatchListDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddToWatchList([FromBody] AddWatchListRequest request)
    {
        var item = new UserWatchList
        {
            Ticker = request.Ticker.ToUpperInvariant(),
            TickerName = request.TickerName,
            AddedBy = "WebUI",
            Priority = request.Priority ?? 100,
            Notes = request.Notes
        };

        var success = await _watchListRepo.AddAsync(item);
        if (!success)
        {
            return BadRequest(new { message = $"股票 {request.Ticker} 已在追蹤清單中" });
        }

        var created = await _watchListRepo.GetByTickerAsync(item.Ticker);
        _logger.LogInformation("Added to watchlist: {Ticker}", item.Ticker);
        return CreatedAtAction(nameof(GetWatchList), MapToDto(created!));
    }

    /// <summary>
    /// 批量新增至追蹤清單
    /// </summary>
    [HttpPost("watchlist/bulk")]
    [ProducesResponseType(typeof(BulkAddResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> BulkAddToWatchList([FromBody] BulkAddWatchListRequest request)
    {
        var count = await _watchListRepo.BulkAddAsync(request.Tickers);
        _logger.LogInformation("Bulk added {Count} tickers to watchlist", count);

        return Ok(new BulkAddResponse
        {
            AddedCount = count,
            RequestedCount = request.Tickers.Count
        });
    }

    /// <summary>
    /// 更新追蹤項目
    /// </summary>
    [HttpPut("watchlist/{ticker}")]
    [ProducesResponseType(typeof(UserWatchListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWatchListItem(string ticker, [FromBody] UpdateWatchListRequest request)
    {
        var existing = await _watchListRepo.GetByTickerAsync(ticker);
        if (existing == null)
        {
            return NotFound(new { message = $"找不到股票 {ticker}" });
        }

        existing.TickerName = request.TickerName ?? existing.TickerName;
        existing.Priority = request.Priority ?? existing.Priority;
        existing.Notes = request.Notes ?? existing.Notes;
        existing.IsActive = request.IsActive ?? existing.IsActive;

        await _watchListRepo.UpdateAsync(existing);
        return Ok(MapToDto(existing));
    }

    /// <summary>
    /// 設定追蹤項目啟用狀態
    /// </summary>
    [HttpPatch("watchlist/{ticker}/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetWatchListActive(string ticker, [FromQuery] bool active)
    {
        var success = await _watchListRepo.SetActiveAsync(ticker, active);
        if (!success)
        {
            return NotFound(new { message = $"找不到股票 {ticker}" });
        }
        return NoContent();
    }

    /// <summary>
    /// 從追蹤清單移除
    /// </summary>
    [HttpDelete("watchlist/{ticker}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFromWatchList(string ticker)
    {
        var success = await _watchListRepo.RemoveAsync(ticker);
        if (!success)
        {
            return NotFound(new { message = $"找不到股票 {ticker}" });
        }
        _logger.LogInformation("Removed from watchlist: {Ticker}", ticker);
        return NoContent();
    }

    #endregion

    #region Private Methods

    private static DiscoveryPoolDto MapToDto(DiscoveryPool item) => new()
    {
        Ticker = item.Ticker,
        TickerName = item.TickerName,
        Industry = item.Industry,
        ClosePrice = item.ClosePrice,
        Volume = item.Volume,
        VolMultiplier = item.VolMultiplier,
        ShortRatio = item.ShortRatio,
        MarginRatio = item.MarginRatio,
        HasCB = item.HasCB,
        CBTicker = item.CBTicker,
        CBPriceRatio = item.CBPriceRatio,
        SqueezeScore = item.SqueezeScore,
        ScanDate = item.ScanDate.ToString("yyyy-MM-dd")
    };

    private static UserWatchListDto MapToDto(UserWatchList item) => new()
    {
        Ticker = item.Ticker,
        TickerName = item.TickerName,
        AddedTime = item.AddedTime,
        AddedBy = item.AddedBy,
        IsActive = item.IsActive,
        Priority = item.Priority,
        LastDeepScrapedTime = item.LastDeepScrapedTime,
        LastSqueezeScore = item.LastSqueezeScore,
        Notes = item.Notes
    };

    #endregion
}

#region DTOs

public class DiscoveryPoolResponse
{
    public List<DiscoveryPoolDto> Items { get; set; } = new();
    public string ScanDate { get; set; } = string.Empty;
    public int TotalCount { get; set; }
}

public class DiscoveryPoolDto
{
    public string Ticker { get; set; } = string.Empty;
    public string? TickerName { get; set; }
    public string? Industry { get; set; }
    public decimal? ClosePrice { get; set; }
    public long? Volume { get; set; }
    public decimal? VolMultiplier { get; set; }
    public decimal? ShortRatio { get; set; }
    public decimal? MarginRatio { get; set; }
    public bool HasCB { get; set; }
    public string? CBTicker { get; set; }
    public decimal? CBPriceRatio { get; set; }
    public int? SqueezeScore { get; set; }
    public string ScanDate { get; set; } = string.Empty;
}

public class DiscoveryFilterRequest
{
    public DateTime? ScanDate { get; set; }
    public decimal? MinShortRatio { get; set; }
    public decimal? MinVolMultiplier { get; set; }
    public decimal? MinPrice { get; set; }
    public long? MinVolume { get; set; }
    public bool? HasCB { get; set; }
    public int? MinScore { get; set; }
    public int Limit { get; set; } = 100;
}

public class DiscoveryConfigUpdateRequest
{
    public string Value { get; set; } = string.Empty;
}

public class UserWatchListDto
{
    public string Ticker { get; set; } = string.Empty;
    public string? TickerName { get; set; }
    public DateTime AddedTime { get; set; }
    public string AddedBy { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public DateTime? LastDeepScrapedTime { get; set; }
    public int? LastSqueezeScore { get; set; }
    public string? Notes { get; set; }
}

public class AddWatchListRequest
{
    public string Ticker { get; set; } = string.Empty;
    public string? TickerName { get; set; }
    public int? Priority { get; set; }
    public string? Notes { get; set; }
}

public class BulkAddWatchListRequest
{
    public List<string> Tickers { get; set; } = new();
}

public class BulkAddResponse
{
    public int AddedCount { get; set; }
    public int RequestedCount { get; set; }
}

public class UpdateWatchListRequest
{
    public string? TickerName { get; set; }
    public int? Priority { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
}

#endregion
