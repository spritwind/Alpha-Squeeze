using AlphaSqueeze.Shared.Grpc;

namespace AlphaSqueeze.Shared.Services;

/// <summary>
/// gRPC 客戶端介面 - 與 Python Squeeze Engine 通訊
/// </summary>
public interface ISqueezeEngineClient
{
    /// <summary>
    /// 取得單一標的軋空訊號
    /// </summary>
    Task<SqueezeResponse> GetSqueezeSignalAsync(SqueezeRequest request, CancellationToken ct = default);

    /// <summary>
    /// 批量取得多個標的軋空訊號
    /// </summary>
    Task<BatchSqueezeResponse> GetBatchSignalsAsync(BatchSqueezeRequest request, CancellationToken ct = default);

    /// <summary>
    /// 取得當日熱門軋空候選標的
    /// </summary>
    Task<TopCandidatesResponse> GetTopCandidatesAsync(TopCandidatesRequest request, CancellationToken ct = default);

    /// <summary>
    /// gRPC 伺服器是否可用
    /// </summary>
    bool IsAvailable { get; }
}
