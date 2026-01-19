using AlphaSqueeze.Core.Entities;

namespace AlphaSqueeze.Core.Interfaces;

/// <summary>
/// 股票日指標 Repository 介面
/// </summary>
public interface IStockMetricsRepository
{
    /// <summary>
    /// 根據股票代號和日期取得指標
    /// </summary>
    /// <param name="ticker">股票代號</param>
    /// <param name="date">交易日期</param>
    /// <returns>股票日指標，若不存在則返回 null</returns>
    Task<DailyStockMetric?> GetByTickerAndDateAsync(string ticker, DateTime date);

    /// <summary>
    /// 根據日期取得所有股票指標
    /// </summary>
    /// <param name="date">交易日期</param>
    /// <returns>該日所有股票指標</returns>
    Task<IEnumerable<DailyStockMetric>> GetByDateAsync(DateTime date);

    /// <summary>
    /// 取得指定股票的歷史資料
    /// </summary>
    /// <param name="ticker">股票代號</param>
    /// <param name="startDate">開始日期</param>
    /// <param name="endDate">結束日期</param>
    /// <returns>歷史指標資料</returns>
    Task<IEnumerable<DailyStockMetric>> GetHistoryAsync(string ticker, DateTime startDate, DateTime endDate);

    /// <summary>
    /// 取得指定日期券資比最高的標的
    /// </summary>
    /// <param name="date">交易日期</param>
    /// <param name="minMarginRatio">最低券資比門檻</param>
    /// <param name="limit">返回數量</param>
    /// <returns>高券資比標的</returns>
    Task<IEnumerable<DailyStockMetric>> GetTopByMarginRatioAsync(DateTime date, decimal minMarginRatio, int limit);

    /// <summary>
    /// 新增或更新單一指標
    /// </summary>
    /// <param name="metric">股票日指標</param>
    /// <returns>影響的資料列數</returns>
    Task<int> UpsertAsync(DailyStockMetric metric);

    /// <summary>
    /// 批量新增或更新指標
    /// </summary>
    /// <param name="metrics">股票日指標集合</param>
    /// <returns>影響的資料列數</returns>
    Task<int> BulkUpsertAsync(IEnumerable<DailyStockMetric> metrics);
}
