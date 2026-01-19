using FluentAssertions;
using AlphaSqueeze.Data;
using Microsoft.Data.SqlClient;

namespace AlphaSqueeze.Tests;

/// <summary>
/// DbConnectionFactory 單元測試
/// </summary>
public class DbConnectionFactoryTests
{
    private const string TestConnectionString = "Server=localhost;Database=AlphaSqueeze;Trusted_Connection=True;TrustServerCertificate=True;";

    [Fact]
    public void Constructor_ShouldThrowException_WhenConnectionStringIsNull()
    {
        // Arrange & Act
        Action act = () => new SqlConnectionFactory(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenConnectionStringIsEmpty()
    {
        // Arrange & Act
        Action act = () => new SqlConnectionFactory(string.Empty);

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenConnectionStringIsWhitespace()
    {
        // Arrange & Act
        Action act = () => new SqlConnectionFactory("   ");

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ShouldSucceed_WhenConnectionStringIsValid()
    {
        // Arrange & Act
        var factory = new SqlConnectionFactory(TestConnectionString);

        // Assert
        factory.Should().NotBeNull();
        factory.ConnectionString.Should().Be(TestConnectionString);
    }

    [Fact]
    public void CreateConnection_ShouldReturnSqlConnection()
    {
        // Arrange
        var factory = new SqlConnectionFactory(TestConnectionString);

        // Act
        var connection = factory.CreateConnection();

        // Assert
        connection.Should().NotBeNull();
        connection.Should().BeOfType<SqlConnection>();
    }

    [Fact]
    public void CreateConnection_ShouldReturnNewInstanceEachTime()
    {
        // Arrange
        var factory = new SqlConnectionFactory(TestConnectionString);

        // Act
        var connection1 = factory.CreateConnection();
        var connection2 = factory.CreateConnection();

        // Assert
        connection1.Should().NotBeSameAs(connection2);
    }

    [Fact]
    public void Factory_ShouldImplementIDbConnectionFactory()
    {
        // Arrange
        var factory = new SqlConnectionFactory(TestConnectionString);

        // Assert
        factory.Should().BeAssignableTo<IDbConnectionFactory>();
    }

    [Fact]
    public void ConnectionString_ShouldReturnOriginalValue()
    {
        // Arrange
        var factory = new SqlConnectionFactory(TestConnectionString);

        // Act
        var result = factory.ConnectionString;

        // Assert
        result.Should().Be(TestConnectionString);
    }
}
