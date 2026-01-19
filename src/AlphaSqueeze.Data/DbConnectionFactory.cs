using System.Data;
using Microsoft.Data.SqlClient;

namespace AlphaSqueeze.Data;

/// <summary>
/// 資料庫連線工廠介面
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// 建立新的資料庫連線
    /// </summary>
    /// <returns>資料庫連線物件</returns>
    IDbConnection CreateConnection();

    /// <summary>
    /// 取得連線字串
    /// </summary>
    string ConnectionString { get; }
}

/// <summary>
/// SQL Server 連線工廠實作
/// </summary>
public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// 初始化 SQL Server 連線工廠
    /// </summary>
    /// <param name="connectionString">連線字串</param>
    /// <exception cref="ArgumentNullException">當連線字串為空時拋出</exception>
    public SqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString), "Connection string cannot be null or empty.");
        }

        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public string ConnectionString => _connectionString;

    /// <inheritdoc />
    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
