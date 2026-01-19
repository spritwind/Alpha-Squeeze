namespace AlphaSqueeze.Core.Entities;

/// <summary>
/// 系統日誌實體
/// </summary>
public class SystemLog
{
    /// <summary>
    /// 主鍵 ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 日誌等級 (INFO/WARNING/ERROR)
    /// </summary>
    public string LogLevel { get; set; } = string.Empty;

    /// <summary>
    /// 來源模組
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 日誌訊息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 例外詳情
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// 相關股票代號
    /// </summary>
    public string? Ticker { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 日誌等級列舉
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// 資訊
    /// </summary>
    Info,

    /// <summary>
    /// 警告
    /// </summary>
    Warning,

    /// <summary>
    /// 錯誤
    /// </summary>
    Error
}
