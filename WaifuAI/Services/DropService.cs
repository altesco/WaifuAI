using System;
using Avalonia.Threading;
using WaifuAI.Models;
using WaifuAI.ViewModels;

namespace WaifuAI.Services;

public static class DropService
{
    private const int CooldownMinutes = 5;
    private static DispatcherTimer? _timer;

    /// <summary>
    /// Запускает фоновую проверку падения характеристик.
    /// Вызывать 1 раз при старте приложения.
    /// </summary>
    public static void Start()
    {
        if (_timer is { IsEnabled: true })
            return;

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
        // 1. Получаем текущий активный архетип и его конфиги (например, из настроек или менеджера)
        var s = SettingsVM.Instance;
        var archetype = s.SelectedArchetype;

        // 2. Вызываем дроп вовлеченности
        DropEngagement(
            baseFloorValue: archetype.Sensitivity.EngagementFloor,
            baseDropChance: archetype.Sensitivity.EngagementDropChance,
            dropRange: archetype.Sensitivity.EngagementDropRange,
            energy: s.EnergyLevel,
            mood: s.MoodLevel
        );

        // 3. дроп энергии
        // DropEnergy(...);
    }

    public static void DropEngagement(
        int baseFloorValue,
        float baseDropChance,
        (int Min, int Max) dropRange,
        EnergyType energy,
        MoodType mood)
    {
        var s = SettingsVM.Instance;
        var currentValue = s.Engagement;

        // 1. Проверка кулдауна по времени
        if ((DateTime.UtcNow - s.LastEngagementDrop).TotalMinutes < CooldownMinutes)
            return;

        // 2. Расчет вероятности наступления среза
        float energyMod = energy == EnergyType.Low ? 1.5f : 1.0f;
        float moodMod = mood == MoodType.Bad ? 1.3f : 1.0f;
        float finalDropChance = baseDropChance * energyMod * moodMod;

        if (Random.Shared.NextDouble() >= finalDropChance)
            return; // Срез не случился

        s.LastEngagementDrop = DateTime.UtcNow; // Фиксируем время срабатывания

        // 3. Расчет величины среза (с уклоном в Max, если низкая энергия)
        float bias = energy == EnergyType.Low ? 0.75f : 0.35f; // Чем выше bias, тем ближе к Max
        int rawDropAmount = dropRange.Min +
                            (int)Math.Round((dropRange.Max - dropRange.Min) *
                                            (Random.Shared.NextDouble() * (1 - bias) + bias));

        // 4. Ограничитель Базового уровня (не даем уйти ниже baseFloorValue)
        int maxAllowedDrop = Math.Max(0, currentValue - baseFloorValue);
        int actualDrop = Math.Min(rawDropAmount, maxAllowedDrop);

        s.Engagement -= actualDrop;
    }
}

