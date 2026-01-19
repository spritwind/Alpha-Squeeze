using System.Data;
using Dapper;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;

namespace AlphaSqueeze.Data.Repositories;

/// <summary>
/// 系統配置 Repository 實作
/// 使用 Dapper 進行資料存取
/// </summary>
public class SystemConfigRepository : ISystemConfigRepository
{
    private readonly IDbConnection _connection;

    public SystemConfigRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SystemConfig>> GetAllAsync()
    {
        const string sql = @"
            SELECT ConfigKey, ConfigValue, ValueType, Category,
                   Description, MinValue, MaxValue, IsReadOnly,
                   CreatedAt, UpdatedAt, UpdatedBy
            FROM SystemConfig
            ORDER BY Category, ConfigKey";

        return await _connection.QueryAsync<SystemConfig>(sql);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SystemConfig>> GetByCategoryAsync(string category)
    {
        const string sql = @"
            SELECT ConfigKey, ConfigValue, ValueType, Category,
                   Description, MinValue, MaxValue, IsReadOnly,
                   CreatedAt, UpdatedAt, UpdatedBy
            FROM SystemConfig
            WHERE Category = @Category
            ORDER BY ConfigKey";

        return await _connection.QueryAsync<SystemConfig>(sql, new { Category = category });
    }

    /// <inheritdoc />
    public async Task<SystemConfig?> GetByKeyAsync(string key)
    {
        const string sql = @"
            SELECT ConfigKey, ConfigValue, ValueType, Category,
                   Description, MinValue, MaxValue, IsReadOnly,
                   CreatedAt, UpdatedAt, UpdatedBy
            FROM SystemConfig
            WHERE ConfigKey = @Key";

        return await _connection.QuerySingleOrDefaultAsync<SystemConfig>(sql, new { Key = key });
    }

    /// <inheritdoc />
    public async Task<string?> GetValueAsync(string key)
    {
        const string sql = "SELECT ConfigValue FROM SystemConfig WHERE ConfigKey = @Key";
        return await _connection.QuerySingleOrDefaultAsync<string>(sql, new { Key = key });
    }

    /// <inheritdoc />
    public async Task<bool> UpdateValueAsync(string key, string value, string? updatedBy = null)
    {
        const string sql = @"
            UPDATE SystemConfig
            SET ConfigValue = @Value,
                UpdatedAt = GETDATE(),
                UpdatedBy = @UpdatedBy
            WHERE ConfigKey = @Key AND IsReadOnly = 0";

        var rowsAffected = await _connection.ExecuteAsync(
            sql, new { Key = key, Value = value, UpdatedBy = updatedBy });

        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<int> UpdateManyAsync(IEnumerable<(string Key, string Value)> updates, string? updatedBy = null)
    {
        const string sql = @"
            UPDATE SystemConfig
            SET ConfigValue = @Value,
                UpdatedAt = GETDATE(),
                UpdatedBy = @UpdatedBy
            WHERE ConfigKey = @Key AND IsReadOnly = 0";

        var totalAffected = 0;
        foreach (var (key, value) in updates)
        {
            var affected = await _connection.ExecuteAsync(
                sql, new { Key = key, Value = value, UpdatedBy = updatedBy });
            totalAffected += affected;
        }

        return totalAffected;
    }

    /// <inheritdoc />
    public async Task<SqueezeAlgorithmConfig> GetSqueezeConfigAsync()
    {
        var weights = await GetByCategoryAsync("SQUEEZE_WEIGHT");
        var thresholds = await GetByCategoryAsync("SQUEEZE_THRESHOLD");

        var weightDict = weights.ToDictionary(w => w.ConfigKey, w => w.GetDoubleValue());
        var thresholdDict = thresholds.ToDictionary(t => t.ConfigKey, t => t.GetIntValue());

        return new SqueezeAlgorithmConfig
        {
            WeightBorrow = weightDict.GetValueOrDefault("SQUEEZE_WEIGHT_BORROW", 0.35),
            WeightGamma = weightDict.GetValueOrDefault("SQUEEZE_WEIGHT_GAMMA", 0.25),
            WeightMargin = weightDict.GetValueOrDefault("SQUEEZE_WEIGHT_MARGIN", 0.20),
            WeightMomentum = weightDict.GetValueOrDefault("SQUEEZE_WEIGHT_MOMENTUM", 0.20),
            BullishThreshold = thresholdDict.GetValueOrDefault("SQUEEZE_THRESHOLD_BULLISH", 70),
            BearishThreshold = thresholdDict.GetValueOrDefault("SQUEEZE_THRESHOLD_BEARISH", 40)
        };
    }

    /// <inheritdoc />
    public async Task<bool> UpdateSqueezeConfigAsync(SqueezeAlgorithmConfig config, string? updatedBy = null)
    {
        // 驗證權重總和
        if (!config.ValidateWeights())
        {
            throw new ArgumentException("Weight sum must equal 1.0");
        }

        var updates = new List<(string Key, string Value)>
        {
            ("SQUEEZE_WEIGHT_BORROW", config.WeightBorrow.ToString("F2")),
            ("SQUEEZE_WEIGHT_GAMMA", config.WeightGamma.ToString("F2")),
            ("SQUEEZE_WEIGHT_MARGIN", config.WeightMargin.ToString("F2")),
            ("SQUEEZE_WEIGHT_MOMENTUM", config.WeightMomentum.ToString("F2")),
            ("SQUEEZE_THRESHOLD_BULLISH", config.BullishThreshold.ToString()),
            ("SQUEEZE_THRESHOLD_BEARISH", config.BearishThreshold.ToString())
        };

        var affected = await UpdateManyAsync(updates, updatedBy);
        return affected > 0;
    }
}
