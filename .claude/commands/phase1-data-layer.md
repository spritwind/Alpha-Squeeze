# Phase 1: 資料層開發指引

## 目標
建立完整的 MSSQL 資料庫結構與 .NET Dapper 資料存取層。

## 前置條件
- MSSQL Server 已安裝並運行
- .NET 8 SDK 已安裝

## 開發任務

### Task 1.1: 建立 .NET Solution 結構
```bash
# 在專案根目錄執行
dotnet new sln -n AlphaSqueeze
dotnet new classlib -n AlphaSqueeze.Core -o src/AlphaSqueeze.Core
dotnet new classlib -n AlphaSqueeze.Data -o src/AlphaSqueeze.Data
dotnet new classlib -n AlphaSqueeze.Shared -o src/AlphaSqueeze.Shared
dotnet new webapi -n AlphaSqueeze.Api -o src/AlphaSqueeze.Api
dotnet new xunit -n AlphaSqueeze.Tests -o tests/AlphaSqueeze.Tests

# 加入專案到 Solution
dotnet sln add src/AlphaSqueeze.Core
dotnet sln add src/AlphaSqueeze.Data
dotnet sln add src/AlphaSqueeze.Shared
dotnet sln add src/AlphaSqueeze.Api
dotnet sln add tests/AlphaSqueeze.Tests

# 設定專案參考
dotnet add src/AlphaSqueeze.Data reference src/AlphaSqueeze.Core
dotnet add src/AlphaSqueeze.Api reference src/AlphaSqueeze.Data
dotnet add src/AlphaSqueeze.Api reference src/AlphaSqueeze.Shared
dotnet add tests/AlphaSqueeze.Tests reference src/AlphaSqueeze.Data
```

### Task 1.2: 安裝 NuGet 套件
```bash
# AlphaSqueeze.Data
dotnet add src/AlphaSqueeze.Data package Dapper
dotnet add src/AlphaSqueeze.Data package Microsoft.Data.SqlClient

# AlphaSqueeze.Api
dotnet add src/AlphaSqueeze.Api package Grpc.Net.Client
dotnet add src/AlphaSqueeze.Api package Google.Protobuf
dotnet add src/AlphaSqueeze.Api package Grpc.Tools

# AlphaSqueeze.Tests
dotnet add tests/AlphaSqueeze.Tests package Moq
dotnet add tests/AlphaSqueeze.Tests package FluentAssertions
```

### Task 1.3: 建立 Domain Models (AlphaSqueeze.Core)

建立以下 Entity 類別：

```csharp
// src/AlphaSqueeze.Core/Entities/DailyStockMetric.cs
namespace AlphaSqueeze.Core.Entities;

public class DailyStockMetric
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public DateTime TradeDate { get; set; }
    public decimal? ClosePrice { get; set; }
    public decimal? OpenPrice { get; set; }
    public decimal? HighPrice { get; set; }
    public decimal? LowPrice { get; set; }
    public long? BorrowingBalance { get; set; }
    public int? BorrowingBalanceChange { get; set; }
    public long? MarginBalance { get; set; }
    public long? ShortBalance { get; set; }
    public decimal? MarginRatio { get; set; }
    public decimal? HistoricalVolatility20D { get; set; }
    public long? Volume { get; set; }
    public long? Turnover { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

建立以下 Entities:
- `DailyStockMetric`
- `WarrantMarketData`
- `SqueezeSignal`
- `CBMarketData`
- `SystemLog`

### Task 1.4: 建立 Repository Interfaces (AlphaSqueeze.Core)

```csharp
// src/AlphaSqueeze.Core/Interfaces/IStockMetricsRepository.cs
namespace AlphaSqueeze.Core.Interfaces;

public interface IStockMetricsRepository
{
    Task<DailyStockMetric?> GetByTickerAndDateAsync(string ticker, DateTime date);
    Task<IEnumerable<DailyStockMetric>> GetByDateAsync(DateTime date);
    Task<IEnumerable<DailyStockMetric>> GetHistoryAsync(string ticker, DateTime startDate, DateTime endDate);
    Task<int> UpsertAsync(DailyStockMetric metric);
    Task<int> BulkUpsertAsync(IEnumerable<DailyStockMetric> metrics);
}
```

建立以下 Interfaces:
- `IStockMetricsRepository`
- `IWarrantRepository`
- `ISqueezeSignalRepository`

### Task 1.5: 實作 Dapper Repositories (AlphaSqueeze.Data)

```csharp
// src/AlphaSqueeze.Data/Repositories/StockMetricsRepository.cs
namespace AlphaSqueeze.Data.Repositories;

public class StockMetricsRepository : IStockMetricsRepository
{
    private readonly IDbConnection _connection;

    public StockMetricsRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<DailyStockMetric>> GetByDateAsync(DateTime date)
    {
        const string sql = @"
            SELECT Id, Ticker, TradeDate, ClosePrice, OpenPrice, HighPrice, LowPrice,
                   BorrowingBalance, BorrowingBalanceChange, MarginBalance, ShortBalance,
                   MarginRatio, HistoricalVolatility20D, Volume, Turnover,
                   CreatedAt, UpdatedAt
            FROM DailyStockMetrics
            WHERE TradeDate = @TradeDate
            ORDER BY Ticker";

        return await _connection.QueryAsync<DailyStockMetric>(sql, new { TradeDate = date });
    }

    public async Task<int> BulkUpsertAsync(IEnumerable<DailyStockMetric> metrics)
    {
        // 使用 SqlBulkCopy 實作批量插入
        // 或使用 Dapper Plus
    }
}
```

### Task 1.6: 建立資料庫連線管理

```csharp
// src/AlphaSqueeze.Data/DbConnectionFactory.cs
namespace AlphaSqueeze.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}

public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
```

### Task 1.7: 建立單元測試

```csharp
// tests/AlphaSqueeze.Tests/Repositories/StockMetricsRepositoryTests.cs
namespace AlphaSqueeze.Tests.Repositories;

public class StockMetricsRepositoryTests
{
    [Fact]
    public async Task GetByDateAsync_ReturnsMetrics_WhenDataExists()
    {
        // Arrange
        var mockConnection = new Mock<IDbConnection>();
        var repository = new StockMetricsRepository(mockConnection.Object);
        var testDate = new DateTime(2024, 1, 15);

        // Act
        var result = await repository.GetByDateAsync(testDate);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task BulkUpsertAsync_InsertsRecords_Successfully()
    {
        // 測試批量插入邏輯
    }
}
```

## 驗收標準

### 功能驗收
- [ ] Solution 結構正確建立
- [ ] 所有 Entity 類別完成
- [ ] Repository Interface 定義完成
- [ ] Dapper Repository 實作完成
- [ ] 資料庫連線工廠運作正常
- [ ] 可成功執行 database/schema.sql

### 測試驗收
- [ ] Repository 單元測試通過
- [ ] 測試覆蓋率 > 80%
- [ ] 可連接測試資料庫執行整合測試

### 品質檢查
- [ ] 無編譯警告
- [ ] 程式碼符合 C# 命名規範
- [ ] 適當的 XML 註解
- [ ] 無 SQL Injection 風險

## 執行測試
```bash
dotnet test tests/AlphaSqueeze.Tests --filter "Category=DataLayer"
```

## 完成後輸出
1. 完整的 .NET Solution 結構
2. 所有 Entity 和 Repository 類別
3. 通過的單元測試報告
4. 資料庫連線驗證結果
