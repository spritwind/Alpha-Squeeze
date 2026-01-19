using AlphaSqueeze.Api.Models;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AlphaSqueeze.Api.Controllers;

/// <summary>
/// 系統配置 API
///
/// 提供系統參數的查詢與修改功能。
/// 包括軋空演算法的權重、門檻等設定。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ConfigController : ControllerBase
{
    private readonly ISystemConfigRepository _configRepo;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(
        ISystemConfigRepository configRepo,
        ILogger<ConfigController> logger)
    {
        _configRepo = configRepo;
        _logger = logger;
    }

    /// <summary>
    /// 取得所有系統配置
    /// </summary>
    /// <returns>配置分類列表</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ConfigCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllConfig()
    {
        _logger.LogInformation("Requesting all system configurations");

        var configs = await _configRepo.GetAllAsync();

        // 依分類分組
        var categories = configs
            .GroupBy(c => c.Category)
            .Select(g => new ConfigCategoryDto
            {
                Category = g.Key,
                Description = GetCategoryDescription(g.Key),
                Items = g.Select(MapToDto).ToList()
            })
            .OrderBy(c => GetCategoryOrder(c.Category))
            .ToList();

        return Ok(categories);
    }

    /// <summary>
    /// 取得軋空演算法配置
    /// </summary>
    /// <returns>軋空配置</returns>
    [HttpGet("squeeze")]
    [ProducesResponseType(typeof(SqueezeConfigDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSqueezeConfig()
    {
        _logger.LogInformation("Requesting squeeze algorithm configuration");

        var config = await _configRepo.GetSqueezeConfigAsync();

        var dto = new SqueezeConfigDto
        {
            Weights = new WeightsDto
            {
                Borrow = config.WeightBorrow,
                Gamma = config.WeightGamma,
                Margin = config.WeightMargin,
                Momentum = config.WeightMomentum
            },
            Thresholds = new ThresholdsDto
            {
                Bullish = config.BullishThreshold,
                Bearish = config.BearishThreshold
            }
        };

        return Ok(dto);
    }

    /// <summary>
    /// 更新軋空演算法配置
    /// </summary>
    /// <param name="request">更新請求</param>
    /// <returns>更新後的配置</returns>
    [HttpPut("squeeze")]
    [ProducesResponseType(typeof(SqueezeConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSqueezeConfig([FromBody] UpdateSqueezeConfigRequest request)
    {
        _logger.LogInformation("Updating squeeze algorithm configuration");

        // 驗證權重總和
        if (!request.Weights.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = $"權重總和必須為 1.0，目前為 {request.Weights.Total:F2}",
                ErrorCode = "INVALID_WEIGHTS"
            });
        }

        // 驗證門檻邏輯
        if (request.Thresholds.Bearish >= request.Thresholds.Bullish)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "看空門檻必須小於看多門檻",
                ErrorCode = "INVALID_THRESHOLDS"
            });
        }

        var config = new SqueezeAlgorithmConfig
        {
            WeightBorrow = request.Weights.Borrow,
            WeightGamma = request.Weights.Gamma,
            WeightMargin = request.Weights.Margin,
            WeightMomentum = request.Weights.Momentum,
            BullishThreshold = request.Thresholds.Bullish,
            BearishThreshold = request.Thresholds.Bearish
        };

        var success = await _configRepo.UpdateSqueezeConfigAsync(config, "API");

        if (!success)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "更新配置失敗，部分配置可能為唯讀",
                ErrorCode = "UPDATE_FAILED"
            });
        }

        _logger.LogInformation(
            "Squeeze config updated: Weights=[{Borrow}, {Gamma}, {Margin}, {Momentum}], Thresholds=[{Bullish}, {Bearish}]",
            config.WeightBorrow, config.WeightGamma, config.WeightMargin, config.WeightMomentum,
            config.BullishThreshold, config.BearishThreshold);

        return Ok(new SqueezeConfigDto
        {
            Weights = request.Weights,
            Thresholds = request.Thresholds
        });
    }

    /// <summary>
    /// 依分類取得配置
    /// </summary>
    /// <param name="category">分類名稱</param>
    /// <returns>配置項目列表</returns>
    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(ConfigCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCategory(string category)
    {
        _logger.LogInformation("Requesting configuration for category: {Category}", category);

        var configs = await _configRepo.GetByCategoryAsync(category);
        var configList = configs.ToList();

        if (configList.Count == 0)
        {
            return NotFound(new ErrorResponse
            {
                Message = $"找不到分類 '{category}' 的配置",
                ErrorCode = "CATEGORY_NOT_FOUND"
            });
        }

        var dto = new ConfigCategoryDto
        {
            Category = category,
            Description = GetCategoryDescription(category),
            Items = configList.Select(MapToDto).ToList()
        };

        return Ok(dto);
    }

    /// <summary>
    /// 更新單一配置值
    /// </summary>
    /// <param name="request">更新請求</param>
    /// <returns>更新結果</returns>
    [HttpPut]
    [ProducesResponseType(typeof(ConfigItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConfig([FromBody] UpdateConfigRequest request)
    {
        _logger.LogInformation("Updating config: {Key} = {Value}", request.Key, request.Value);

        // 檢查配置是否存在
        var existing = await _configRepo.GetByKeyAsync(request.Key);
        if (existing == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = $"找不到配置項目: {request.Key}",
                ErrorCode = "CONFIG_NOT_FOUND"
            });
        }

        if (existing.IsReadOnly)
        {
            return BadRequest(new ErrorResponse
            {
                Message = $"配置項目 '{request.Key}' 為唯讀，無法修改",
                ErrorCode = "CONFIG_READONLY"
            });
        }

        // 驗證數值範圍
        if (existing.ValueType is "INT" or "DECIMAL")
        {
            if (!decimal.TryParse(request.Value, out var numValue))
            {
                return BadRequest(new ErrorResponse
                {
                    Message = $"配置值必須為數值",
                    ErrorCode = "INVALID_VALUE_TYPE"
                });
            }

            if (existing.MinValue.HasValue && numValue < existing.MinValue)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = $"配置值不可小於 {existing.MinValue}",
                    ErrorCode = "VALUE_BELOW_MINIMUM"
                });
            }

            if (existing.MaxValue.HasValue && numValue > existing.MaxValue)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = $"配置值不可大於 {existing.MaxValue}",
                    ErrorCode = "VALUE_ABOVE_MAXIMUM"
                });
            }
        }

        var success = await _configRepo.UpdateValueAsync(request.Key, request.Value, "API");

        if (!success)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "更新配置失敗",
                ErrorCode = "UPDATE_FAILED"
            });
        }

        // 回傳更新後的配置
        var updated = await _configRepo.GetByKeyAsync(request.Key);
        return Ok(MapToDto(updated!));
    }

    #region 私有方法

    private static ConfigItemDto MapToDto(SystemConfig config)
    {
        return new ConfigItemDto
        {
            Key = config.ConfigKey,
            Value = config.ConfigValue,
            ValueType = config.ValueType,
            Description = config.Description,
            MinValue = config.MinValue,
            MaxValue = config.MaxValue,
            IsReadOnly = config.IsReadOnly,
            UpdatedAt = config.UpdatedAt
        };
    }

    private static string GetCategoryDescription(string category)
    {
        return category switch
        {
            "SQUEEZE_WEIGHT" => "軋空演算法權重設定",
            "SQUEEZE_THRESHOLD" => "軋空訊號門檻設定",
            "MARGIN_SCORING" => "券資比評分區間設定",
            "SYSTEM" => "系統設定",
            _ => category
        };
    }

    private static int GetCategoryOrder(string category)
    {
        return category switch
        {
            "SQUEEZE_WEIGHT" => 1,
            "SQUEEZE_THRESHOLD" => 2,
            "MARGIN_SCORING" => 3,
            "SYSTEM" => 4,
            _ => 99
        };
    }

    #endregion
}
