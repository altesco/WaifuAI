using System;

namespace WaifuAI.Models;

public struct MoodVector
{
    public (int MinDelta, int MaxDelta) Affection { get; set; }
    public (int MinDelta, int MaxDelta) Engagement { get; set; }
    public (int MinDelta, int MaxDelta) Mood { get; set; }
    public (int MinDelta, int MaxDelta) Energy { get; set; }
}

public struct Factors
{
    public int DaysKnown { get; set; }
    public int MessageCount { get; set; }
    public TimeSpan TimeSinceLastMessage { get; set; }
    public int RandomDailyNoise { get; set; } 
}

public struct ArchetypeSensitivity
{
    // --- AFFECTION (Симпатия) ---
    public float AbsenceAffectionImpact { get; set; }
    public float DaysAffectionBonus { get; set; }

    // --- ENGAGEMENT (Вовлеченность) ---
    public float AbsenceEngagementImpact { get; set; }
    public float DaysEngagementBonus { get; set; }
    public float EngagementDropChance { get; set; }
    public float EngagementFloor { get; set; }
    public (int Min, int Max) EngagementDropRange { get; set; }

    // --- MOOD (Настроение) ---
    public float AbsenceMoodImpact { get; set; }
    public float DaysMoodBonus { get; set; }
    public float MoodDropChance { get; set; }
    public float MoodFloor { get; set; }
    public (int Min, int Max) MoodDropRange { get; set; }

    // --- ENERGY (Энергия) ---
    public float AbsenceEnergyImpact { get; set; }
    public float DaysEnergyBonus { get; set; }
    public float EnergyDropRate { get; set; } // Списание энергии за 5 минут (float)
    public float EnergyRecoveryRate { get; set; } // Восстановление энергии в час сна (float)
    public float SleepChanceLowEnergy { get; set; } // Базовая вероятность уйти спать при Energy.Low

    // --- SLEEP & AUTONOMY ---
    public DateTime LatestBedtime { get; set; } // Самое позднее время отхода ко сну
    public (int Min, int Max) BaseSleepDurationRange { get; set; } // Диапазон часов сна (Min, Max)
    public float FirstMessageChance { get; set; } // Шанс написать первой при пробуждении

    // --- GENERAL ---
    public int DaysSaturation { get; set; }
    public int MessageSaturation { get; set; }
    public float AbsenceTauHours { get; set; }
    public float ResponseQuestionChance { get; set; }
}

public struct NormalizedFactors
{
    public float DaysFactor { get; set; } // 0.0 ... ~1.5
    public float MessageFactor { get; set; }
    public float AbsenceFactor { get; set; } // 0.0 ... 1.0
    public int DailyNoise { get; set; } // -2 ... +2
}