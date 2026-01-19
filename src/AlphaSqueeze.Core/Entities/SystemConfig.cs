namespace AlphaSqueeze.Core.Entities;

/// <summary>
/// 系統配置實體
/// 對應資料庫 SystemConfig 表
/// </summary>
public class SystemConfig
{
    /// <summary>配置鍵名</summary>
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>配置值</summary>
    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>值類型 (STRING/INT/DECIMAL/BOOL/JSON)</summary>
    public string ValueType { get; set; } = "STRING";

    /// <summary>配置分類</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>說明</summary>
    public string? Description { get; set; }

    /// <summary>最小值限制</summary>
    public decimal? MinValue { get; set; }

    /// <summary>最大值限制</summary>
    public decimal? MaxValue { get; set; }

    /// <summary>是否唯讀</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>建立時間</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>更新時間</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>更新者</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// 取得配置值的整數表示
    /// </summary>
    public int GetIntValue(int defaultValue = 0)
    {
        return int.TryParse(ConfigValue, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// 取得配置值的小數表示
    /// </summary>
    public decimal GetDecimalValue(decimal defaultValue = 0)
    {
        return decimal.TryParse(ConfigValue, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// 取得配置值的浮點數表示
    /// </summary>
    public double GetDoubleValue(double defaultValue = 0)
    {
        return double.TryParse(ConfigValue, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// 取得配置值的布林表示
    /// </summary>
    public bool GetBoolValue(bool defaultValue = false)
    {
        return bool.TryParse(ConfigValue, out var result) ? result : defaultValue;
    }
}

/// <summary>
/// 軋空演算法配置 DTO
/// </summary>
public class SqueezeAlgorithmConfig
{
    /// <summary>法人空頭權重</summary>
    public double WeightBorrow { get; set; } = 0.35;

    /// <summary>Gamma效應權重</summary>
    public double WeightGamma { get; set; } = 0.25;

    /// <summary>散戶燃料權重</summary>
    public double WeightMargin { get; set; } = 0.20;

    /// <summary>價量動能權重</summary>
    public double WeightMomentum { get; set; } = 0.20;

    /// <summary>看多門檻</summary>
    public int BullishThreshold { get; set; } = 70;

    /// <summary>看空門檻</summary>
    public int BearishThreshold { get; set; } = 40;

    /// <summary>
    /// 驗證權重總和是否為 1.0
    /// </summary>
    public bool ValidateWeights()
    {
        var total = WeightBorrow + WeightGamma + WeightMargin + WeightMomentum;
        return Math.Abs(total - 1.0) < 0.001;
    }
}
