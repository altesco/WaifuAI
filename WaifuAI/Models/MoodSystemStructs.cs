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
    public float AbsenceAffectionImpact { get; set; } // Влияние отсутствия на минимум (e.g. -1.0f)
    public float DaysAffectionBonus { get; set; } // Рост максимального барьера от дней (e.g. +3.0f)

    // --- ENGAGEMENT (Вовлеченность/Интерес) ---
    public float AbsenceEngagementImpact { get; set; } // Падение интереса при отсутствии (e.g. -6.0f)
    public float DaysEngagementBonus { get; set; } // Рост интереса со временем (e.g. +2.0f)

    // --- MOOD (Настроение) ---
    public float AbsenceMoodImpact { get; set; } // Обида/раздражение от молчания (e.g. -5.0f)
    public float DaysMoodBonus { get; set; } // Стабильность настроения от дней (e.g. +1.5f)

    // --- ENERGY (Энергия) ---
    public float AbsenceEnergyImpact { get; set; } // Упадок энергии от ожидания (e.g. -2.0f)
    public float DaysEnergyBonus { get; set; } // e.g. 0.0f

    public int DaysSaturation { get; set; }
    public int MessageSaturation { get; set; }
    public float AbsenceTauHours { get; set; }
}

public struct NormalizedFactors
{
    public float DaysFactor { get; set; } // 0.0 ... ~1.5
    public float MessageFactor { get; set; }
    public float AbsenceFactor { get; set; } // 0.0 ... 1.0
    public int DailyNoise { get; set; } // -2 ... +2
}