using System;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace TaikoMods;

public class RankedMatchSongSelectPatch
{
    public static ManualLogSource Log => Plugin.Log;

    [HarmonyPatch(typeof(RankedMatchSceneManager), "GetDifficulty")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPrefix]
    public static bool GetDifficulty_Prefix(RankedMatchSceneManager __instance, MusicDataInterface.MusicInfoAccesser info, DataConst.RankType rank1, DataConst.RankType rank2, ref EnsoData.EnsoLevelType level1, ref EnsoData.EnsoLevelType level2)
    {
        var rankedMatchStatus = Traverse.Create(__instance).Field("status").GetValue() as RankedMatchStatus;
        if (rankedMatchStatus == null)
        {
            throw new Exception("RankedMatchStatus was null");
        }

        // Always return the rank-selected difficulties for the ranked match
        if (!EnsoData.IsFriendMatch(rankedMatchStatus.CurrentMatchingType))
            return true;

        Log.LogInfo("NOW choosing difficulty for friend match");

        var hasConfigDefault = Plugin.Instance.ConfigFriendMatchingDefaultDifficulty.Value != 0;
        var configDefault = (EnsoData.EnsoLevelType)Plugin.Instance.ConfigFriendMatchingDefaultDifficulty.Value - 1;

        var isUraExist = info.Stars[4] > 0;
        EnsoData.EnsoLevelType friendLevelType;
        if (Input.GetKey(KeyCode.C))
        {
            friendLevelType = EnsoData.EnsoLevelType.Easy;
        }
        else if (Input.GetKey(KeyCode.V))
        {
            friendLevelType = EnsoData.EnsoLevelType.Normal;
        }
        else if (Input.GetKey(KeyCode.B))
        {
            friendLevelType = EnsoData.EnsoLevelType.Hard;
        }
        else if (Input.GetKey(KeyCode.N))
        {
            friendLevelType = EnsoData.EnsoLevelType.Mania;
        }
        else if(Input.GetKey(KeyCode.M))
        {
            friendLevelType = isUraExist ? EnsoData.EnsoLevelType.Ura : EnsoData.EnsoLevelType.Mania;
        }
        else if(hasConfigDefault)
        {
            Log.LogInfo($"No key pressed, using config default: {configDefault}");
            friendLevelType = configDefault;
        }
        else
        {
            Log.LogInfo("No key pressed, choosing random difficulty");
            return true; // Use the random algorithm if no key is pressed
        }

        level1 = level2 = friendLevelType;

        return false;
    }
}