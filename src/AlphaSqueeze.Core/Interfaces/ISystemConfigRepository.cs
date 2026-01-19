using AlphaSqueeze.Core.Entities;

namespace AlphaSqueeze.Core.Interfaces;

/// <summary>
/// 系統配置倉儲介面
/// </summary>
public interface ISystemConfigRepository
{
    /// <summary>
    /// 取得所有配置
    /// </summary>
    Task<IEnumerable<SystemConfig>> GetAllAsync();

    /// <summary>
    /// 依分類取得配置
    /// </summary>
    Task<IEnumerable<SystemConfig>> GetByCategoryAsync(string category);

    /// <summary>
    /// 取得單一配置值
    /// </summary>
    Task<SystemConfig?> GetByKeyAsync(string key);

    /// <summary>
    /// 取得配置值 (字串)
    /// </summary>
    Task<string?> GetValueAsync(string key);

    /// <summary>
    /// 更新配置值
    /// </summary>
    Task<bool> UpdateValueAsync(string key, string value, string? updatedBy = null);

    /// <summary>
    /// 批量更新配置
    /// </summary>
    Task<int> UpdateManyAsync(IEnumerable<(string Key, string Value)> updates, string? updatedBy = null);

    /// <summary>
    /// 取得軋空演算法配置
    /// </summary>
    Task<SqueezeAlgorithmConfig> GetSqueezeConfigAsync();

    /// <summary>
    /// 更新軋空演算法配置
    /// </summary>
    Task<bool> UpdateSqueezeConfigAsync(SqueezeAlgorithmConfig config, string? updatedBy = null);
}
