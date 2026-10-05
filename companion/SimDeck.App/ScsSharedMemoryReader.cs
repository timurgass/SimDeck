using System.IO;
using System.IO.MemoryMappedFiles;
using SimDeck.Core;

namespace SimDeck.App;

internal sealed class ScsSharedMemoryReader : IDisposable
{
    public static bool MapEditingAllowed(uint game)
    {
        try
        {
            using var memory=MemoryMappedFile.OpenExisting("Local\\SCSTelemetry",MemoryMappedFileRights.Read);
            using var snapshot=memory.CreateViewAccessor(0,ScsTelemetryParser.SnapshotSize,MemoryMappedFileAccess.Read);
            var b=new byte[ScsTelemetryParser.SnapshotSize];snapshot.ReadArray(0,b,0,b.Length);
            if(b[4]==0)return false; // The world map pauses single-player driving.
            b[4]=0; // Inspect paused speed/game identity locally; never publish as live data.
            return ScsTelemetryParser.TryParse(b,out _,out var frame,game)&&frame is not null&&frame.SpeedMps<.1;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or InvalidOperationException){return false;}
    }
    MemoryMappedFile? map;
    MemoryMappedViewAccessor? view;
    readonly byte[] buffer = new byte[ScsTelemetryParser.SnapshotSize];

    public bool TryRead(out ulong timestamp, out Telemetry? telemetry, out string error, uint expectedGame = 1)
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
                error = "Телеметрия SCS приостановлена игрой.";
                return false;
            }
            if (!ScsTelemetryParser.TryParse(buffer, out timestamp, out telemetry, expectedGame))
            {
                error = "Данные SCS недоступны или версия плагина не поддерживается (нужна revision 12).";
                return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            error = "Плагин SCS Telemetry не найден. Установите Win64 DLL и перезапустите выбранную игру ETS2/ATS.";
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
