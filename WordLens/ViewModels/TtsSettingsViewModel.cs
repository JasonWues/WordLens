using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WordLens.Models;
using WordLens.Services;

namespace WordLens.ViewModels;

public partial class TtsSettingsViewModel : ViewModelBase
{
    private static readonly IReadOnlyList<string> BinFilePatterns = new[] { "*.bin" };
    private static readonly IReadOnlyList<string> FarFilePatterns = new[] { "*.far" };
    private static readonly IReadOnlyList<string> FstFilePatterns = new[] { "*.fst" };
    private static readonly IReadOnlyList<string> OnnxFilePatterns = new[] { "*.onnx" };
    private static readonly IReadOnlyList<string> TextFilePatterns = new[] { "*.txt" };
    private readonly IPathPickerService _pathPickerService;

    [ObservableProperty] private string ttsDataDir = string.Empty;
    [ObservableProperty] private string ttsDictDir = string.Empty;
    [ObservableProperty] private bool ttsEnabled;
    [ObservableProperty] private string ttsLexiconPath = string.Empty;
    [ObservableProperty] private string ttsModelPath = string.Empty;
    [ObservableProperty] private TtsModelType ttsModelType = TtsModelType.Vits;
    [ObservableProperty] private int ttsNumThreads = 2;
    [ObservableProperty] private string ttsProvider = "cpu";
    [ObservableProperty] private string ttsRuleFars = string.Empty;
    [ObservableProperty] private string ttsRuleFsts = string.Empty;
    [ObservableProperty] private int ttsSpeakerId;
    [ObservableProperty] private double ttsSpeed = 1.0;
    [ObservableProperty] private string ttsTokensPath = string.Empty;
    [ObservableProperty] private string ttsVocoderPath = string.Empty;
    [ObservableProperty] private string ttsVoicesPath = string.Empty;

    public TtsSettingsViewModel(IPathPickerService pathPickerService)
    {
        _pathPickerService = pathPickerService;
    }

    public List<TtsModelTypeOption> AvailableTtsModelTypes { get; } = new()
    {
        new TtsModelTypeOption(TtsModelType.Vits, "VITS / Piper"),
        new TtsModelTypeOption(TtsModelType.Kokoro, "Kokoro"),
        new TtsModelTypeOption(TtsModelType.Matcha, "Matcha")
    };

    public bool IsVitsTtsModel => TtsModelType == TtsModelType.Vits;

    public bool IsKokoroTtsModel => TtsModelType == TtsModelType.Kokoro;

    public bool IsMatchaTtsModel => TtsModelType == TtsModelType.Matcha;

    public void Load(TtsConfig config)
    {
        TtsEnabled = config.Enabled;
        TtsModelType = config.ModelType;
        TtsModelPath = config.ModelPath;
        TtsTokensPath = config.TokensPath;
        TtsVoicesPath = config.VoicesPath;
        TtsDataDir = config.DataDir;
        TtsLexiconPath = config.LexiconPath;
        TtsDictDir = config.DictDir;
        TtsVocoderPath = config.VocoderPath;
        TtsRuleFsts = config.RuleFsts;
        TtsRuleFars = config.RuleFars;
        TtsProvider = config.Provider;
        TtsNumThreads = config.NumThreads;
        TtsSpeakerId = config.SpeakerId;
        TtsSpeed = config.Speed;
    }

    public TtsConfig BuildTtsConfig()
    {
        return new TtsConfig
        {
            Enabled = TtsEnabled,
            ModelType = TtsModelType,
            ModelPath = TtsModelPath,
            TokensPath = TtsTokensPath,
            VoicesPath = TtsVoicesPath,
            DataDir = TtsDataDir,
            LexiconPath = TtsLexiconPath,
            DictDir = TtsDictDir,
            VocoderPath = TtsVocoderPath,
            RuleFsts = TtsRuleFsts,
            RuleFars = TtsRuleFars,
            Provider = string.IsNullOrWhiteSpace(TtsProvider) ? "cpu" : TtsProvider,
            NumThreads = Math.Max(1, TtsNumThreads),
            SpeakerId = Math.Max(0, TtsSpeakerId),
            Speed = Math.Clamp(TtsSpeed, 0.25, 4.0)
        };
    }

