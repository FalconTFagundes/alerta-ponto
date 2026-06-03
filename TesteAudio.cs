using NAudio.Wave;

namespace MonitorPonto;

public static class TesteAudio
{
    public static void ListarDispositivos()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Total de dispositivos: {WaveOut.DeviceCount}");
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            sb.AppendLine($"  [{i}] {caps.ProductName}");
        }
        System.Windows.MessageBox.Show(sb.ToString(), "Dispositivos de Áudio");
    }
}
