using AlphaSqueeze.Core.Entities;

namespace AlphaSqueeze.Core.Interfaces;

/// <summary>
/// Discovery Pool 資料存取介面
/// </summary>
public interface IDiscoveryRepository
{
    /// <summary>取得指定日期的掃描結果</summary>
    Task<IEnumerable<DiscoveryPool>> GetByDateAsync(DateTime scanDate);

    /// <summary>取得最新掃描結果</summary>
    Task<IEnumerable<DiscoveryPool>> GetLatestAsync(int limit = 100);

    /// <summary>依條件篩選掃描結果</summary>
    Task<IEnumerable<DiscoveryPool>> FilterAsync(
        DateTime? scanDate = null,
        decimal? minShortRatio = null,
        decimal? minVolMultiplier = null,
        decimal? minPrice = null,
        long? minVolume = null,
        bool? hasCB = null,
        int? minScore = null,
        int limit = 100);

    /// <summary>新增掃描結果</summary>
    Task<int> AddAsync(DiscoveryPool item);

    /// <summary>批量新增掃描結果</summary>
    Task<int> BulkAddAsync(IEnumerable<DiscoveryPool> items);

    /// <summary>清除指定日期的舊資料</summary>
    Task<int> ClearByDateAsync(DateTime scanDate);

    /// <summary>取得掃描參數配置</summary>
    Task<Dictionary<string, string>> GetConfigAsync();

    /// <summary>更新掃描參數配置</summary>
    Task UpdateConfigAsync(string key, string value);
}
