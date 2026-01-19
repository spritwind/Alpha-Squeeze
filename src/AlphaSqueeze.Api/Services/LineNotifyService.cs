using System.Net.Http.Headers;
using System.Text;
using AlphaSqueeze.Api.Models;

namespace AlphaSqueeze.Api.Services;

/// <summary>
/// LINE Notify 推播服務實作
///
/// 功能：
/// - 發送軋空訊號推播
/// - 格式化推播訊息
/// - 錯誤處理與重試
/// </summary>
public class LineNotifyService : ILineNotifyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LineNotifyService> _logger;
    private readonly string? _accessToken;
    private readonly bool _isEnabled;

    private const string LINE_NOTIFY_URL = "https://notify-api.line.me/api/notify";

    public LineNotifyService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<LineNotifyService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accessToken = configuration["LineNotify:AccessToken"];
        _isEnabled = !string.IsNullOrEmpty(_accessToken);

        if (!_isEnabled)
        {
            _logger.LogWarning("LINE Notify is disabled - AccessToken not configured");
        }
    }

    /// <summary>
    /// 發送軋空訊號通知
    /// </summary>
    public async Task SendSqueezeAlertAsync(
        IEnumerable<SqueezeSignalDto> candidates,
        CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            _logger.LogWarning("LINE Notify is disabled, skipping alert");
            return;
        }

        var candidateList = candidates.ToList();
        if (candidateList.Count == 0)
        {
            _logger.LogInformation("No candidates to send alert for");
            return;
        }

        var message = FormatAlertMessage(candidateList);
        await SendMessageAsync(message, ct);
    }

    /// <summary>
    /// 發送自訂訊息
    /// </summary>
    public async Task SendMessageAsync(string message, CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            _logger.LogWarning("LINE Notify is disabled, skipping message");
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be empty", nameof(message));
        }

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("message", message)
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, LINE_NOTIFY_URL);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Content = content;

        try
        {
            _logger.LogDebug("Sending LINE Notify message...");

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("LINE Notify sent successfully");
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "LINE Notify failed: {StatusCode} - {Response}",
                    response.StatusCode, responseBody);
                throw new HttpRequestException(
                    $"LINE Notify failed with status {response.StatusCode}: {responseBody}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to send LINE Notify");
            throw;
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == ct)
        {
            _logger.LogWarning("LINE Notify request was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while sending LINE Notify");
            throw;
        }
    }

    /// <summary>
    /// 格式化推播訊息
    /// </summary>
    private static string FormatAlertMessage(IReadOnlyList<SqueezeSignalDto> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("🚀 Alpha Squeeze 軋空訊號");
        sb.AppendLine("═══════════════════");

        int rank = 1;
        foreach (var c in candidates.Take(5))
        {
            var trendEmoji = GetTrendEmoji(c.Trend);
            var scoreBar = GetScoreBar(c.Score);

            sb.AppendLine();
            sb.AppendLine($"{rank}. {c.Ticker} {trendEmoji}");
            sb.AppendLine($"   分數: {c.Score} {scoreBar}");

            if (!string.IsNullOrEmpty(c.Comment))
            {
                // 限制評論長度
                var comment = c.Comment.Length > 50
                    ? c.Comment[..47] + "..."
                    : c.Comment;
                sb.AppendLine($"   {comment}");
            }

            if (c.Factors != null)
            {
                sb.AppendLine($"   📊 借券:{c.Factors.BorrowScore:F0} " +
                              $"Gamma:{c.Factors.GammaScore:F0} " +
                              $"散戶:{c.Factors.MarginScore:F0} " +
                              $"動能:{c.Factors.MomentumScore:F0}");
            }

            rank++;
        }

        if (candidates.Count > 5)
        {
            sb.AppendLine();
            sb.AppendLine($"...還有 {candidates.Count - 5} 檔標的");
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════");
        sb.AppendLine($"分析時間: {DateTime.Now:MM/dd HH:mm}");

        return sb.ToString();
    }

    /// <summary>
    /// 取得趨勢表情符號
    /// </summary>
    private static string GetTrendEmoji(string trend) => trend switch
    {
        "BULLISH" => "🔴",  // 看多
        "BEARISH" => "🟢",  // 看空
        "NEUTRAL" => "⚪",  // 中性
        "DEGRADED" => "⚠️", // 降級模式
        _ => "⚪"
    };

    /// <summary>
    /// 取得分數條
    /// </summary>
    private static string GetScoreBar(int score)
    {
        var filled = score / 10;
        var empty = 10 - filled;
        return $"[{'█'.ToString().PadRight(filled, '█')}{'░'.ToString().PadRight(empty, '░')}]";
    }
}
