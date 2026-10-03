using System.Collections.Generic;

namespace Qlz;

// Tracks successful machine deliveries, independently of inventory and active Buffs.
internal sealed class LifeBottleQuota
{
    private readonly Dictionary<int, int> receivedLife = new();

    internal bool CanReceive(int playerId, int lifeId) =>
        !receivedLife.TryGetValue(playerId, out int previousLife) || previousLife != lifeId;

    internal void RecordReceived(int playerId, int lifeId) => receivedLife[playerId] = lifeId;
    internal void Forget(int playerId) => receivedLife.Remove(playerId);
    internal void Reset() => receivedLife.Clear();
}
