# Phase 3: .NET Web API 開發指引

## 目標
建立 ASP.NET Core Web API，整合 gRPC Client 與 Python 引擎通訊，並實作 LINE Notify 推播功能。

## 前置條件
- 已完成 Phase 1 資料層
- 已完成 Phase 2 Python 引擎
- Python gRPC Server 可正常運行

## 開發任務

### Task 3.1: 生成 gRPC Client 程式碼

在 `src/AlphaSqueeze.Shared` 專案中：

```xml
<!-- AlphaSqueeze.Shared.csproj -->
<ItemGroup>
  <Protobuf Include="..\..\proto\squeeze.proto" GrpcServices="Client" />
</ItemGroup>

<ItemGroup>
  <PackageReference Include="Grpc.Net.Client" Version="2.59.0" />
  <PackageReference Include="Google.Protobuf" Version="3.25.1" />
  <PackageReference Include="Grpc.Tools" Version="2.60.0" PrivateAssets="All" />
</ItemGroup>
```

### Task 3.2: 建立 gRPC Client 服務

```csharp
// src/AlphaSqueeze.Shared/Services/SqueezeEngineClient.cs
namespace AlphaSqueeze.Shared.Services;

public interface ISqueezeEngineClient
{
    Task<SqueezeResponse> GetSqueezeSignalAsync(SqueezeRequest request, CancellationToken ct = default);
    Task<BatchSqueezeResponse> GetBatchSignalsAsync(BatchSqueezeRequest request, CancellationToken ct = default);
    Task<TopCandidatesResponse> GetTopCandidatesAsync(TopCandidatesRequest request, CancellationToken ct = default);
    bool IsAvailable { get; }
}

public class SqueezeEngineClient : ISqueezeEngineClient
{
    private readonly SqueezeEngine.SqueezeEngineClient _client;
    private readonly ILogger<SqueezeEngineClient> _logger;
    private bool _isAvailable = true;

    public SqueezeEngineClient(
        SqueezeEngine.SqueezeEngineClient client,
        ILogger<SqueezeEngineClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public bool IsAvailable => _isAvailable;

    public async Task<SqueezeResponse> GetSqueezeSignalAsync(
        SqueezeRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetSqueezeSignalAsync(request, cancellationToken: ct);
            _isAvailable = true;
            return response;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogWarning("gRPC server unavailable, entering degraded mode");
            _isAvailable = false;
            throw;
        }
    }

    public async Task<BatchSqueezeResponse> GetBatchSignalsAsync(
        BatchSqueezeRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetBatchSignalsAsync(request, cancellationToken: ct);
            _isAvailable = true;
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Batch signals request failed");
            _isAvailable = false;
            throw;
        }
    }

    public async Task<TopCandidatesResponse> GetTopCandidatesAsync(
        TopCandidatesRequest request,
        CancellationToken ct = default)
    {
        try
        {
            return await _client.GetTopCandidatesAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Top candidates request failed");
            throw;
        }
    }
}
```

### Task 3.3: 建立 API DTOs

```csharp
// src/AlphaSqueeze.Api/Models/SqueezeModels.cs
namespace AlphaSqueeze.Api.Models;

public record SqueezeSignalDto
{
    public string Ticker { get; init; } = string.Empty;
    public int Score { get; init; }
    public string Trend { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public FactorScoresDto? Factors { get; init; }
}

public record FactorScoresDto
{
    public double BorrowScore { get; init; }
    public double GammaScore { get; init; }
    public double MarginScore { get; init; }
    public double MomentumScore { get; init; }
}

public record TopCandidatesDto
{
    public List<SqueezeSignalDto> Candidates { get; init; } = new();
    public string AnalysisDate { get; init; } = string.Empty;
    public string GeneratedAt { get; init; } = string.Empty;
}

public record StockMetricDto
{
    public string Ticker { get; init; } = string.Empty;
    public DateTime TradeDate { get; init; }
    public decimal? ClosePrice { get; init; }
    public int? BorrowingBalanceChange { get; init; }
    public decimal? MarginRatio { get; init; }
    public decimal? HistoricalVolatility20D { get; init; }
    public long? Volume { get; init; }
}
```

### Task 3.4: 建立 API Controllers

