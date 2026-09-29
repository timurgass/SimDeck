using System.IO;
using System.IO.MemoryMappedFiles;
using SimDeck.Core;

namespace SimDeck.App;

internal sealed class ScsSharedMemoryReader : IDisposable
{
    MemoryMappedFile? map;
    MemoryMappedViewAccessor? view;
    readonly byte[] buffer = new byte[ScsTelemetryParser.SnapshotSize];

    public bool TryRead(out ulong timestamp, out Telemetry? telemetry, out string error)
    {
        timestamp = 0;
        telemetry = null;
        error = "";
        try
        {
            EnsureOpen();
            view!.ReadArray(0, buffer, 0, buffer.Length);
            if (buffer[0] == 0)
            {
                error = "Плагин SCS загружен, но грузовик ещё не активен.";
                return false;
            }
            if (buffer[4] != 0)
            {
                error = "Телеметрия ETS2 приостановлена игрой.";
                return false;
            }
            if (!ScsTelemetryParser.TryParse(buffer, out timestamp, out telemetry))
            {
                error = "Данные SCS недоступны или версия плагина не поддерживается (нужна revision 12).";
                return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            error = "Плагин SCS Telemetry не найден. Установите Win64 DLL и перезапустите ETS2.";
            Reset();
            return false;
        }
    }

    void EnsureOpen()
    {
        if (view is not null) return;
        map = MemoryMappedFile.OpenExisting("Local\\SCSTelemetry", MemoryMappedFileRights.Read);
        view = map.CreateViewAccessor(0, buffer.Length, MemoryMappedFileAccess.Read);
    }

    public void Reset()
    {
        view?.Dispose(); map?.Dispose();
        view = null; map = null;
    }

    public void Dispose() => Reset();
}
