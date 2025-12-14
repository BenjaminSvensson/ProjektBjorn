/// <summary>
/// Defines the type of a limb, mainly for attachment logic.
/// </summary>
public enum LimbType
{
    Head,
    Arm,
    Leg,
    Universal // Can be attached to either Arm or Leg slots
}

/// <summary>
/// Defines the specific slot a limb can be attached to.
/// </summary>
public enum LimbSlot
{
    Head,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg
}