```csharp
// src/AlphaSqueeze.Api/Controllers/SqueezeController.cs
namespace AlphaSqueeze.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    [HttpGet("top-candidates")]
    [ProducesResponseType(typeof(TopCandidatesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetTopCandidates(
        [FromQuery] int limit = 10,
        [FromQuery] int minScore = 60,
        CancellationToken ct = default)
    {
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

            return Ok(new TopCandidatesDto
            {
                Candidates = response.Candidates.Select(MapToDto).ToList(),
                AnalysisDate = response.AnalysisDate,
                GeneratedAt = response.GeneratedAt
            });
        }
        catch (RpcException)
        {
            return await GetDegradedTopCandidates(limit, ct);
        }
    }

    /// <summary>
    /// 取得單一標的軋空分析
    /// </summary>
    [HttpGet("{ticker}")]
    [ProducesResponseType(typeof(SqueezeSignalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSqueezeSignal(
        string ticker,
        CancellationToken ct = default)
    {
        var metric = await _metricsRepo.GetByTickerAndDateAsync(ticker, DateTime.Today);
        if (metric == null)
        {
            return NotFound($"No data found for ticker {ticker}");
        }

        if (!_engineClient.IsAvailable)
        {
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
        catch (RpcException)
        {
            return Ok(CreateDegradedSignal(metric));
        }
    }

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
                Score = 0,  // 無法計算
                Trend = "DEGRADED",
                Comment = "量化引擎暫時無法連線，僅顯示基本籌碼數據",
                Factors = null
            })
            .ToList();

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
            Comment = $"券資比: {metric.MarginRatio:F2}%, 借券變化: {metric.BorrowingBalanceChange}",
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
}
```

### Task 3.5: 建立 Metrics Controller

```csharp
// src/AlphaSqueeze.Api/Controllers/MetricsController.cs
[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IStockMetricsRepository _repo;

    public MetricsController(IStockMetricsRepository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// 取得指定日期的所有股票指標
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetByDate([FromQuery] DateTime? date = null)
    {
        var targetDate = date ?? DateTime.Today;
        var metrics = await _repo.GetByDateAsync(targetDate);
        return Ok(metrics.Select(MapToDto));
    }

    /// <summary>
    /// 取得單一標的歷史資料
    /// </summary>
    [HttpGet("{ticker}/history")]
    public async Task<IActionResult> GetHistory(
        string ticker,
        [FromQuery] int days = 30)
    {
        var endDate = DateTime.Today;
        var startDate = endDate.AddDays(-days);
        var history = await _repo.GetHistoryAsync(ticker, startDate, endDate);
        return Ok(history.Select(MapToDto));
    }

    private static StockMetricDto MapToDto(DailyStockMetric m) => new()
    {
        Ticker = m.Ticker,
        TradeDate = m.TradeDate,
        ClosePrice = m.ClosePrice,
        BorrowingBalanceChange = m.BorrowingBalanceChange,
        MarginRatio = m.MarginRatio,
        HistoricalVolatility20D = m.HistoricalVolatility20D,
        Volume = m.Volume
    };
}
```

### Task 3.6: 實作 LINE Notify 服務

```csharp
// src/AlphaSqueeze.Api/Services/LineNotifyService.cs
namespace AlphaSqueeze.Api.Services;

public interface ILineNotifyService
{
    Task SendSqueezeAlertAsync(IEnumerable<SqueezeSignalDto> candidates, CancellationToken ct = default);
}

public class LineNotifyService : ILineNotifyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LineNotifyService> _logger;
    private readonly string _accessToken;

    private const string LINE_NOTIFY_URL = "https://notify-api.line.me/api/notify";

    public LineNotifyService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<LineNotifyService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _accessToken = configuration["LineNotify:AccessToken"]
            ?? throw new InvalidOperationException("LINE Notify access token not configured");
    }

    public async Task SendSqueezeAlertAsync(
        IEnumerable<SqueezeSignalDto> candidates,
        CancellationToken ct = default)
    {
        var message = FormatAlertMessage(candidates);

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("message", message)
        });

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);

        try
        {
            var response = await _httpClient.PostAsync(LINE_NOTIFY_URL, content, ct);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("LINE Notify sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send LINE Notify");
            throw;
        }
    }

    private static string FormatAlertMessage(IEnumerable<SqueezeSignalDto> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n🚀 Alpha Squeeze 明日軋空清單");
        sb.AppendLine("─────────────────");

        int rank = 1;
        foreach (var c in candidates.Take(5))
        {
            var trendEmoji = c.Trend switch
            {
                "BULLISH" => "🔴",
                "BEARISH" => "🟢",
                _ => "⚪"
            };

            sb.AppendLine($"{rank}. {c.Ticker} (Score: {c.Score}) {trendEmoji}");
            sb.AppendLine($"   {c.Comment}");
            rank++;
        }

        sb.AppendLine("─────────────────");
        sb.AppendLine($"分析時間: {DateTime.Now:HH:mm}");

        return sb.ToString();
    }
}
```

