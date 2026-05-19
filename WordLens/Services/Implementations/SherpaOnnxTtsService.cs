using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SherpaOnnx;
using WordLens.Models;
using WordLens.Services;
using ZLogger;

namespace WordLens.Services.Implementations;

public sealed class SherpaOnnxTtsService : ITtsService, IDisposable
{
    private readonly IAudioPlayerService _audioPlayer;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<SherpaOnnxTtsService> _logger;
    private readonly ISettingsService _settingsService;
    private string? _cachedConfigKey;
    private OfflineTts? _cachedTts;

    public SherpaOnnxTtsService(
        ISettingsService settingsService,
        IAudioPlayerService audioPlayer,
        ILogger<SherpaOnnxTtsService> logger)
    {
        _settingsService = settingsService;
        _audioPlayer = audioPlayer;
        _logger = logger;
    }

    public async Task SpeakAsync(string? text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        await _gate.WaitAsync(cancellationToken);

        try
        {
            _audioPlayer.Stop();

            var settings = await _settingsService.LoadAsync();
            var config = settings.Tts;
            if (!config.Enabled)
            {
                _logger.ZLogWarning($"本地 TTS 未启用，跳过朗读");
                return;
            }

            ValidateConfig(config);

            var tts = GetOrCreateTts(config);
            var waveData = await Task.Run(
                () => GenerateWaveData(tts, text.Trim(), config),
                cancellationToken);

            await _audioPlayer.PlayWaveAsync(waveData, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"本地 TTS 朗读失败: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Stop()
    {
        _audioPlayer.Stop();
    }

    public void Dispose()
    {
        _cachedTts?.Dispose();
        _gate.Dispose();
    }

    private OfflineTts GetOrCreateTts(TtsConfig config)
    {
        var key = BuildConfigKey(config);
        if (_cachedTts != null && string.Equals(_cachedConfigKey, key, StringComparison.Ordinal))
            return _cachedTts;

        _cachedTts?.Dispose();
        _cachedTts = new OfflineTts(CreateOfflineTtsConfig(config));
        _cachedConfigKey = key;
        return _cachedTts;
    }

    private static OfflineTtsConfig CreateOfflineTtsConfig(TtsConfig config)
    {
        var modelConfig = new OfflineTtsModelConfig
        {
            NumThreads = Math.Max(1, config.NumThreads),
            Debug = 0,
            Provider = Normalize(config.Provider, "cpu")
        };

        switch (config.ModelType)
        {
            case TtsModelType.Kokoro:
                modelConfig.Kokoro = new OfflineTtsKokoroModelConfig
                {
                    Model = Normalize(config.ModelPath),
                    Voices = Normalize(config.VoicesPath),
                    Tokens = Normalize(config.TokensPath),
                    DataDir = Normalize(config.DataDir),
                    Lexicon = Normalize(config.LexiconPath),
                    DictDir = Normalize(config.DictDir),
                    Lang = string.Empty,
                    LengthScale = 1.0f
                };
                break;

            case TtsModelType.Matcha:
                modelConfig.Matcha = new OfflineTtsMatchaModelConfig
                {
                    AcousticModel = Normalize(config.ModelPath),
                    Vocoder = Normalize(config.VocoderPath),
                    Lexicon = Normalize(config.LexiconPath),
                    Tokens = Normalize(config.TokensPath),
                    DataDir = Normalize(config.DataDir),
                    DictDir = Normalize(config.DictDir),
                    NoiseScale = 0.667f,
                    LengthScale = 1.0f
                };
                break;

            case TtsModelType.Vits:
            default:
                modelConfig.Vits = new OfflineTtsVitsModelConfig
                {
                    Model = Normalize(config.ModelPath),
                    Tokens = Normalize(config.TokensPath),
                    DataDir = Normalize(config.DataDir),
                    Lexicon = Normalize(config.LexiconPath),
                    DictDir = Normalize(config.DictDir),
                    NoiseScale = 0.667f,
                    NoiseScaleW = 0.8f,
                    LengthScale = 1.0f
                };
                break;
        }

        return new OfflineTtsConfig
        {
            Model = modelConfig,
            RuleFsts = Normalize(config.RuleFsts),
            RuleFars = Normalize(config.RuleFars),
            MaxNumSentences = 1,
            SilenceScale = 1.0f
        };
    }

    private static void ValidateConfig(TtsConfig config)
    {
        RequireFile(config.ModelPath, "模型文件");

        if (config.ModelType == TtsModelType.Matcha)
            RequireFile(config.VocoderPath, "Vocoder 文件");
        else if (config.ModelType == TtsModelType.Kokoro)
            RequireFile(config.VoicesPath, "Voices 文件");

        if (!string.IsNullOrWhiteSpace(config.TokensPath))
            RequireFile(config.TokensPath, "Tokens 文件");
        if (!string.IsNullOrWhiteSpace(config.LexiconPath))
            RequirePathList(config.LexiconPath, "Lexicon 文件");
        if (!string.IsNullOrWhiteSpace(config.RuleFsts))
            RequirePathList(config.RuleFsts, "Rule FST 文件");
        if (!string.IsNullOrWhiteSpace(config.RuleFars))
            RequirePathList(config.RuleFars, "Rule FAR 文件");
        if (!string.IsNullOrWhiteSpace(config.DataDir))
            RequireDirectory(config.DataDir, "数据目录");
        if (!string.IsNullOrWhiteSpace(config.DictDir))
            RequireDirectory(config.DictDir, "词典目录");
    }

    private static void RequireFile(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException($"{name}不存在。", path);
    }

    private static void RequireDirectory(string path, string name)
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"{name}不存在: {path}");
    }

    private static void RequirePathList(string paths, string name)
    {
        foreach (var path in paths.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            RequireFile(path, name);
    }

    private static string BuildConfigKey(TtsConfig config)
    {
        return string.Join(
            "|",
            config.Enabled,
            config.ModelType,
            config.ModelPath,
            config.TokensPath,
            config.VoicesPath,
            config.DataDir,
            config.LexiconPath,
            config.DictDir,
            config.VocoderPath,
            config.RuleFsts,
            config.RuleFars,
            config.Provider,
            config.NumThreads);
    }

    private static string Normalize(string? value, string fallback = "")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static byte[] GenerateWaveData(OfflineTts tts, string text, TtsConfig config)
    {
        var audio = tts.Generate(text, (float)config.Speed, config.SpeakerId);
        try
        {
            return CreateWaveData(audio.Samples, audio.SampleRate);
        }
        finally
        {
            audio.Dispose();
        }
    }

    private static byte[] CreateWaveData(float[] samples, int sampleRate)
    {
        if (sampleRate <= 0)
            throw new InvalidOperationException("TTS 生成的音频采样率无效。");

        const short channelCount = 1;
        const short bitsPerSample = 16;
        const short bytesPerSample = bitsPerSample / 8;

        var dataLength = checked(samples.Length * bytesPerSample);
        using var stream = new MemoryStream(44 + dataLength);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        WriteAscii(writer, "RIFF");
        writer.Write(36 + dataLength);
        WriteAscii(writer, "WAVE");
        WriteAscii(writer, "fmt ");
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channelCount);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channelCount * bytesPerSample);
        writer.Write((short)(channelCount * bytesPerSample));
        writer.Write(bitsPerSample);
        WriteAscii(writer, "data");
        writer.Write(dataLength);

        foreach (var sample in samples)
            writer.Write(ToPcm16(sample));

        writer.Flush();
        return stream.ToArray();
    }

    private static short ToPcm16(float sample)
    {
        sample = Math.Clamp(sample, -1.0f, 1.0f);
        return sample < 0
            ? (short)(sample * 32768.0f)
            : (short)(sample * 32767.0f);
    }

    private static void WriteAscii(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.ASCII.GetBytes(value));
    }
}
