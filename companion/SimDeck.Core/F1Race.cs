using System.Buffers.Binary;
using System.Text;

namespace SimDeck.Core;

public sealed record F1Driver(int Index, string Name, int Team, int Number, int Position, int Lap,
    double Distance, int Pit, int Result, int DriverStatus, int GapAheadMs, int GapLeaderMs, bool Player);
public sealed record F1Race(int TrackId, int TrackLength, int SessionType, bool Fresh, F1Driver[] Drivers);

internal sealed class F1RaceCache
{
    (string Name, int Team, int Number)[] participants = [];
    F1Driver[] laps = [];
    long lapAt = long.MinValue, participantsAt = long.MinValue;
    int trackId = -1, trackLength, sessionType;
    public void Session(ReadOnlySpan<byte> c)
    {
        var next = (int)unchecked((sbyte)c[7]);
        if (next != trackId) { laps = []; lapAt = long.MinValue; }
        trackId = next; trackLength = BinaryPrimitives.ReadUInt16LittleEndian(c[4..]); sessionType = c[6];
    }
    public bool Participants(ReadOnlySpan<byte> b, long now, int format = 2024)
    {
        var count = b[29]; if (count > 22) return false;
        var stride = format == 2025 ? 57 : 60;
        var nameLength = format == 2025 ? 32 : 48;
        var next = new (string, int, int)[count];
        for (var i = 0; i < count; i++)
        {
            var c = b.Slice(30 + i * stride, stride); var nameBytes = c.Slice(7, nameLength);
            var end = nameBytes.IndexOf((byte)0); if (end >= 0) nameBytes = nameBytes[..end];
            var name = new string(Encoding.UTF8.GetString(nameBytes).Where(x => !char.IsControl(x)).ToArray()).Trim();
            next[i] = (name.Length == 0 ? $"Car {i + 1}" : name, c[3], c[5]);
        }
        participants = next; participantsAt = now; return true;
    }
    public bool Laps(ReadOnlySpan<byte> b, long now, int player)
    {
        var next = new List<F1Driver>();
        for (var i = 0; i < 22; i++)
        {
            var c = b.Slice(29 + i * 57, 57);
            if (c[45] is 0 or 1) continue; // Invalid / inactive slots, including unused cars.
            var distance = BinaryPrimitives.ReadSingleLittleEndian(c[20..]);
            if (!float.IsFinite(distance) || Math.Abs(distance) > 100000 || c[32] > 22 || c[34] > 2 || c[45] > 7 || c[44] > 4) return false;
            next.Add(new(i, "", 0, 0, c[32], c[33], Math.Round(distance, 1), c[34], c[45], c[44],
                BinaryPrimitives.ReadUInt16LittleEndian(c[14..]) + c[16] * 60000,
                BinaryPrimitives.ReadUInt16LittleEndian(c[17..]) + c[19] * 60000, i == player));
        }
        laps = next.ToArray(); lapAt = now; return true;
    }
    public F1Race Snapshot(long now)
    {
        var fresh = lapAt != long.MinValue && now - lapAt is >= 0 and < 1000;
        var known = participantsAt != long.MinValue && now - participantsAt is >= 0 and < 30000;
        var drivers = known ? laps.Where(d => d.Index < participants.Length).Select(d => d with {
            Name = participants[d.Index].Name, Team = participants[d.Index].Team, Number = participants[d.Index].Number
        }).OrderBy(d => d.Position == 0 ? 99 : d.Position).ThenBy(d => d.Index).ToArray() : [];
        return new(trackId, trackLength, sessionType, fresh, drivers);
    }
}
