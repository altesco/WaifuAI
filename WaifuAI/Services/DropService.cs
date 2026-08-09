using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using WaifuAI.Models;
using WaifuAI.ViewModels;

namespace WaifuAI.Services;

public static class DropService
{
    private const int CooldownMinutes = 5;
    private static DispatcherTimer? _timer;

    public static event Func<Task>? OnWakeUpFirstMessageRequested;

    public static void Start()
    {
        if (_timer is { IsEnabled: true })
            return;

        CatchUpOfflineTime();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(CooldownMinutes)
        };

        ProcessAllDrops();
        _timer.Tick += (_, _) => ProcessAllDrops();
        _timer.Start();
    }

    public static void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    private static void ProcessAllDrops()
    {
        var settings = SettingsVM.Instance;
        var s = settings.SelectedArchetype.Sensitivity;

        settings.LastUserEntry = DateTime.UtcNow;

        if (settings.IsSleeping)
        {
            RestoreEnergyDuringSleep(s.EnergyRecoveryRate);
            CheckWakeUp(s.FirstMessageChance);
            return;
        }

        DropEngagement(
            baseFloorValue: s.EngagementFloor,
            baseDropChance: s.EngagementDropChance,
            dropRange: s.EngagementDropRange,
            energy: settings.EnergyLevel,
            mood: settings.MoodLevel
        );

        DropMood(
            baseFloorValue: s.MoodFloor,
            baseDropChance: s.MoodDropChance,
            dropRange: s.MoodDropRange,
            energy: settings.EnergyLevel
        );

        DropEnergy(s.EnergyDropRate);
    }

    public static void DropEngagement(
        float baseFloorValue,
        float baseDropChance,
        (int Min, int Max) dropRange,
        EnergyType energy,
        MoodType mood)
    {
        var s = SettingsVM.Instance;
        var currentValue = s.Engagement;

        // Погрешность в 2 секунды, чтобы DispatcherTimer не пропускал тики
        if ((DateTime.UtcNow - s.LastEngagementDrop).TotalSeconds < (CooldownMinutes * 60 - 2))
            return;

        float energyMod = energy == EnergyType.Low ? 1.5f : 1.0f;
        float moodMod = mood == MoodType.Bad ? 1.3f : 1.0f;
        float finalDropChance = baseDropChance * energyMod * moodMod;

        if (Random.Shared.NextDouble() >= finalDropChance)
            return;

        s.LastEngagementDrop = DateTime.UtcNow;

        float bias = energy == EnergyType.Low ? 0.75f : 0.35f;
        int rawDropAmount = dropRange.Min +
                            (int)Math.Round((dropRange.Max - dropRange.Min) *
                                            (Random.Shared.NextDouble() * (1 - bias) + bias));

        float maxAllowedDrop = Math.Max(0.0f, currentValue - baseFloorValue);
        float actualDrop = Math.Min(rawDropAmount, maxAllowedDrop);

        s.Engagement -= actualDrop;
    }

    public static void DropMood(
        float baseFloorValue,
        float baseDropChance,
        (int Min, int Max) dropRange,
        EnergyType energy)
    {
        var s = SettingsVM.Instance;
        var currentValue = s.Mood;

        if ((DateTime.UtcNow - s.LastMoodDrop).TotalSeconds < (CooldownMinutes * 60 - 2))
            return;

        float energyMod = energy == EnergyType.Low ? 1.4f : 1.0f;
        float finalDropChance = baseDropChance * energyMod;

        if (Random.Shared.NextDouble() >= finalDropChance)
            return;

        s.LastMoodDrop = DateTime.UtcNow;

        int rawDropAmount = Random.Shared.Next(dropRange.Min, dropRange.Max + 1);
        float maxAllowedDrop = Math.Max(0.0f, currentValue - baseFloorValue);
        float actualDrop = Math.Min(rawDropAmount, maxAllowedDrop);

        s.Mood -= actualDrop;
    }

    public static void DropEnergy(float dropRate)
    {
        var s = SettingsVM.Instance;
        if ((DateTime.UtcNow - s.LastEnergyDrop).TotalSeconds < (CooldownMinutes * 60 - 2))
            return;

        s.LastEnergyDrop = DateTime.UtcNow;
        s.Energy = Math.Max(0.0f, s.Energy - dropRate);
    }

    private static void RestoreEnergyDuringSleep(float hourlyRecoveryRate)
    {
        var s = SettingsVM.Instance;
        float recoveryPerTick = hourlyRecoveryRate / 12.0f;
        s.Energy = Math.Min(100.0f, s.Energy + recoveryPerTick);
    }

    private static void CatchUpOfflineTime()
    {
        var s = SettingsVM.Instance;
        var lastEntry = s.LastUserEntry;
        var now = DateTime.UtcNow;

        if (lastEntry == default || lastEntry >= now)
        {
            s.LastUserEntry = now;
            return;
        }

        var sensitivity = s.SelectedArchetype.Sensitivity;
        var currentTime = lastEntry;
        float hourlyEnergyDrop = sensitivity.EnergyDropRate * 12.0f;
        bool wokeUpOffline = false;

        while (currentTime < now)
        {
            if (s.IsSleeping)
            {
                if (s.WakeUpTime <= now)
                {
                    double sleptHours = (s.WakeUpTime - currentTime).TotalHours;
                    s.Energy = Math.Min(100.0f, s.Energy + (float)(sleptHours * sensitivity.EnergyRecoveryRate));

                    //s.IsSleeping = false;
                    SetRandomMorningMood();
                    wokeUpOffline = true;
                    currentTime = s.WakeUpTime;
                }
                else
                {
                    double sleptHours = (now - currentTime).TotalHours;
                    s.Energy = Math.Min(100.0f, s.Energy + (float)(sleptHours * sensitivity.EnergyRecoveryRate));
                    currentTime = now;
                }
            }
            else
            {
                // Защита от деления на 0 и зацикливания при нулевой энергии
                double hoursUntilZero = hourlyEnergyDrop > 0 ? s.Energy / hourlyEnergyDrop : 24.0;
                DateTime timeOfZeroEnergy = currentTime.AddHours(hoursUntilZero);

                DateTime todaysBedtime = new DateTime(
                    currentTime.Year, currentTime.Month, currentTime.Day,
                    sensitivity.LatestBedtime.Hour, sensitivity.LatestBedtime.Minute, 0, DateTimeKind.Utc
                );
                if (todaysBedtime <= currentTime)
                    todaysBedtime = todaysBedtime.AddDays(1);

                DateTime nextSleepTime = timeOfZeroEnergy < todaysBedtime ? timeOfZeroEnergy : todaysBedtime;

                // Если засыпание должно произойти прямо сейчас (например, энергия уже была 0)
                if (nextSleepTime <= currentTime)
                {
                    //s.IsSleeping = true;
                    int sleepHours = Random.Shared.Next(
                        sensitivity.BaseSleepDurationRange.Min,
                        sensitivity.BaseSleepDurationRange.Max + 1
                    );
                    s.WakeUpTime = currentTime.AddHours(sleepHours);
                    continue;
                }

                if (nextSleepTime >= now)
                {
                    double awakeHours = (now - currentTime).TotalHours;
                    s.Energy = Math.Max(0.0f, s.Energy - (float)(awakeHours * hourlyEnergyDrop));
                    currentTime = now;
                }
                else
                {
                    double awakeHours = (nextSleepTime - currentTime).TotalHours;
                    s.Energy = Math.Max(0.0f, s.Energy - (float)(awakeHours * hourlyEnergyDrop));

                    //s.IsSleeping = true;
                    int sleepHours = Random.Shared.Next(
                        sensitivity.BaseSleepDurationRange.Min,
                        sensitivity.BaseSleepDurationRange.Max + 1
                    );
                    s.WakeUpTime = nextSleepTime.AddHours(sleepHours);
                    currentTime = nextSleepTime;
                }
            }
        }

        s.LastUserEntry = now;

        if (wokeUpOffline && Random.Shared.NextDouble() < sensitivity.FirstMessageChance)
            _ = OnWakeUpFirstMessageRequested?.Invoke();
    }

    private static void CheckWakeUp(float firstMessageChance)
    {
        var s = SettingsVM.Instance;

        if (DateTime.UtcNow < s.WakeUpTime)
            return;

        //s.IsSleeping = false;
        SetRandomMorningMood();

        if (Random.Shared.NextDouble() < firstMessageChance)
            _ = OnWakeUpFirstMessageRequested?.Invoke();
    }

    private static float CalculateMorningMood(ArchetypeVM archetype, int randomDailyNoise)
    {
        var s = archetype.Sensitivity;

        // 1. Из структуры MoodVector берем кортеж Mood (MinDelta, MaxDelta) и высчитываем среднюю дельту
        float moodVectorMidpoint =
            (archetype.BaseMoodVector.Mood.MinDelta + archetype.BaseMoodVector.Mood.MaxDelta) / 2.0f;

        // Базовый центр настроения (50.0f) со смещением характера
        float baseTarget = 50.0f + moodVectorMidpoint;

        // Влияние ТОЛЬКО суточного шума (от -2 до 2)
        float noiseImpact = randomDailyNoise * 2.0f;

        // Мат. ожидание без влияния дней и простоя
        float targetMean = Math.Max(s.MoodFloor, baseTarget + noiseImpact);

        // 2. Преобразование Бокса-Мюллера (Гауссово распределение вокруг targetMean)
        double u1 = 1.0 - Random.Shared.NextDouble();
        double u2 = 1.0 - Random.Shared.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

        float stdDev = 20.0f;
        float generatedMood = targetMean + (float)(randStdNormal * stdDev);

        // 3. Зажимаем в диапазон 1..100
        return Math.Clamp(generatedMood, 1.0f, 100.0f);
    }

    private static void SetRandomMorningMood()
    {
        var settings = SettingsVM.Instance;
        var archetype = settings.SelectedArchetype;

        // Генерируем суточный шум от -2 до 2
        settings.RandomDailyNoise = Random.Shared.Next(-2, 3);

        // Рассчитываем и выставляем настроение
        settings.Mood = CalculateMorningMood(archetype, settings.RandomDailyNoise);
    }
}