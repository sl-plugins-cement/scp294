namespace Qlz;

internal sealed class MeteorMotion
{
    private float lastMovedAt;
    public MeteorMotion(float startedAt) => lastMovedAt = startedAt;
    public bool ShouldExplode(float now, float speed, float stillSeconds)
    {
        if (speed >= 0.1f) lastMovedAt = now;
        return now - lastMovedAt > stillSeconds;
    }
}
