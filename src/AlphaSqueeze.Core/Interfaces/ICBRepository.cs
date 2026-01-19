using AlphaSqueeze.Core.Entities;

namespace AlphaSqueeze.Core.Interfaces;

/// <summary>
/// CB 可轉債 Repository 介面
/// </summary>
public interface ICBRepository
{
    #region CBIssuance 相關

    /// <summary>
    /// 取得所有活躍 CB
    /// </summary>
    /// <returns>活躍 CB 清單</returns>
    Task<IEnumerable<CBIssuance>> GetActiveCBsAsync();

    /// <summary>
    /// 根據 CB 代號取得發行資訊
    /// </summary>
    /// <param name="cbTicker">CB 代號</param>
    /// <returns>CB 發行資訊，若不存在則返回 null</returns>
    Task<CBIssuance?> GetCBByTickerAsync(string cbTicker);

    /// <summary>
    /// 根據標的股票代號取得相關 CB
    /// </summary>
    /// <param name="underlyingTicker">標的股票代號</param>
    /// <returns>相關 CB 清單</returns>
    Task<IEnumerable<CBIssuance>> GetCBsByUnderlyingAsync(string underlyingTicker);

    /// <summary>
    /// 新增或更新 CB 發行資訊
    /// </summary>
    /// <param name="cb">CB 發行資訊</param>
    /// <returns>影響的資料列數</returns>
    Task<int> UpsertCBIssuanceAsync(CBIssuance cb);

    #endregion

    #region CBDailyTracking 相關

    /// <summary>
    /// 取得指定日期的所有 CB 追蹤資料
    /// </summary>
    /// <param name="tradeDate">交易日期</param>
    /// <param name="minWarningLevel">最低預警等級篩選 (可選)</param>
    /// <returns>CB 追蹤資料清單</returns>
    Task<IEnumerable<CBDailyTracking>> GetDailyTrackingAsync(DateTime tradeDate, string? minWarningLevel = null);

    /// <summary>
    /// 取得單一 CB 指定日期的追蹤資料
    /// </summary>
    /// <param name="cbTicker">CB 代號</param>
    /// <param name="tradeDate">交易日期</param>
    /// <returns>CB 追蹤資料</returns>
    Task<CBDailyTracking?> GetDailyTrackingByTickerAsync(string cbTicker, DateTime tradeDate);

    /// <summary>
    /// 取得 CB 歷史追蹤資料
    /// </summary>
    /// <param name="cbTicker">CB 代號</param>
    /// <param name="startDate">開始日期</param>
    /// <param name="endDate">結束日期</param>
    /// <returns>歷史追蹤資料</returns>
    Task<IEnumerable<CBDailyTracking>> GetTrackingHistoryAsync(string cbTicker, DateTime startDate, DateTime endDate);

    /// <summary>
    /// 取得高風險 CB (高連續天數)
    /// </summary>
    /// <param name="tradeDate">交易日期</param>
    /// <param name="minDaysAbove">最低連續天數</param>
    /// <param name="limit">返回數量上限</param>
    /// <returns>高風險 CB 清單</returns>
    Task<IEnumerable<CBDailyTracking>> GetCriticalCBsAsync(DateTime tradeDate, int minDaysAbove, int limit);

    /// <summary>
    /// 新增或更新 CB 每日追蹤資料
    /// 使用存儲程序自動計算連續天數與預警等級
    /// </summary>
    /// <param name="cbTicker">CB 代號</param>
    /// <param name="tradeDate">交易日期</param>
    /// <param name="underlyingClosePrice">標的收盤價</param>
    /// <param name="conversionPrice">轉換價</param>
    /// <param name="outstandingBalance">剩餘餘額</param>
    /// <returns>更新結果 (連續天數, 預警等級)</returns>
    Task<(int ConsecutiveDays, string WarningLevel)> UpsertDailyTrackingAsync(
        string cbTicker,
        DateTime tradeDate,
        decimal underlyingClosePrice,
        decimal conversionPrice,
        decimal outstandingBalance);

    #endregion

    #region 聚合查詢

    /// <summary>
    /// 取得 CB 預警摘要統計
    /// </summary>
    /// <param name="tradeDate">交易日期</param>
    /// <returns>各等級數量統計</returns>
    Task<(int Total, int Critical, int Warning, int Caution, int Safe)> GetWarningSummaryAsync(DateTime tradeDate);

    /// <summary>
    /// 取得完整 CB 預警資料 (Join CBIssuance 與 CBDailyTracking)
    /// </summary>
    /// <param name="tradeDate">交易日期</param>
    /// <param name="minWarningLevel">最低預警等級篩選</param>
    /// <returns>完整預警資料</returns>
    Task<IEnumerable<CBWarningData>> GetCBWarningsWithDetailsAsync(DateTime tradeDate, string minWarningLevel = "SAFE");

    #endregion
}

/// <summary>
/// CB 預警完整資料 (聚合查詢用)
/// </summary>
public class CBWarningData
{
    // CBIssuance 欄位
    public string CBTicker { get; set; } = string.Empty;
    public string UnderlyingTicker { get; set; } = string.Empty;
    public string? CBName { get; set; }
    public decimal? TotalIssueAmount { get; set; }
    public DateTime MaturityDate { get; set; }
    public decimal RedemptionTriggerPct { get; set; }
    public int RedemptionTriggerDays { get; set; }

    // CBDailyTracking 欄位
    public DateTime TradeDate { get; set; }
    public decimal? UnderlyingClosePrice { get; set; }
    public decimal? ConversionPrice { get; set; }
    public decimal? PriceToConversionRatio { get; set; }
    public bool? IsAboveTrigger { get; set; }
    public int ConsecutiveDaysAbove { get; set; }
    public decimal? OutstandingBalance { get; set; }
    public decimal? BalanceChangePercent { get; set; }
    public string? WarningLevel { get; set; }
}
