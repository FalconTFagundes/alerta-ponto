using System.IO;
using NAudio.Wave;

namespace MonitorPonto.Services;

/// <summary>
/// Serviço de áudio — toca no dispositivo Speakers (Realtek) sem sobreposição.
/// Ao fechar o alerta, StopAll() para imediatamente.
/// </summary>
public static class AudioService
{
    // Índice do dispositivo Speakers (Realtek(R) Audio) — verificado via TesteAudio
    private const int DeviceIndex = 1;

    private static readonly string PathAviso = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Resources", "alerta.wav");

    private static readonly string PathUrgente = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Resources", "urgente.wav");

    private static WaveOutEvent? _device;
    private static AudioFileReader? _reader;
    private static readonly object _lock = new();
    private static CancellationTokenSource? _cts;

    // ── API pública ──────────────────────────────────────────────────────

    public static void PlayAviso()   => PlayFile(PathAviso);
    public static void PlayUrgente() => PlayFile(File.Exists(PathUrgente) ? PathUrgente : PathAviso);

    public static void StopAll()
    {
        lock (_lock)
        {
            try { _cts?.Cancel(); } catch { }
            try { _device?.Stop(); } catch { }
            try { _device?.Dispose(); _device = null; } catch { }
            try { _reader?.Dispose(); _reader = null; } catch { }
        }
    }

    // ── Interno ──────────────────────────────────────────────────────────

    private static void PlayFile(string path)
    {
        if (!File.Exists(path)) return;

        // Se já está tocando, não atropela
        lock (_lock)
        {
            if (_device?.PlaybackState == PlaybackState.Playing)
                return;
        }

        // Cancela qualquer reprodução anterior
        StopAll();

        var cts = new CancellationTokenSource();
        lock (_lock) { _cts = cts; }

        Task.Run(() =>
        {
            WaveOutEvent? dev = null;
            AudioFileReader? reader = null;
            try
            {
                reader = new AudioFileReader(path);
                dev    = new WaveOutEvent { DeviceNumber = DeviceIndex };

                lock (_lock)
                {
                    _device = dev;
                    _reader = reader;
                }

                dev.Init(reader);
                dev.Play();

                while (!cts.Token.IsCancellationRequested
                    && dev.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(50);
                }

                dev.Stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioService] {ex.Message}");
                // Fallback: dispositivo padrão
                try
                {
                    using var r2  = new AudioFileReader(path);
                    using var d2  = new WaveOutEvent { DeviceNumber = 0 };
                    d2.Init(r2);
                    d2.Play();
                    while (!cts.Token.IsCancellationRequested
                        && d2.PlaybackState == PlaybackState.Playing)
                        Thread.Sleep(50);
                }
                catch { }
            }
            finally
            {
                lock (_lock)
                {
                    try { dev?.Dispose(); } catch { }
                    try { reader?.Dispose(); } catch { }
                    if (_device == dev)   { _device = null; }
                    if (_reader == reader) { _reader = null; }
                }
            }
        }, cts.Token);
    }
}
