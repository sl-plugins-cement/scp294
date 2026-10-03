using System;

namespace Qlz;

internal sealed class VirtualMagazine
{
    public int Remaining { get; private set; } = 67;
    public bool RefillPending { get; private set; }

    public void Reload()
    {
        Remaining = 67;
        RefillPending = false;
    }

    public void Shot()
    {
        Remaining = Math.Max(0, Remaining - 1);
        // The revolver raises ShotWeapon before marking the native chamber discharged.
        // Commit the refill on the next frame, after the native shot has finished.
        RefillPending = true;
    }

    public void RefillCommitted() => RefillPending = false;
}
