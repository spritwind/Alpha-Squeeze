using AlphaSqueeze.Core.Entities;

namespace AlphaSqueeze.Core.Interfaces;

/// <summary>
/// 資料回補任務倉儲介面
/// </summary>
public interface IBackfillJobRepository
{
    /// <summary>
    /// 建立新的回補任務
    /// </summary>
    Task<int> CreateJobAsync(
        string jobType,
        DateTime startDate,
        DateTime endDate,
        int totalTickers,
        string? createdBy = null);

    /// <summary>
    /// 取得任務詳情
    /// </summary>
    Task<BackfillJob?> GetByIdAsync(int jobId);

    /// <summary>
    /// 取得最近的任務列表
    /// </summary>
    Task<IEnumerable<BackfillJob>> GetRecentJobsAsync(int limit = 10);

    /// <summary>
    /// 取得正在執行的任務
    /// </summary>
    Task<IEnumerable<BackfillJob>> GetRunningJobsAsync();

    /// <summary>
    /// 更新任務狀態為執行中
    /// </summary>
    Task StartJobAsync(int jobId);

    /// <summary>
    /// 更新任務進度
    /// </summary>
    Task UpdateProgressAsync(int jobId, int processedTickers, int failedTickers = 0);

    /// <summary>
    /// 完成任務
    /// </summary>
    Task CompleteJobAsync(int jobId, string? errorMessage = null);
}
