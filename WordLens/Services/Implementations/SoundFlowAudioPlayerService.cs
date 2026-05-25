using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Providers;
using SoundFlow.Structs;
using WordLens.Abstractions.Services;
using WordLens.Services;
using ZLogger;

namespace WordLens.Services.Implementations;

public sealed class SoundFlowAudioPlayerService : IAudioPlayerService, IDisposable
{
    private readonly AudioFormat _format = AudioFormat.DvdHq;
    private readonly ILogger<SoundFlowAudioPlayerService> _logger;
    private readonly Lock _sync = new();
    private AudioPlaybackDevice? _device;
    private MiniAudioEngine? _engine;
    private PlaybackSession? _session;

    public SoundFlowAudioPlayerService(ILogger<SoundFlowAudioPlayerService> logger)
    {
        _logger = logger;
    }

    public async Task PlayWaveAsync(byte[] waveData, CancellationToken cancellationToken)
    {
        if (waveData.Length == 0)
            return;

        Stop();

        PlaybackSession session;
        lock (_sync)
        {
            var engine = EnsureEngine();
            var device = EnsureDevice(engine);
            var provider = new StreamDataProvider(engine, _format, new MemoryStream(waveData, writable: false));
            var player = new SoundPlayer(engine, _format, provider)
            {
                Name = "WordLens TTS Player"
            };

            session = new PlaybackSession(device, player);
            player.PlaybackEnded += session.OnPlaybackEnded;
            device.MasterMixer.AddComponent(player);

            if (!device.IsRunning)
                device.Start();

            _session = session;
            player.Play();
        }

        try
        {
            await session.Completion.Task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Stop();
            throw;
        }
        finally
        {
            CleanupSession(session, complete: false);
        }
    }

    public void Stop()
    {
        PlaybackSession? session;
        lock (_sync)
        {
            session = _session;
            _session = null;
        }

        if (session != null)
            CleanupSession(session, complete: true);
    }

    public void Dispose()
    {
        Stop();

        lock (_sync)
        {
            try
            {
                if (_device?.IsRunning == true)
                    _device.Stop();
            }
            catch (Exception ex)
            {
                _logger.ZLogWarning(ex, $"停止 SoundFlow 输出设备失败: {ex.Message}");
            }

            _device?.Dispose();
            _device = null;
            _engine?.Dispose();
            _engine = null;
        }
    }

    private MiniAudioEngine EnsureEngine()
    {
        if (_engine is { IsDisposed: false })
            return _engine;

        _engine = new MiniAudioEngine();
        return _engine;
    }

    private AudioPlaybackDevice EnsureDevice(MiniAudioEngine engine)
    {
        if (_device is { IsDisposed: false })
            return _device;

        engine.UpdateAudioDevicesInfo();
        var defaultDevice = engine.PlaybackDevices.FirstOrDefault(static d => d.IsDefault);
        _device = engine.InitializePlaybackDevice(defaultDevice.Id == IntPtr.Zero ? null : defaultDevice, _format);
        return _device;
    }

    private void CleanupSession(PlaybackSession session, bool complete)
    {
        if (!session.TryBeginCleanup())
        {
            if (complete)
                session.Completion.TrySetResult();
            return;
        }

        lock (_sync)
        {
            if (ReferenceEquals(_session, session))
                _session = null;
        }

        try
        {
            session.Player.PlaybackEnded -= session.OnPlaybackEnded;
            session.Player.Stop();
            session.Device.MasterMixer.RemoveComponent(session.Player);
            session.Player.Dispose();
        }
        catch (Exception ex)
        {
            _logger.ZLogWarning(ex, $"清理 SoundFlow 播放会话失败: {ex.Message}");
        }
        finally
        {
            if (complete)
                session.Completion.TrySetResult();
        }
    }

    private sealed class PlaybackSession
    {
        private int _isCleanedUp;

        public PlaybackSession(AudioPlaybackDevice device, SoundPlayer player)
        {
            Device = device;
            Player = player;
        }

        public AudioPlaybackDevice Device { get; }
        public SoundPlayer Player { get; }
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void OnPlaybackEnded(object? sender, EventArgs e)
        {
            Completion.TrySetResult();
        }

        public bool TryBeginCleanup()
        {
            return Interlocked.Exchange(ref _isCleanedUp, 1) == 0;
        }
    }
}
