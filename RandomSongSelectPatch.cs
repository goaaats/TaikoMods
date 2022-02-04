﻿using HarmonyLib;
using Microsoft.Xbox;

namespace TaikoMods;

/// <summary>
/// This patch will address the issue where signing with GDK is done correctly
/// </summary>
[HarmonyPatch(typeof(SongSelectManager))]
[HarmonyPatch("UpdateRandomSelect")]
public class RandomSongSelectPatch
{
    // ReSharper disable once InconsistentNaming
    private static bool Prefix(SongSelectManager __instance)
    {
        var stateTraverse = Traverse.Create(__instance).Field("currentRandomSelectState"); // SongSelectManager.RandomSelectState
        var state = (int) stateTraverse.GetValue();

        if (state == 2) // DecideSong
        {
            stateTraverse.SetValue(0); // Prepare
            Traverse.Create(__instance).Method("ChangeState", SongSelectManager.State.SongSelect)
                .GetValue(SongSelectManager.State.SongSelect); // Switch back to SongSelect mode
            Traverse.Create(__instance).Field("isSongLoadRequested").SetValue(true); // Needed to have the game load the song preview

            return false; // Don't call original method
        }

        return true;
    }
}