namespace WaifuAI.Models;

#region MoodSystem

public enum AffectionType
{
    Bad,
    Normal,
    Good,
    Love
}

public enum EnergyType
{
    Low,
    Middle,
    High
}

public enum EngagementType
{
    Indifferent,
    Balanced,
    Interested
}

public enum MoodType
{
    Bad,
    Normal,
    Best
}

#endregion


#region AnswerLength

public enum ResponseLength
{
    Short,
    MediumShort,
    Medium,
    MediumLong,
    Long
}

#endregion


#region Model3D

public enum CameraVariant
{
    Portrait,
    Medium,
    Full
}

#endregion

public enum ThemeVariant
{
    Dark,
    Light,
    System
}