### Task 3.7: 建立 Background Service 排程

```csharp
// src/AlphaSqueeze.Api/Services/DailyAlertService.cs
namespace AlphaSqueeze.Api.Services;

public class DailyAlertService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyAlertService> _logger;

    public DailyAlertService(
        IServiceProvider serviceProvider,
        ILogger<DailyAlertService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var targetTime = new DateTime(now.Year, now.Month, now.Day, 20, 0, 0);

            if (now > targetTime)
            {
                targetTime = targetTime.AddDays(1);
            }

            var delay = targetTime - now;
            _logger.LogInformation("Next alert scheduled at {TargetTime}", targetTime);

            await Task.Delay(delay, stoppingToken);

            if (!stoppingToken.IsCancellationRequested)
            {
                await SendDailyAlertAsync(stoppingToken);
            }
        }
    }

    private async Task SendDailyAlertAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var engineClient = scope.ServiceProvider.GetRequiredService<ISqueezeEngineClient>();
        var lineNotify = scope.ServiceProvider.GetRequiredService<ILineNotifyService>();

        try
        {
            var request = new TopCandidatesRequest
            {
                Limit = 10,
                MinScore = 60,
                Date = DateTime.Today.ToString("yyyy-MM-dd")
            };

            var response = await engineClient.GetTopCandidatesAsync(request, ct);

            var candidates = response.Candidates.Select(c => new SqueezeSignalDto
            {
                Ticker = c.Ticker,
                Score = c.Score,
                Trend = c.Trend,
                Comment = c.Comment
            });

            await lineNotify.SendSqueezeAlertAsync(candidates, ct);
            _logger.LogInformation("Daily alert sent with {Count} candidates", response.Candidates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send daily alert");
        }
    }
}
```

### Task 3.8: 配置 DI 與中介軟體

```csharp
// src/AlphaSqueeze.Api/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
    new SqlConnectionFactory(builder.Configuration.GetConnectionString("DefaultConnection")!));

builder.Services.AddScoped<IDbConnection>(sp =>
    sp.GetRequiredService<IDbConnectionFactory>().CreateConnection());

// Repositories
builder.Services.AddScoped<IStockMetricsRepository, StockMetricsRepository>();
builder.Services.AddScoped<IWarrantRepository, WarrantRepository>();
builder.Services.AddScoped<ISqueezeSignalRepository, SqueezeSignalRepository>();

// gRPC Client
builder.Services.AddGrpcClient<SqueezeEngine.SqueezeEngineClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcSettings:EngineUrl"]!);
});
builder.Services.AddScoped<ISqueezeEngineClient, SqueezeEngineClient>();

// LINE Notify
builder.Services.AddHttpClient<ILineNotifyService, LineNotifyService>();

// Background Services
builder.Services.AddHostedService<DailyAlertService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Alpha Squeeze API", Version = "v1" });
});

builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
```

### Task 3.9: 建立 API 整合測試

```csharp
// tests/AlphaSqueeze.Tests/Controllers/SqueezeControllerTests.cs
namespace AlphaSqueeze.Tests.Controllers;

public class SqueezeControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SqueezeControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetTopCandidates_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/squeeze/top-candidates");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSqueezeSignal_InvalidTicker_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/squeeze/INVALID");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMetricsByDate_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/metrics");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

## 驗收標準

### 功能驗收
- [ ] API 可正常啟動並回應請求
- [ ] gRPC Client 可連接 Python Server
- [ ] 降級模式正常運作
- [ ] LINE Notify 推播成功
- [ ] Swagger 文件可存取

### 測試驗收
- [ ] 所有 Controller 測試通過
- [ ] 整合測試涵蓋所有 Endpoints
- [ ] 錯誤處理測試完整

### 品質檢查
- [ ] 無編譯警告
- [ ] API 回應時間 < 200ms
- [ ] 適當的 HTTP 狀態碼

## 執行測試
```bash
dotnet test tests/AlphaSqueeze.Tests --filter "Category=Api"
```

## 完成後輸出
1. 可運行的 Web API
2. Swagger 文件截圖
3. 通過的測試報告
4. LINE Notify 推播截圖
