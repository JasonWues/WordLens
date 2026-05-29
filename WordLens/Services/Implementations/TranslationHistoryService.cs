using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services.Implementations;

/// <summary>
/// 翻译历史服务实现
/// </summary>
public class TranslationHistoryService : ITranslationHistoryService
{
    private readonly string _connectionString;
    private readonly Task _initializeTask;
    private readonly ILogger<TranslationHistoryService> _logger;

    public TranslationHistoryService(ILogger<TranslationHistoryService> logger)
    {
        _logger = logger;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbDir = Path.Combine(appData, "WordLens");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "translation_history.db");

        _logger.ZLogInformation($"翻译历史数据库路径: {dbPath}");

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
        }.ToString();

        _initializeTask = InitializeDatabaseAsync();
    }

    /// <summary>
    /// 初始化数据库表
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS TranslationHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SourceText TEXT NOT NULL,
                    SourceLanguage TEXT NOT NULL,
                    TargetLanguage TEXT NOT NULL,
                    ResultsJson TEXT NULL,
                    ProviderNames TEXT NULL,
                    CreatedAt INTEGER NOT NULL,
                    IsFavorite INTEGER NOT NULL DEFAULT 0
                )
                """);

            await connection.ExecuteAsync(
                """
                CREATE INDEX IF NOT EXISTS IX_TranslationHistory_CreatedAt
                ON TranslationHistory (CreatedAt)
                """);

            await connection.ExecuteAsync(
                """
                CREATE INDEX IF NOT EXISTS IX_TranslationHistory_IsFavorite
                ON TranslationHistory (IsFavorite)
                """);

            _logger.ZLogInformation($"翻译历史表初始化成功");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"初始化翻译历史表失败: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(TranslationHistory history)
    {
        try
        {
            await _initializeTask;

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            var parameters = ToSaveParameter(history);

            if (history.Id == 0)
            {
                await connection.ExecuteAsync(
                    """
                    INSERT INTO TranslationHistory (
                        SourceText,
                        SourceLanguage,
                        TargetLanguage,
                        ResultsJson,
                        ProviderNames,
                        CreatedAt,
                        IsFavorite
                    )
                    VALUES (
                        @SourceText,
                        @SourceLanguage,
                        @TargetLanguage,
                        @ResultsJson,
                        @ProviderNames,
                        @CreatedAt,
                        @IsFavorite
                    )
                    """,
                    parameters);

                var newId = await connection.ExecuteScalarAsync<long>(
                    """
                    SELECT last_insert_rowid()
                    """);
                history.Id = checked((int)newId);
                _logger.ZLogInformation($"保存翻译历史记录成功，ID: {history.Id}");
            }
            else
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE TranslationHistory
                    SET SourceText = @SourceText,
                        SourceLanguage = @SourceLanguage,
                        TargetLanguage = @TargetLanguage,
                        ResultsJson = @ResultsJson,
                        ProviderNames = @ProviderNames,
                        CreatedAt = @CreatedAt,
                        IsFavorite = @IsFavorite
                    WHERE Id = @Id
                    """,
                    parameters);
                _logger.ZLogInformation($"更新翻译历史记录成功，ID: {history.Id}");
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"保存翻译历史记录失败: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<TranslationHistory>> GetAllAsync()
    {
        try
        {
            await _initializeTask.ConfigureAwait(false);

            await using var connection = CreateConnection();
            await connection.OpenAsync().ConfigureAwait(false);
            var rows = await connection.QueryAsync<TranslationHistoryRow>(
                """
                SELECT Id,
                       SourceText,
                       SourceLanguage,
                       TargetLanguage,
                       ResultsJson,
                       ProviderNames,
                       CreatedAt,
                       IsFavorite
                FROM TranslationHistory
                ORDER BY CreatedAt DESC
                """).ConfigureAwait(false);

            var histories = rows.Select(ToModel).ToList();
            _logger.ZLogInformation($"获取所有历史记录成功，共 {histories.Count} 条");
            return histories;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"获取历史记录失败: {ex.Message}");
            return new List<TranslationHistory>();
        }
    }

    /// <inheritdoc/>
    public async Task<List<TranslationHistory>> GetPagedAsync(int skip, int take)
    {
        try
        {
            await _initializeTask.ConfigureAwait(false);

            var parameters = new PageParameter
            {
                Skip = Math.Max(0, skip),
                Take = Math.Max(0, take),
            };

            await using var connection = CreateConnection();
            await connection.OpenAsync().ConfigureAwait(false);
            var rows = await connection.QueryAsync<TranslationHistoryRow>(
                """
                SELECT Id,
                       SourceText,
                       SourceLanguage,
                       TargetLanguage,
                       ResultsJson,
                       ProviderNames,
                       CreatedAt,
                       IsFavorite
                FROM TranslationHistory
                ORDER BY CreatedAt DESC
                LIMIT @Take OFFSET @Skip
                """,
                parameters).ConfigureAwait(false);

            var histories = rows.Select(ToModel).ToList();
            _logger.ZLogDebug($"分页获取历史记录成功，跳过 {parameters.Skip}，获取 {parameters.Take}，返回 {histories.Count} 条");
            return histories;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"分页获取历史记录失败: {ex.Message}");
            return new List<TranslationHistory>();
        }
    }

    /// <inheritdoc/>
    public async Task<List<TranslationHistory>> SearchAsync(string keyword)
    {
        try
        {
            await _initializeTask.ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return await GetAllAsync().ConfigureAwait(false);
            }

            var parameters = new SearchParameter
            {
                Pattern = $"%{keyword.Trim()}%",
            };

            await using var connection = CreateConnection();
            await connection.OpenAsync().ConfigureAwait(false);
            var rows = await connection.QueryAsync<TranslationHistoryRow>(
                """
                SELECT Id,
                       SourceText,
                       SourceLanguage,
                       TargetLanguage,
                       ResultsJson,
                       ProviderNames,
                       CreatedAt,
                       IsFavorite
                FROM TranslationHistory
                WHERE SourceText LIKE @Pattern
                   OR ResultsJson LIKE @Pattern
                   OR ProviderNames LIKE @Pattern
                ORDER BY CreatedAt DESC
                """,
                parameters).ConfigureAwait(false);

            var histories = rows.Select(ToModel).ToList();
            _logger.ZLogInformation($"搜索历史记录成功，关键词: '{keyword}'，找到 {histories.Count} 条");
            return histories;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"搜索历史记录失败: {ex.Message}");
            return new List<TranslationHistory>();
        }
    }

    /// <inheritdoc/>
    public async Task<TranslationHistory?> GetByIdAsync(int id)
    {
        try
        {
            await _initializeTask;

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            var row = await connection.QueryFirstOrDefaultAsync<TranslationHistoryRow>(
                """
                SELECT Id,
                       SourceText,
                       SourceLanguage,
                       TargetLanguage,
                       ResultsJson,
                       ProviderNames,
                       CreatedAt,
                       IsFavorite
                FROM TranslationHistory
                WHERE Id = @Id
                """,
                new IdParameter { Id = id });

            if (row != null)
            {
                _logger.ZLogDebug($"根据ID获取历史记录成功，ID: {id}");
                return ToModel(row);
            }

            _logger.ZLogWarning($"未找到ID为 {id} 的历史记录");
            return null;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"根据ID获取历史记录失败，ID: {id}, 错误: {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(int id)
    {
        try
        {
            await _initializeTask;

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            var affectedRows = await connection.ExecuteAsync(
                """
                DELETE FROM TranslationHistory
                WHERE Id = @Id
                """,
                new IdParameter { Id = id });

            if (affectedRows > 0)
            {
                _logger.ZLogInformation($"删除历史记录成功，ID: {id}");
            }
            else
            {
                _logger.ZLogWarning($"尝试删除不存在的历史记录，ID: {id}");
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"删除历史记录失败，ID: {id}, 错误: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task ClearAllAsync()
    {
        try
        {
            await _initializeTask;

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                DELETE FROM TranslationHistory
                """);
            _logger.ZLogInformation($"清空所有历史记录成功");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"清空历史记录失败: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetCountAsync()
    {
        try
        {
            await _initializeTask.ConfigureAwait(false);

            await using var connection = CreateConnection();
            await connection.OpenAsync().ConfigureAwait(false);
            var count = await connection.ExecuteScalarAsync<long>(
                """
                SELECT COUNT(1)
                FROM TranslationHistory
                """).ConfigureAwait(false);
            _logger.ZLogDebug($"获取历史记录总数: {count}");
            return count > int.MaxValue ? int.MaxValue : (int)count;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"获取历史记录总数失败: {ex.Message}");
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task ToggleFavoriteAsync(int id)
    {
        try
        {
            await _initializeTask;

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            var affectedRows = await connection.ExecuteAsync(
                """
                UPDATE TranslationHistory
                SET IsFavorite = CASE WHEN IsFavorite = 1 THEN 0 ELSE 1 END
                WHERE Id = @Id
                """,
                new IdParameter { Id = id });

            if (affectedRows > 0)
            {
                _logger.ZLogInformation($"切换收藏状态成功，ID: {id}");
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"切换收藏状态失败，ID: {id}, 错误: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<TranslationHistory>> GetFavoritesAsync()
    {
        try
        {
            await _initializeTask.ConfigureAwait(false);

            await using var connection = CreateConnection();
            await connection.OpenAsync().ConfigureAwait(false);
            var rows = await connection.QueryAsync<TranslationHistoryRow>(
                """
                SELECT Id,
                       SourceText,
                       SourceLanguage,
                       TargetLanguage,
                       ResultsJson,
                       ProviderNames,
                       CreatedAt,
                       IsFavorite
                FROM TranslationHistory
                WHERE IsFavorite = 1
                ORDER BY CreatedAt DESC
                """).ConfigureAwait(false);

            var favorites = rows.Select(ToModel).ToList();
            _logger.ZLogInformation($"获取收藏记录成功，共 {favorites.Count} 条");
            return favorites;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"获取收藏记录失败: {ex.Message}");
            return new List<TranslationHistory>();
        }
    }

    private DbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private static SaveHistoryParameter ToSaveParameter(TranslationHistory history)
    {
        return new SaveHistoryParameter
        {
            Id = history.Id,
            SourceText = history.SourceText,
            SourceLanguage = history.SourceLanguage,
            TargetLanguage = history.TargetLanguage,
            ResultsJson = history.ResultsJson,
            ProviderNames = history.ProviderNames,
            CreatedAt = history.CreatedAt.Ticks,
            IsFavorite = history.IsFavorite ? 1 : 0,
        };
    }

    private static TranslationHistory ToModel(TranslationHistoryRow row)
    {
        return new TranslationHistory
        {
            Id = row.Id,
            SourceText = row.SourceText,
            SourceLanguage = row.SourceLanguage,
            TargetLanguage = row.TargetLanguage,
            ResultsJson = row.ResultsJson,
            ProviderNames = row.ProviderNames,
            CreatedAt = new DateTime(row.CreatedAt),
            IsFavorite = row.IsFavorite != 0,
        };
    }

}

internal sealed class TranslationHistoryRow
{
    public int Id { get; set; }

    public string SourceText { get; set; } = string.Empty;

    public string SourceLanguage { get; set; } = string.Empty;

    public string TargetLanguage { get; set; } = string.Empty;

    public string? ResultsJson { get; set; }

    public string? ProviderNames { get; set; }

    public long CreatedAt { get; set; }

    public int IsFavorite { get; set; }
}

internal sealed class SaveHistoryParameter
{
    public int Id { get; set; }

    public string SourceText { get; set; } = string.Empty;

    public string SourceLanguage { get; set; } = string.Empty;

    public string TargetLanguage { get; set; } = string.Empty;

    public string? ResultsJson { get; set; }

    public string? ProviderNames { get; set; }

    public long CreatedAt { get; set; }

    public int IsFavorite { get; set; }
}

internal sealed class IdParameter
{
    public int Id { get; set; }
}

internal sealed class PageParameter
{
    public int Skip { get; set; }

    public int Take { get; set; }
}

internal sealed class SearchParameter
{
    public string Pattern { get; set; } = string.Empty;
}
