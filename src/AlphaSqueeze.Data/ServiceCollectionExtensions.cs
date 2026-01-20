using System.Data;
using Microsoft.Extensions.DependencyInjection;
using AlphaSqueeze.Core.Interfaces;
using AlphaSqueeze.Data.Repositories;

namespace AlphaSqueeze.Data;

/// <summary>
/// 資料層服務註冊擴充方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Alpha Squeeze 資料層服務
    /// </summary>
    /// <param name="services">服務集合</param>
    /// <param name="connectionString">資料庫連線字串</param>
    /// <returns>服務集合</returns>
    public static IServiceCollection AddAlphaSqueezeData(
        this IServiceCollection services,
        string connectionString)
    {
        // 註冊連線工廠
        services.AddSingleton<IDbConnectionFactory>(
            _ => new SqlConnectionFactory(connectionString));

        // 註冊連線 (Scoped)
        services.AddScoped<IDbConnection>(sp =>
            sp.GetRequiredService<IDbConnectionFactory>().CreateConnection());

        // 註冊 Repositories
        services.AddScoped<IStockMetricsRepository, StockMetricsRepository>();
        services.AddScoped<IWarrantRepository, WarrantRepository>();
        services.AddScoped<ISqueezeSignalRepository, SqueezeSignalRepository>();
        services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();
        services.AddScoped<IBackfillJobRepository, BackfillJobRepository>();
        services.AddScoped<ITrackedTickerRepository, TrackedTickerRepository>();
        services.AddScoped<ICBRepository, CBRepository>();
        services.AddScoped<IDiscoveryRepository, DiscoveryRepository>();
        services.AddScoped<IUserWatchListRepository, UserWatchListRepository>();

        return services;
    }
}
