using AlphaSqueeze.Api.Models;
using AlphaSqueeze.Shared.Grpc;
using AlphaSqueeze.Shared.Services;

namespace AlphaSqueeze.Api.Services;

/// <summary>
/// 每日軋空訊號推播服務
///
/// 功能：
/// - 每日固定時間發送軋空訊號
/// - 自動重試機制
/// - 降級模式支援
/// </summary>
public class DailyAlertService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyAlertService> _logger;
    private readonly TimeOnly _alertTime;
    private readonly bool _isEnabled;

    public DailyAlertService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<DailyAlertService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 從設定檔讀取推播時間，預設 20:00
        var timeString = configuration["DailyAlert:Time"] ?? "20:00";
        _alertTime = TimeOnly.Parse(timeString);

        // 是否啟用每日推播
        _isEnabled = configuration.GetValue("DailyAlert:Enabled", true);

        _logger.LogInformation(
            "DailyAlertService initialized. Enabled: {Enabled}, AlertTime: {Time}",
            _isEnabled, _alertTime);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_isEnabled)
        {
            _logger.LogInformation("DailyAlertService is disabled");
            return;
        }

        _logger.LogInformation("DailyAlertService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = CalculateDelayUntilNextAlert();
                var nextAlertTime = DateTime.Now.Add(delay);

                _logger.LogInformation(
                    "Next daily alert scheduled at {NextAlertTime}",
                    nextAlertTime.ToString("yyyy-MM-dd HH:mm:ss"));

                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await SendDailyAlertAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("DailyAlertService is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DailyAlertService main loop");
                // 發生錯誤時等待 5 分鐘後重試
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("DailyAlertService stopped");
    }

    /// <summary>
    /// 計算到下次推播的延遲時間
    /// </summary>
    private TimeSpan CalculateDelayUntilNextAlert()
    {
        var now = DateTime.Now;
        var todayAlert = new DateTime(
            now.Year, now.Month, now.Day,
            _alertTime.Hour, _alertTime.Minute, 0);

        // 如果今天的推播時間已過，排程到明天
        if (now > todayAlert)
        {
            todayAlert = todayAlert.AddDays(1);
        }

        return todayAlert - now;
    }

    /// <summary>
    /// 發送每日軋空推播
    /// </summary>
    private async Task SendDailyAlertAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting daily alert...");

        using var scope = _serviceProvider.CreateScope();

        var engineClient = scope.ServiceProvider.GetRequiredService<ISqueezeEngineClient>();
        var lineNotify = scope.ServiceProvider.GetRequiredService<ILineNotifyService>();

        try
        {
            List<SqueezeSignalDto> candidates;

            if (engineClient.IsAvailable)
            {
                // 正常模式：從量化引擎取得候選標的
                candidates = await GetCandidatesFromEngineAsync(engineClient, ct);
            }
            else
            {
                // 降級模式：從資料庫取得高券資比標的
                _logger.LogWarning("Engine unavailable, using degraded mode for daily alert");
                candidates = await GetDegradedCandidatesAsync(scope.ServiceProvider, ct);
            }

            if (candidates.Count == 0)
            {
                _logger.LogInformation("No candidates found for daily alert");
                return;
            }

            await lineNotify.SendSqueezeAlertAsync(candidates, ct);

            _logger.LogInformation(
                "Daily alert sent successfully with {Count} candidates",
                candidates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send daily alert");
        }
    }

    /// <summary>
    /// 從量化引擎取得候選標的
    /// </summary>
    private async Task<List<SqueezeSignalDto>> GetCandidatesFromEngineAsync(
        ISqueezeEngineClient engineClient,
        CancellationToken ct)
    {
        var request = new TopCandidatesRequest
        {
            Limit = 10,
            MinScore = 60,
            Date = DateTime.Today.ToString("yyyy-MM-dd")
        };

        try
        {
            var response = await engineClient.GetTopCandidatesAsync(request, ct);

            return response.Candidates.Select(c => new SqueezeSignalDto
            {
                Ticker = c.Ticker,
                Score = c.Score,
                Trend = c.Trend,
                Comment = c.Comment,
                Factors = c.Factors != null ? new FactorScoresDto
                {
                    BorrowScore = c.Factors.BorrowScore,
                    GammaScore = c.Factors.GammaScore,
                    MarginScore = c.Factors.MarginScore,
                    MomentumScore = c.Factors.MomentumScore
                } : null
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get candidates from engine");
            return new List<SqueezeSignalDto>();
        }
    }

    /// <summary>
    /// 降級模式：從資料庫取得高券資比標的
    /// </summary>
    private async Task<List<SqueezeSignalDto>> GetDegradedCandidatesAsync(
        IServiceProvider serviceProvider,
        CancellationToken ct)
    {
        try
        {
            var metricsRepo = serviceProvider.GetRequiredService<Core.Interfaces.IStockMetricsRepository>();
            var metrics = await metricsRepo.GetByDateAsync(DateTime.Today);

            return metrics
                .Where(m => m.MarginRatio > 10)
                .OrderByDescending(m => m.MarginRatio)
                .Take(10)
                .Select(m => new SqueezeSignalDto
                {
                    Ticker = m.Ticker,
                    Score = 0,
                    Trend = "DEGRADED",
                    Comment = $"量化引擎離線。券資比: {m.MarginRatio:F2}%, 借券變化: {m.BorrowingBalanceChange:+#;-#;0}",
                    Factors = null
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get degraded candidates from database");
            return new List<SqueezeSignalDto>();
        }
    }
}
