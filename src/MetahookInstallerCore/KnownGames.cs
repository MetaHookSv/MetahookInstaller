using System;
using System.Collections.Generic;
using System.Linq;

namespace MetahookInstaller;

public sealed record GameInfo(string Name, string ModDirectory, uint AppId);

public static class KnownGames
{
    public static IReadOnlyList<GameInfo> All { get; } =
    [
        new("Sven Co-op", "svencoop", 225840),
        new("Half-Life", "valve", 70),
        new("Half-Life Updated", "halflife_updated", 70),
        new("Half-Life Opposing Force", "gearbox", 50),
        new("Half-Life Blue Shift", "bshift", 130),
        new("Half-Life Echoes", "echoes", 70),
        new("Half-Life Field Intensity", "field_intensity", 70),
        new("Half-Life MMod", "HL1MMod", 1761270),
        new("Counter-Strike", "cstrike", 10),
        new("Counter-Strike Condition Zero", "czero", 80),
        new("Counter-Strike Condition Zero - Deleted Scenes", "czeror", 100),
        new("Day of Defeat", "dod", 30),
        new("Afraid of Monsters: Director's Cut", "aomdc", 70),
    ];

    // Without a mod directory, the first game registered for the app (its base game) is returned.
    public static GameInfo? Find(uint appId, string? modDirectory = null)
    {
        return All.FirstOrDefault(game => game.AppId == appId &&
            (modDirectory == null || game.ModDirectory.Equals(modDirectory, StringComparison.OrdinalIgnoreCase)));
    }
}
