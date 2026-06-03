using System.IO;
using NAudio.Wave;

namespace MonitorPonto.Services;

public static class AudioService
{
    // Índice fixo do Speakers (Realtek(R) Audio) — verificado via TesteAudio
    private const int DeviceIndex = 1;

    private static readonly string AudioPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Resources", "alerta.wav");

    public static void PlayAlert()
    {
        Task.Run(() =>
        {
            try
            {
                if (!File.Exists(AudioPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Áudio não encontrado: {AudioPath}");
                    return;
                }

                using var audioFile = new AudioFileReader(AudioPath);
                using var outputDevice = new WaveOutEvent { DeviceNumber = DeviceIndex };

                outputDevice.Init(audioFile);
                outputDevice.Play();

                while (outputDevice.PlaybackState == PlaybackState.Playing)
                    Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro áudio: {ex.Message}");
                // Fallback: dispositivo padrão
                try
                {
                    using var audioFile = new AudioFileReader(AudioPath);
                    using var outputDevice = new WaveOutEvent { DeviceNumber = 0 };
                    outputDevice.Init(audioFile);
                    outputDevice.Play();
                    while (outputDevice.PlaybackState == PlaybackState.Playing)
                        Thread.Sleep(50);
                }
                catch { }
            }
        });
    }
}