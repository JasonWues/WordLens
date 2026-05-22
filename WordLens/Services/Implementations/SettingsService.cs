using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services.Implementations;

/// <summary>
/// 应用设置服务实现
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly EncryptionService _encryptionService;
    private readonly ILogger<SettingsService> _logger;
    private readonly string _path;

    public SettingsService(
        ILogger<SettingsService> logger,
        EncryptionService encryptionService)
    {
        _logger = logger;
        _encryptionService = encryptionService;

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

            if (settings == null)
            {
                _logger.ZLogWarning($"配置反序列化失败，使用默认配置");
                return new AppSettings();
            }


            // 自动迁移：检查并加密未加密的API Key
            var needsSave = false;
            settings.Providers ??= new AppSettings().Providers;
            settings.OcrProviders ??= new List<ProviderConfig>();
            needsSave |= NormalizeHotkeys(settings);
            needsSave |= NormalizeOcrProviders(settings);

            foreach (var provider in settings.Providers)
                if (!string.IsNullOrEmpty(provider.ApiKey) &&
                    !_encryptionService.IsEncrypted(provider.ApiKey))
                {
                    _logger.ZLogInformation($"检测到未加密的API Key，正在加密: {provider.Name}");
                    provider.ApiKey = _encryptionService.Encrypt(provider.ApiKey);
                    needsSave = true;
                }

            foreach (var provider in settings.OcrProviders)
                if (!string.IsNullOrEmpty(provider.ApiKey) &&
                    !_encryptionService.IsEncrypted(provider.ApiKey))
                {
                    _logger.ZLogInformation($"检测到未加密的OCR API Key，正在加密: {provider.Name}");
                    provider.ApiKey = _encryptionService.Encrypt(provider.ApiKey);
                    needsSave = true;
                }

            // 如果有未加密的配置，自动保存加密后的版本
            if (needsSave)
            {
                _logger.ZLogInformation($"自动保存加密后的配置");
                await SaveAsync(settings);
            }

            _logger.ZLogInformation($"配置加载成功，翻译源数量: {settings.Providers.Count}，OCR源数量: {settings.OcrProviders.Count}");
            return settings;
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
            settings.Providers ??= new AppSettings().Providers;
            settings.OcrProviders ??= new List<ProviderConfig>();
            NormalizeHotkeys(settings);
            NormalizeOcrProviders(settings);

            _logger.ZLogInformation($"开始保存配置，翻译源数量: {settings.Providers.Count}，OCR源数量: {settings.OcrProviders.Count}");

            // 确保所有API Key都已加密
            foreach (var provider in settings.Providers)
                if (!string.IsNullOrEmpty(provider.ApiKey) &&
                    !_encryptionService.IsEncrypted(provider.ApiKey))
                {
                    _logger.ZLogDebug($"保存前加密API Key: {provider.Name}");
                    provider.ApiKey = _encryptionService.Encrypt(provider.ApiKey);
                }

            foreach (var provider in settings.OcrProviders)
                if (!string.IsNullOrEmpty(provider.ApiKey) &&
                    !_encryptionService.IsEncrypted(provider.ApiKey))
                {
                    _logger.ZLogDebug($"保存前加密OCR API Key: {provider.Name}");
                    provider.ApiKey = _encryptionService.Encrypt(provider.ApiKey);
                }

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

    private static bool NormalizeOcrProviders(AppSettings settings)
    {
        var needsSave = false;

        if (settings.OcrProviders.Count == 0)
        {
            settings.OcrProviders.Add(AppSettings.CreateDefaultOcrProvider());
            needsSave = true;
        }

        if (string.IsNullOrWhiteSpace(settings.SelectedOcrProvider) ||
            settings.OcrProviders.All(p => p.Name != settings.SelectedOcrProvider))
        {
            settings.SelectedOcrProvider =
                settings.OcrProviders.FirstOrDefault(p => p.IsEnabled)?.Name ??
                settings.OcrProviders[0].Name;
            needsSave = true;
        }

        return needsSave;
    }

    private static bool NormalizeHotkeys(AppSettings settings)
    {
        var needsSave = false;

        if (settings.Hotkey == null)
        {
            settings.Hotkey = HotkeyConfig.Default();
            needsSave = true;
        }

        if (settings.OcrHotkey == null)
        {
            settings.OcrHotkey = HotkeyConfig.DefaultOcr();
            needsSave = true;
        }

        if (AreHotkeysEqual(settings.Hotkey, settings.OcrHotkey) &&
            AreHotkeysEqual(settings.Hotkey, HotkeyConfig.Default()))
        {
            settings.OcrHotkey = HotkeyConfig.DefaultOcr();
            needsSave = true;
        }

        return needsSave;
    }

    private static bool AreHotkeysEqual(HotkeyConfig left, HotkeyConfig right)
    {
        return left.Modifiers == right.Modifiers && left.Key == right.Key;
    }
}
