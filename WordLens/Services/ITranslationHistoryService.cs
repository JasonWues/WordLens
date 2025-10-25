using System.Collections.Generic;
using System.Threading.Tasks;
using WordLens.Models;

namespace WordLens.Services;

/// <summary>
/// 翻译历史服务接口
/// </summary>
public interface ITranslationHistoryService
{
    /// <summary>
    /// 保存翻译历史记录
    /// </summary>
    Task SaveAsync(TranslationHistory history);

    /// <summary>
    /// 获取所有历史记录（按时间倒序）
    /// </summary>
    Task<List<TranslationHistory>> GetAllAsync();

    /// <summary>
    /// 分页获取历史记录
    /// </summary>
    /// <param name="skip">跳过的记录数</param>
    /// <param name="take">获取的记录数</param>
    Task<List<TranslationHistory>> GetPagedAsync(int skip, int take);

    /// <summary>
    /// 搜索历史记录
    /// </summary>
    /// <param name="keyword">搜索关键词（在源文本中搜索）</param>
    Task<List<TranslationHistory>> SearchAsync(string keyword);

    /// <summary>
    /// 根据ID获取历史记录
    /// </summary>
    Task<TranslationHistory?> GetByIdAsync(int id);

    /// <summary>
    /// 删除指定的历史记录
    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// 清空所有历史记录
    /// </summary>
    Task ClearAllAsync();

    /// <summary>
    /// 获取历史记录总数
    /// </summary>
    Task<int> GetCountAsync();

    /// <summary>
    /// 切换收藏状态
    /// </summary>
    Task ToggleFavoriteAsync(int id);

    /// <summary>
    /// 获取所有收藏的记录
    /// </summary>
    Task<List<TranslationHistory>> GetFavoritesAsync();
}