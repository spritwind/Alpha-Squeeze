using AlphaSqueeze.Core.Entities;

namespace AlphaSqueeze.Core.Interfaces;

/// <summary>
/// 用戶追蹤清單資料存取介面
/// </summary>
public interface IUserWatchListRepository
{
    /// <summary>取得所有追蹤項目</summary>
    Task<IEnumerable<UserWatchList>> GetAllAsync();

    /// <summary>取得啟用中的追蹤項目</summary>
    Task<IEnumerable<UserWatchList>> GetActiveAsync();

    /// <summary>取得啟用中的股票代號列表</summary>
    Task<IEnumerable<string>> GetActiveTickersAsync();

    /// <summary>依股票代號取得</summary>
    Task<UserWatchList?> GetByTickerAsync(string ticker);

    /// <summary>新增追蹤項目</summary>
    Task<bool> AddAsync(UserWatchList item);

    /// <summary>批量新增追蹤項目</summary>
    Task<int> BulkAddAsync(IEnumerable<string> tickers, string addedBy = "WebUI");

    /// <summary>更新追蹤項目</summary>
    Task<bool> UpdateAsync(UserWatchList item);

    /// <summary>設定啟用狀態</summary>
    Task<bool> SetActiveAsync(string ticker, bool isActive);

    /// <summary>移除追蹤項目</summary>
    Task<bool> RemoveAsync(string ticker);

    /// <summary>更新最後爬蟲時間</summary>
    Task<bool> UpdateScrapedTimeAsync(string ticker, DateTime scrapedTime, int? squeezeScore = null);
}
