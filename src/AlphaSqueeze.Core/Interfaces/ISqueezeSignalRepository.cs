using AlphaSqueeze.Core.Entities;

namespace AlphaSqueeze.Core.Interfaces;

/// <summary>
/// 軋空訊號 Repository 介面
/// </summary>
public interface ISqueezeSignalRepository
{
    /// <summary>
    /// 根據股票代號和日期取得訊號
    /// </summary>
    /// <param name="ticker">股票代號</param>
    /// <param name="date">訊號日期</param>
    /// <returns>軋空訊號，若不存在則返回 null</returns>
    Task<SqueezeSignal?> GetByTickerAndDateAsync(string ticker, DateTime date);

    /// <summary>
    /// 取得指定日期的頂尖軋空候選標的
    /// </summary>
    /// <param name="date">訊號日期</param>
    /// <param name="minScore">最低分數門檻</param>
    /// <param name="limit">返回數量上限</param>
    /// <returns>軋空候選標的（按分數降序）</returns>
    Task<IEnumerable<SqueezeSignal>> GetTopCandidatesAsync(DateTime date, int minScore = 60, int limit = 10);

    /// <summary>
    /// 取得指定日期所有軋空訊號
    /// </summary>
    /// <param name="date">訊號日期</param>
    /// <returns>該日所有軋空訊號</returns>
    Task<IEnumerable<SqueezeSignal>> GetByDateAsync(DateTime date);

    /// <summary>
    /// 取得指定標的的歷史訊號
    /// </summary>
    /// <param name="ticker">股票代號</param>
    /// <param name="startDate">開始日期</param>
    /// <param name="endDate">結束日期</param>
    /// <returns>歷史訊號資料</returns>
    Task<IEnumerable<SqueezeSignal>> GetHistoryAsync(string ticker, DateTime startDate, DateTime endDate);

    /// <summary>
    /// 取得尚未發送通知的訊號
    /// </summary>
    /// <param name="date">訊號日期</param>
    /// <param name="minScore">最低分數門檻</param>
    /// <returns>尚未發送通知的訊號</returns>
    Task<IEnumerable<SqueezeSignal>> GetPendingNotificationsAsync(DateTime date, int minScore = 60);

    /// <summary>
    /// 新增或更新單一訊號
    /// </summary>
    /// <param name="signal">軋空訊號</param>
    /// <returns>影響的資料列數</returns>
    Task<int> UpsertAsync(SqueezeSignal signal);

    /// <summary>
    /// 批量新增或更新訊號
    /// </summary>
    /// <param name="signals">軋空訊號集合</param>
    /// <returns>影響的資料列數</returns>
    Task<int> BulkUpsertAsync(IEnumerable<SqueezeSignal> signals);

    /// <summary>
    /// 標記訊號為已發送通知
    /// </summary>
    /// <param name="signalIds">訊號 ID 集合</param>
    /// <returns>影響的資料列數</returns>
    Task<int> MarkNotificationSentAsync(IEnumerable<int> signalIds);
}
