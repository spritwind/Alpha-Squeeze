using System.ComponentModel.DataAnnotations;

namespace AlphaSqueeze.Api.Models;

/// <summary>
/// 系統配置項目 DTO
/// </summary>
public class ConfigItemDto
{
    /// <summary>配置鍵名</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>配置值</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>值類型</summary>
    public string ValueType { get; set; } = "STRING";

    /// <summary>說明</summary>
    public string? Description { get; set; }

    /// <summary>最小值</summary>
    public decimal? MinValue { get; set; }

    /// <summary>最大值</summary>
    public decimal? MaxValue { get; set; }

    /// <summary>是否唯讀</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>更新時間</summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 配置分類 DTO
/// </summary>
public class ConfigCategoryDto
{
    /// <summary>分類名稱</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>分類說明</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>配置項目列表</summary>
    public List<ConfigItemDto> Items { get; set; } = new();
}

/// <summary>
/// 軋空演算法配置 DTO
/// </summary>
public class SqueezeConfigDto
{
    /// <summary>權重配置</summary>
    public WeightsDto Weights { get; set; } = new();

    /// <summary>門檻配置</summary>
    public ThresholdsDto Thresholds { get; set; } = new();
}

/// <summary>
/// 權重配置 DTO
/// </summary>
public class WeightsDto
{
    /// <summary>法人空頭權重 (0-1)</summary>
    [Range(0, 1, ErrorMessage = "權重必須在 0-1 之間")]
    public double Borrow { get; set; } = 0.35;

    /// <summary>Gamma效應權重 (0-1)</summary>
    [Range(0, 1, ErrorMessage = "權重必須在 0-1 之間")]
    public double Gamma { get; set; } = 0.25;

    /// <summary>散戶燃料權重 (0-1)</summary>
    [Range(0, 1, ErrorMessage = "權重必須在 0-1 之間")]
    public double Margin { get; set; } = 0.20;

    /// <summary>價量動能權重 (0-1)</summary>
    [Range(0, 1, ErrorMessage = "權重必須在 0-1 之間")]
    public double Momentum { get; set; } = 0.20;

    /// <summary>權重總和</summary>
    public double Total => Borrow + Gamma + Margin + Momentum;

    /// <summary>驗證權重是否有效</summary>
    public bool IsValid => Math.Abs(Total - 1.0) < 0.001;
}

/// <summary>
/// 門檻配置 DTO
/// </summary>
public class ThresholdsDto
{
    /// <summary>看多門檻 (0-100)</summary>
    [Range(0, 100, ErrorMessage = "門檻必須在 0-100 之間")]
    public int Bullish { get; set; } = 70;

    /// <summary>看空門檻 (0-100)</summary>
    [Range(0, 100, ErrorMessage = "門檻必須在 0-100 之間")]
    public int Bearish { get; set; } = 40;
}

/// <summary>
/// 更新配置請求 DTO
/// </summary>
public class UpdateConfigRequest
{
    /// <summary>配置鍵名</summary>
    [Required(ErrorMessage = "配置鍵名為必填")]
    public string Key { get; set; } = string.Empty;

    /// <summary>新的配置值</summary>
    [Required(ErrorMessage = "配置值為必填")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// 更新軋空配置請求 DTO
/// </summary>
public class UpdateSqueezeConfigRequest
{
    /// <summary>權重配置</summary>
    [Required]
    public WeightsDto Weights { get; set; } = new();

    /// <summary>門檻配置</summary>
    [Required]
    public ThresholdsDto Thresholds { get; set; } = new();
}
