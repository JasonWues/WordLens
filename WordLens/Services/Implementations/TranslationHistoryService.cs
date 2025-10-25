using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SQLite;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services.Implementations;

/// <summary>
/// 翻译历史服务实现
/// </summary>
public class TranslationHistoryService : ITranslationHistoryService
{
    private readonly SQLiteAsyncConnection _database;
    private readonly ILogger<TranslationHistoryService> _logger;

    public TranslationHistoryService(ILogger<TranslationHistoryService> logger)
    {
        _logger = logger;

        // 获取数据库文件路径
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbDir = Path.Combine(appData, "WordLens");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "translation_history.db");

        _logger.ZLogInformation($"翻译历史数据库路径: {dbPath}");

        // 初始化数据库连接
        _database = new SQLiteAsyncConnection(dbPath);

        // 创建表（如果不存在）
        _ = InitializeDatabaseAsync();
    }

    /// <summary>
    /// 初始化数据库表
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        try
        {
            await _database.CreateTableAsync<TranslationHistory>();
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
            if (history.Id == 0)
            {
                // 新记录，插入
                await _database.InsertAsync(history);
                _logger.ZLogInformation($"保存翻译历史记录成功，ID: {history.Id}");
            }
            else
            {
                // 已存在的记录，更新
                await _database.UpdateAsync(history);
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
            var histories = await _database.Table<TranslationHistory>()
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();

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
            var histories = await _database.Table<TranslationHistory>()
                .OrderByDescending(h => h.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            _logger.ZLogDebug($"分页获取历史记录成功，跳过 {skip}，获取 {take}，返回 {histories.Count} 条");
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
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return await GetAllAsync();
            }

            // SQLite 查询，使用 LIKE 进行模糊搜索
            var histories = await _database.Table<TranslationHistory>()
                .Where(h => h.SourceText.Contains(keyword))
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();

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
            var history = await _database.Table<TranslationHistory>()
                .Where(h => h.Id == id)
                .FirstOrDefaultAsync();

            if (history != null)
            {
                _logger.ZLogDebug($"根据ID获取历史记录成功，ID: {id}");
            }
            else
            {
                _logger.ZLogWarning($"未找到ID为 {id} 的历史记录");
            }

            return history;
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
            var history = await GetByIdAsync(id);
            if (history != null)
            {
                await _database.DeleteAsync(history);
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
            await _database.DeleteAllAsync<TranslationHistory>();
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
            var count = await _database.Table<TranslationHistory>().CountAsync();
            _logger.ZLogDebug($"获取历史记录总数: {count}");
            return count;
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
            var history = await GetByIdAsync(id);
            if (history != null)
            {
                history.IsFavorite = !history.IsFavorite;
                await _database.UpdateAsync(history);
                _logger.ZLogInformation($"切换收藏状态成功，ID: {id}, 新状态: {history.IsFavorite}");
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
            var favorites = await _database.Table<TranslationHistory>()
                .Where(h => h.IsFavorite)
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();

            _logger.ZLogInformation($"获取收藏记录成功，共 {favorites.Count} 条");
            return favorites;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"获取收藏记录失败: {ex.Message}");
            return new List<TranslationHistory>();
        }
    }
}