using AlphaSqueeze.Core.Entities;

namespace AlphaSqueeze.Core.Interfaces;

/// <summary>
/// 權證資料 Repository 介面
/// </summary>
public interface IWarrantRepository
{
    /// <summary>
    /// 根據權證代號和日期取得資料
    /// </summary>
    /// <param name="warrantTicker">權證代號</param>
    /// <param name="date">交易日期</param>
    /// <returns>權證資料，若不存在則返回 null</returns>
    Task<WarrantMarketData?> GetByTickerAndDateAsync(string warrantTicker, DateTime date);

    /// <summary>
    /// 根據標的股票代號和日期取得所有相關權證
    /// </summary>
    /// <param name="underlyingTicker">標的股票代號</param>
    /// <param name="date">交易日期</param>
    /// <returns>該標的所有權證資料</returns>
    Task<IEnumerable<WarrantMarketData>> GetByUnderlyingAsync(string underlyingTicker, DateTime date);

    /// <summary>
    /// 取得指定標的的平均 IV
    /// </summary>
    /// <param name="underlyingTicker">標的股票代號</param>
    /// <param name="date">交易日期</param>
    /// <param name="warrantType">權證類型 (Call/Put)，null 表示全部</param>
    /// <returns>平均隱含波動率</returns>
    Task<decimal?> GetAverageIVAsync(string underlyingTicker, DateTime date, string? warrantType = null);

    /// <summary>
    /// 根據日期取得所有權證資料
    /// </summary>
    /// <param name="date">交易日期</param>
    /// <returns>該日所有權證資料</returns>
    Task<IEnumerable<WarrantMarketData>> GetByDateAsync(DateTime date);

    /// <summary>
    /// 新增或更新單一權證資料
    /// </summary>
    /// <param name="warrant">權證資料</param>
    /// <returns>影響的資料列數</returns>
    Task<int> UpsertAsync(WarrantMarketData warrant);

    /// <summary>
    /// 批量新增或更新權證資料
    /// </summary>
    /// <param name="warrants">權證資料集合</param>
    /// <returns>影響的資料列數</returns>
    Task<int> BulkUpsertAsync(IEnumerable<WarrantMarketData> warrants);
}
