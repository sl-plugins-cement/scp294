namespace Qlz;

internal sealed class DrinkTimeline
{
    public float EndAt { get; private set; }
    public bool Backlash { get; private set; }

    public DrinkTimeline(float endAt) => EndAt = endAt;

    public bool TryStartBacklash(float now, float duration)
    {
        if (Backlash || now < EndAt) return false;
        Backlash = true;
        EndAt += duration;
        return true;
    }

    public bool Expired(float now) => now >= EndAt;
}
