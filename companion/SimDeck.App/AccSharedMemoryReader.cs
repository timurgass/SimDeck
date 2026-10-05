using System.IO.MemoryMappedFiles;
using System.IO;
using SimDeck.Core;

namespace SimDeck.App;

internal sealed class AccSharedMemoryReader : IDisposable
{
    MemoryMappedFile? physicsMap;
    MemoryMappedFile? staticMap;
    MemoryMappedViewAccessor? physicsView;
    MemoryMappedViewAccessor? staticView;
    readonly byte[] physics = new byte[AccTelemetryParser.PhysicsSize];
    readonly byte[] staticInfo = new byte[AccTelemetryParser.StaticSize];
    public int? PlayerId { get; private set; }
    MemoryMappedFile? graphicsMap;
    MemoryMappedViewAccessor? graphicsView;

    void ReadPlayer()
    {
        PlayerId = null;
        try {
            graphicsMap ??= MemoryMappedFile.OpenExisting("Local\\acpmf_graphics", MemoryMappedFileRights.Read);
            graphicsView ??= graphicsMap.CreateViewAccessor(0, 1220, MemoryMappedFileAccess.Read);
            var before = graphicsView.ReadInt32(0);
            var status = graphicsView.ReadInt32(4);
            var count = graphicsView.ReadInt32(252);
            var id = graphicsView.ReadInt32(1216);
            if (before == graphicsView.ReadInt32(0) && status == 2 && count is > 0 and <= 60 && id >= 0) PlayerId = id;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) {
            graphicsView?.Dispose(); graphicsMap?.Dispose(); graphicsView = null; graphicsMap = null;
        }
    }

    public bool TryRead(out int packetId, out Telemetry? telemetry, out string error)
    {
        packetId = -1;
        telemetry = null;
        error = "";
        try
        {
            EnsureOpen();
            ReadPlayer();
            physicsView!.ReadArray(0, physics, 0, physics.Length);
            staticView!.ReadArray(0, staticInfo, 0, staticInfo.Length);
            packetId = BitConverter.ToInt32(physics, 0);
            if (!AccTelemetryParser.TryParse(physics, staticInfo, out telemetry))
            {
                error = "ACC Shared Memory содержит некорректные значения.";
                return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            error = "ACC Shared Memory недоступна. Запустите заезд и выйдите на трассу.";
            Reset();
            return false;
        }
    }

    void EnsureOpen()
    {
        if (physicsView is not null && staticView is not null) return;
        physicsMap = MemoryMappedFile.OpenExisting("Local\\acpmf_physics", MemoryMappedFileRights.Read);
        staticMap = MemoryMappedFile.OpenExisting("Local\\acpmf_static", MemoryMappedFileRights.Read);
        physicsView = physicsMap.CreateViewAccessor(0, AccTelemetryParser.PhysicsSize, MemoryMappedFileAccess.Read);
        staticView = staticMap.CreateViewAccessor(0, AccTelemetryParser.StaticSize, MemoryMappedFileAccess.Read);
    }

    public void Reset()
    {
        physicsView?.Dispose(); staticView?.Dispose();
        graphicsView?.Dispose(); graphicsMap?.Dispose(); graphicsView = null; graphicsMap = null; PlayerId = null;
        physicsMap?.Dispose(); staticMap?.Dispose();
        physicsView = null; staticView = null; physicsMap = null; staticMap = null;
    }

    public void Dispose() => Reset();
}
