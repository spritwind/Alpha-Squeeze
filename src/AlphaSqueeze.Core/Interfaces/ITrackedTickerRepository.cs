using AlphaSqueeze.Core.Entities;

namespace AlphaSqueeze.Core.Interfaces;

/// <summary>
/// 追蹤股票倉儲介面
/// </summary>
public interface ITrackedTickerRepository
{
    /// <summary>
    /// 取得所有追蹤股票
    /// </summary>
    Task<IEnumerable<TrackedTicker>> GetAllAsync();

    /// <summary>
    /// 取得啟用中的股票代號列表
    /// </summary>
    Task<IEnumerable<string>> GetActiveTickersAsync();

    /// <summary>
    /// 依類股取得股票
    /// </summary>
    Task<IEnumerable<TrackedTicker>> GetByCategoryAsync(string category);

    /// <summary>
    /// 取得單一股票資訊
    /// </summary>
    Task<TrackedTicker?> GetByTickerAsync(string ticker);

    /// <summary>
    /// 新增追蹤股票
    /// </summary>
    Task<bool> AddAsync(TrackedTicker ticker);

    /// <summary>
    /// 更新追蹤股票
    /// </summary>
    Task<bool> UpdateAsync(TrackedTicker ticker);

    /// <summary>
    /// 啟用/停用追蹤
    /// </summary>
    Task<bool> SetActiveAsync(string ticker, bool isActive);

    /// <summary>
    /// 移除追蹤股票
    /// </summary>
    Task<bool> RemoveAsync(string ticker);
}
