using System.Threading.Tasks;
using WordLens.Models;

namespace WordLens.Services;

/// <summary>
/// 应用设置服务接口
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// 加载应用设置
    /// </summary>
    Task<AppSettings> LoadAsync();

    /// <summary>
    /// 保存应用设置
    /// </summary>
    Task SaveAsync(AppSettings settings);
}