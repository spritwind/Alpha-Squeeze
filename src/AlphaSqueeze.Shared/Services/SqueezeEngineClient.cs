using AlphaSqueeze.Shared.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AlphaSqueeze.Shared.Services;

/// <summary>
/// gRPC 客戶端實作 - 連接 Python Squeeze Engine
///
/// 功能：
/// - 自動處理連線失敗並進入降級模式
/// - 提供重試機制
/// - 記錄詳細日誌
/// </summary>
public class SqueezeEngineClient : ISqueezeEngineClient
{
    private readonly SqueezeEngine.SqueezeEngineClient _client;
    private readonly ILogger<SqueezeEngineClient> _logger;
    private volatile bool _isAvailable = true;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private readonly TimeSpan _recoveryCheckInterval = TimeSpan.FromMinutes(1);

    public SqueezeEngineClient(
        SqueezeEngine.SqueezeEngineClient client,
        ILogger<SqueezeEngineClient> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// gRPC 伺服器是否可用
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            // 如果之前失敗，定期重新檢查
            if (!_isAvailable && DateTime.Now - _lastFailureTime > _recoveryCheckInterval)
            {
                _logger.LogInformation("Attempting to recover gRPC connection...");
                return true; // 嘗試重新連接
            }
            return _isAvailable;
        }
    }

    /// <summary>
    /// 取得單一標的軋空訊號
    /// </summary>
    public async Task<SqueezeResponse> GetSqueezeSignalAsync(
        SqueezeRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            _logger.LogDebug("Requesting squeeze signal for {Ticker}", request.Ticker);

            var response = await _client.GetSqueezeSignalAsync(
                request,
                cancellationToken: ct);

            MarkAsAvailable();

            _logger.LogDebug(
                "Received squeeze signal for {Ticker}: Score={Score}, Trend={Trend}",
                response.Ticker, response.Score, response.Trend);

            return response;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            MarkAsUnavailable();
            _logger.LogWarning(
                "gRPC server unavailable, entering degraded mode. Error: {Message}",
                ex.Message);
            throw;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex,
                "gRPC call failed for {Ticker}: {StatusCode} - {Message}",
                request.Ticker, ex.StatusCode, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 批量取得多個標的軋空訊號
    /// </summary>
    public async Task<BatchSqueezeResponse> GetBatchSignalsAsync(
        BatchSqueezeRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            _logger.LogDebug("Requesting batch signals for {Count} tickers",
                request.Requests.Count);

            var response = await _client.GetBatchSignalsAsync(
                request,
                cancellationToken: ct);

            MarkAsAvailable();

            _logger.LogDebug("Received {Count} batch signal responses",
                response.Responses.Count);

            return response;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            MarkAsUnavailable();
            _logger.LogWarning("gRPC server unavailable during batch request");
            throw;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Batch signals request failed: {StatusCode}", ex.StatusCode);
            MarkAsUnavailable();
            throw;
        }
    }

    /// <summary>
    /// 取得當日熱門軋空候選標的
    /// </summary>
    public async Task<TopCandidatesResponse> GetTopCandidatesAsync(
        TopCandidatesRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            _logger.LogDebug(
                "Requesting top candidates: Date={Date}, Limit={Limit}, MinScore={MinScore}",
                request.Date, request.Limit, request.MinScore);

            var response = await _client.GetTopCandidatesAsync(
                request,
                cancellationToken: ct);

            MarkAsAvailable();

            _logger.LogDebug(
                "Received {Count} top candidates for {Date}",
                response.Candidates.Count, response.AnalysisDate);

            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex,
                "Top candidates request failed for {Date}: {StatusCode}",
                request.Date, ex.StatusCode);

            if (ex.StatusCode == StatusCode.Unavailable)
            {
                MarkAsUnavailable();
            }
            throw;
        }
    }

    private void MarkAsAvailable()
    {
        if (!_isAvailable)
        {
            _logger.LogInformation("gRPC connection recovered");
        }
        _isAvailable = true;
    }

    private void MarkAsUnavailable()
    {
        _isAvailable = false;
        _lastFailureTime = DateTime.Now;
    }
}
