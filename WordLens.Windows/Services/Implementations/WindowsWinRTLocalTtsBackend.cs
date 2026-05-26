using Windows.Foundation;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;
using WordLens.Abstractions.Services;

namespace WordLens.Windows.Services.Implementations;

public sealed class WindowsWinRTLocalTtsBackend : ILocalTtsBackend
{
    private readonly IAudioPlayerService _audioPlayer;

    public WindowsWinRTLocalTtsBackend(IAudioPlayerService audioPlayer)
    {
        _audioPlayer = audioPlayer;
    }

    public async Task SpeakAsync(
        string text,
        string? voice,
        double speed,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        using var synthesizer = new SpeechSynthesizer();
        var selectedVoice = SelectVoice(voice);
        if (selectedVoice != null)
            synthesizer.Voice = selectedVoice;

        var ssml = BuildSsml(text, speed);
        using var stream = await synthesizer.SynthesizeSsmlToStreamAsync(ssml)
            .AsTask(cancellationToken);

        var buffer = new byte[stream.Size];
        using var input = stream.AsStreamForRead();
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await input.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
                break;

            offset += read;
        }

        var waveData = offset == buffer.Length
            ? buffer
            : buffer[..offset];

        await _audioPlayer.PlayWaveAsync(waveData, cancellationToken);
    }

    public void Stop()
    {
        _audioPlayer.Stop();
    }

    private static VoiceInformation? SelectVoice(string? voice)
    {
        if (string.IsNullOrWhiteSpace(voice))
            return null;

        var trimmed = voice.Trim();
        return SpeechSynthesizer.AllVoices.FirstOrDefault(v =>
            string.Equals(v.Id, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v.Description, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v.Language, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildSsml(string text, double speed)
    {
        var escapedText = System.Security.SecurityElement.Escape(text) ?? string.Empty;
        var rate = ToProsodyRate(speed);
        return $"""
               <speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="zh-CN">
                 <prosody rate="{rate}">{escapedText}</prosody>
               </speak>
               """;
    }

    private static string ToProsodyRate(double speed)
    {
        var clamped = Math.Clamp(speed, 0.25, 4.0);
        var percentage = (int)Math.Round((clamped - 1.0) * 100);
        return percentage >= 0 ? $"+{percentage}%" : $"{percentage}%";
    }
}
