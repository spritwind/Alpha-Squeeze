using AlphaSqueeze.Api.Models;

namespace AlphaSqueeze.Api.Services;

/// <summary>
/// LINE Notify 推播服務介面
/// </summary>
public interface ILineNotifyService
{
    /// <summary>
    /// 發送軋空訊號通知
    /// </summary>
    /// <param name="candidates">候選標的清單</param>
    /// <param name="ct">取消令牌</param>
    Task SendSqueezeAlertAsync(IEnumerable<SqueezeSignalDto> candidates, CancellationToken ct = default);

    /// <summary>
    /// 發送自訂訊息
    /// </summary>
    /// <param name="message">訊息內容</param>
    /// <param name="ct">取消令牌</param>
    Task SendMessageAsync(string message, CancellationToken ct = default);
}
