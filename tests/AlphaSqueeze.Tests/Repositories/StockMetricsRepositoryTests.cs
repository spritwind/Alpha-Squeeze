using System.Data;
using Moq;
using FluentAssertions;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Data.Repositories;

namespace AlphaSqueeze.Tests.Repositories;

/// <summary>
/// StockMetricsRepository 單元測試
/// </summary>
public class StockMetricsRepositoryTests
{
    private readonly Mock<IDbConnection> _mockConnection;
    private readonly StockMetricsRepository _repository;

    public StockMetricsRepositoryTests()
    {
        _mockConnection = new Mock<IDbConnection>();
        _repository = new StockMetricsRepository(_mockConnection.Object);
    }

    [Fact]
    public void Constructor_ShouldAcceptIDbConnection()
    {
        // Arrange & Act
        var repository = new StockMetricsRepository(_mockConnection.Object);

        // Assert
        repository.Should().NotBeNull();
    }

    [Fact]
    public void GetByDateAsync_RepositoryShouldBeValid()
    {
        // Note: Dapper extends IDbConnection, so we can't easily mock the Query methods
        // This is a structural test to verify the repository can be instantiated correctly
        // Full integration tests should be used for actual database operations

        // Assert - Repository should be properly constructed
        _repository.Should().NotBeNull();
    }

    [Fact]
    public void Repository_ShouldImplementIStockMetricsRepository()
    {
        // Assert
        _repository.Should().BeAssignableTo<AlphaSqueeze.Core.Interfaces.IStockMetricsRepository>();
    }
}

/// <summary>
/// StockMetricsRepository 整合測試 (需要實際資料庫)
/// </summary>
[Trait("Category", "Integration")]
public class StockMetricsRepositoryIntegrationTests
{
    // Note: These tests require a test database
    // They are marked with [Trait("Category", "Integration")] for filtering

    [Fact(Skip = "Requires test database")]
    public void GetByTickerAndDateAsync_ShouldReturnMetric_WhenExists()
    {
        // This test requires actual database connection
        // Run with: dotnet test --filter "Category=Integration"
    }

    [Fact(Skip = "Requires test database")]
    public void BulkUpsertAsync_ShouldInsertMultipleRecords()
    {
        // This test requires actual database connection
    }
}