    public static TtsConfig CloneTtsConfig(TtsConfig config)
    {
        return new TtsConfig
        {
            Enabled = config.Enabled,
            ModelType = config.ModelType,
            ModelPath = config.ModelPath,
            TokensPath = config.TokensPath,
            VoicesPath = config.VoicesPath,
            DataDir = config.DataDir,
            LexiconPath = config.LexiconPath,
            DictDir = config.DictDir,
            VocoderPath = config.VocoderPath,
            RuleFsts = config.RuleFsts,
            RuleFars = config.RuleFars,
            Provider = config.Provider,
            NumThreads = config.NumThreads,
            SpeakerId = config.SpeakerId,
            Speed = config.Speed
        };
    }

    partial void OnTtsModelTypeChanged(TtsModelType value)
    {
        OnPropertyChanged(nameof(IsVitsTtsModel));
        OnPropertyChanged(nameof(IsKokoroTtsModel));
        OnPropertyChanged(nameof(IsMatchaTtsModel));
    }

    [RelayCommand]
    private async Task PickTtsModelPathAsync()
    {
        await PickSingleFilePathAsync("选择 TTS ONNX 模型", OnnxFilePatterns, path => TtsModelPath = path);
    }

    [RelayCommand]
    private async Task PickTtsTokensPathAsync()
    {
        await PickSingleFilePathAsync("选择 tokens.txt", TextFilePatterns, path => TtsTokensPath = path);
    }

    [RelayCommand]
    private async Task PickTtsVoicesPathAsync()
    {
        await PickSingleFilePathAsync("选择 voices.bin", BinFilePatterns, path => TtsVoicesPath = path);
    }

    [RelayCommand]
    private async Task PickTtsVocoderPathAsync()
    {
        await PickSingleFilePathAsync("选择 Vocoder ONNX 模型", OnnxFilePatterns, path => TtsVocoderPath = path);
    }

    [RelayCommand]
    private async Task PickTtsDataDirAsync()
    {
        await PickFolderPathAsync("选择 espeak-ng-data 目录", path => TtsDataDir = path);
    }

    [RelayCommand]
    private async Task PickTtsDictDirAsync()
    {
        await PickFolderPathAsync("选择词典目录", path => TtsDictDir = path);
    }

    [RelayCommand]
    private async Task PickTtsLexiconPathAsync()
    {
        await PickMultipleFilePathsAsync("选择 Lexicon 文件", TextFilePatterns, paths => TtsLexiconPath = paths);
    }

    [RelayCommand]
    private async Task PickTtsRuleFstsAsync()
    {
        await PickMultipleFilePathsAsync("选择 Rule FST 文件", FstFilePatterns, paths => TtsRuleFsts = paths);
    }

    [RelayCommand]
    private async Task PickTtsRuleFarsAsync()
    {
        await PickMultipleFilePathsAsync("选择 Rule FAR 文件", FarFilePatterns, paths => TtsRuleFars = paths);
    }

    private async Task PickSingleFilePathAsync(
        string title,
        IReadOnlyList<string> patterns,
        Action<string> apply)
    {
        var path = await _pathPickerService.PickFileAsync(title, patterns);
        if (!string.IsNullOrWhiteSpace(path))
            apply(path);
    }

    private async Task PickMultipleFilePathsAsync(
        string title,
        IReadOnlyList<string> patterns,
        Action<string> apply)
    {
        var paths = await _pathPickerService.PickFilesAsync(title, patterns);
        if (paths.Count > 0)
            apply(string.Join(",", paths));
    }

    private async Task PickFolderPathAsync(string title, Action<string> apply)
    {
        var path = await _pathPickerService.PickFolderAsync(title);
        if (!string.IsNullOrWhiteSpace(path))
            apply(path);
    }
}

public class TtsModelTypeOption
{
    public TtsModelTypeOption(TtsModelType value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public TtsModelType Value { get; set; }
    public string DisplayName { get; set; }
}
