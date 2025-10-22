using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services
{
    public interface ISettingsService
    {
        Task<AppSettings> LoadAsync();
        Task SaveAsync(AppSettings settings);
    }

    public class SettingsService : ISettingsService
    {
        readonly private string _path;
        readonly private ILogger<SettingsService> _logger;

        public SettingsService(ILogger<SettingsService> logger)
        {
            _logger = logger;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "WordLens");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "settings.json");
            _logger.ZLogInformation($"设置服务初始化，配置文件路径: {_path}");
        }

        public async Task<AppSettings> LoadAsync()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    _logger.ZLogInformation($"配置文件不存在，创建默认配置");
                    var defaults = new AppSettings();
                    await SaveAsync(defaults);
                    return defaults;
                }

                _logger.ZLogInformation($"开始加载配置文件");
                var json = await File.ReadAllTextAsync(_path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, SourceGenerationContext.Default.AppSettings);
                _logger.ZLogInformation($"配置加载成功，翻译源数量: {settings?.Providers.Count ?? 0}");
                return settings ?? new AppSettings();
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"加载配置失败: {ex.Message}");
                _logger.ZLogWarning($"使用默认配置");
                return new AppSettings();
            }
        }

        public async Task SaveAsync(AppSettings settings)
        {
            try
            {
                _logger.ZLogInformation($"开始保存配置，翻译源数量: {settings.Providers.Count}");
                var json = JsonSerializer.Serialize(settings, SourceGenerationContext.Default.AppSettings);
                await File.WriteAllTextAsync(_path, json);
                _logger.ZLogInformation($"配置保存成功");
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"保存配置失败: {ex.Message}");
                throw;
            }
        }
    }
}