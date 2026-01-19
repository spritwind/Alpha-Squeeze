using System.Data;
using Dapper;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;

namespace AlphaSqueeze.Data.Repositories;

/// <summary>
/// 資料回補任務 Repository 實作
/// 使用 Dapper 進行資料存取
/// </summary>
public class BackfillJobRepository : IBackfillJobRepository
{
    private readonly IDbConnection _connection;

    public BackfillJobRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public async Task<int> CreateJobAsync(
        string jobType,
        DateTime startDate,
        DateTime endDate,
        int totalTickers,
        string? createdBy = null)
    {
        const string sql = @"
            INSERT INTO BackfillJobs (JobType, StartDate, EndDate, Status, TotalTickers, CreatedBy)
            OUTPUT INSERTED.ID
            VALUES (@JobType, @StartDate, @EndDate, 'PENDING', @TotalTickers, @CreatedBy)";

        return await _connection.QuerySingleAsync<int>(sql, new
        {
            JobType = jobType,
            StartDate = startDate,
            EndDate = endDate,
            TotalTickers = totalTickers,
            CreatedBy = createdBy
        });
    }

    /// <inheritdoc />
    public async Task<BackfillJob?> GetByIdAsync(int jobId)
    {
        const string sql = @"
            SELECT ID AS Id, JobType, StartDate, EndDate, Status,
                   TotalTickers, ProcessedTickers, FailedTickers,
                   ErrorMessage, StartedAt, CompletedAt, CreatedAt, CreatedBy
            FROM BackfillJobs
            WHERE ID = @JobId";

        return await _connection.QuerySingleOrDefaultAsync<BackfillJob>(sql, new { JobId = jobId });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BackfillJob>> GetRecentJobsAsync(int limit = 10)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                   ID AS Id, JobType, StartDate, EndDate, Status,
                   TotalTickers, ProcessedTickers, FailedTickers,
                   ErrorMessage, StartedAt, CompletedAt, CreatedAt, CreatedBy
            FROM BackfillJobs
            ORDER BY CreatedAt DESC";

        return await _connection.QueryAsync<BackfillJob>(sql, new { Limit = limit });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BackfillJob>> GetRunningJobsAsync()
    {
        const string sql = @"
            SELECT ID AS Id, JobType, StartDate, EndDate, Status,
                   TotalTickers, ProcessedTickers, FailedTickers,
                   ErrorMessage, StartedAt, CompletedAt, CreatedAt, CreatedBy
            FROM BackfillJobs
            WHERE Status = 'RUNNING'
            ORDER BY StartedAt";

        return await _connection.QueryAsync<BackfillJob>(sql);
    }

    /// <inheritdoc />
    public async Task StartJobAsync(int jobId)
    {
        const string sql = @"
            UPDATE BackfillJobs
            SET Status = 'RUNNING', StartedAt = GETDATE()
            WHERE ID = @JobId AND Status = 'PENDING'";

        await _connection.ExecuteAsync(sql, new { JobId = jobId });
    }

    /// <inheritdoc />
    public async Task UpdateProgressAsync(int jobId, int processedTickers, int failedTickers = 0)
    {
        const string sql = @"
            UPDATE BackfillJobs
            SET ProcessedTickers = @ProcessedTickers,
                FailedTickers = @FailedTickers
            WHERE ID = @JobId";

        await _connection.ExecuteAsync(sql, new
        {
            JobId = jobId,
            ProcessedTickers = processedTickers,
            FailedTickers = failedTickers
        });
    }

    /// <inheritdoc />
    public async Task CompleteJobAsync(int jobId, string? errorMessage = null)
    {
        var status = string.IsNullOrEmpty(errorMessage) ? "COMPLETED" : "FAILED";

        const string sql = @"
            UPDATE BackfillJobs
            SET Status = @Status,
                CompletedAt = GETDATE(),
                ErrorMessage = @ErrorMessage
            WHERE ID = @JobId";

        await _connection.ExecuteAsync(sql, new
        {
            JobId = jobId,
            Status = status,
            ErrorMessage = errorMessage
        });
    }
}
