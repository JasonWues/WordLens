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
        }

        public async Task<AppSettings> LoadAsync()
        {
            if (!File.Exists(_path))
            {
                var defaults = new AppSettings();
                await SaveAsync(defaults);
                return defaults;
            }

            var json = await File.ReadAllTextAsync(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SourceGenerationContext.Default.AppSettings);
            _logger.ZLogInformation($"Settings loaded successfully path:{_path}");
            return settings ?? new AppSettings();
        }

        public async Task SaveAsync(AppSettings settings)
        {
            var json = JsonSerializer.Serialize(settings,SourceGenerationContext.Default.AppSettings);
            await File.WriteAllTextAsync(_path, json);
        }
    }
}