using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using OnlineManager;
using RankedMatch;
using UnityEngine;

namespace TaikoMods.RankedMatch;

public class RankedMatchSongSelectPatch
{
    public static ManualLogSource Log => Plugin.Log;

    /// <summary>
    /// We are basically reimplementing MatchingProcess here, seems like the cleanest way to go about it.
    /// </summary>
    [HarmonyPatch(typeof(RankedMatchSceneManager), "MatchingProcess")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPrefix]
    public static bool MatchingProcess_Prefix(RankedMatchSceneManager __instance, ref IEnumerator __result)
    {
	    var status = Traverse.Create(__instance).Field("status").GetValue<RankedMatchStatus>();
	    var networkManager = Traverse.Create(__instance).Field("networkManager").GetValue<RankedMatchNetworkManager>();
	    var setting = Traverse.Create(__instance).Field("setting").GetValue<RankedMatchSetting>();
	    var inputManager = Traverse.Create(__instance).Field("inputManager").GetValue<RankedMatchInputManager>();
	    var songPlayer = Traverse.Create(__instance).Field("songPlayer").GetValue<RankedMatchSongPlayer>();
	    var voicePlayer = Traverse.Create(__instance).Field("voicePlayer").GetValue<RankedMatchSoundPlayer>();

	    IEnumerator NewMatchingProcess()
	    {
		    /* ================================= */
		    /* ====== Set-up Networking ======== */
		    /* ================================= */

		    var playDataManager = TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.PlayData;
		    var isDone = false;
		    var isSucceeded = false;
		    var errorMessage = "";
		    __instance.userInterfaceObjectReadyMessage = "";
		    status.CurrentMatchingState = MatchingState.Initializing;
		    status.MatchingSongUniqueId = -1;
		    if (!networkManager.IsAcceptedRematch())
		    {
			    isDone = false;
			    networkManager.StartCleaningUpPlayFab(delegate { isDone = true; });
			    yield return new WaitUntil(() => isDone);
			    isDone = false;
			    networkManager.StartInitializingOnlineManager(delegate(bool result)
			    {
				    isDone = true;
				    isSucceeded = result;
			    });
			    yield return new WaitUntil(() => isDone);
			    if (!isSucceeded)
			    {
				    status.CurrentMatchingErrorType = ErrorType.NetworkLight;
				    if (networkManager.IsReceivedInvitation()) networkManager.ResetReceivedInvitationFlag();
				    if (networkManager.IsAcceptedRematch()) networkManager.ResetAcceptedRematchFlag();
				    yield break;
			    }
		    }

		    isDone = false;
		    networkManager.StartRefleshNetworkTime(delegate(bool result)
		    {
			    isDone = true;
			    isSucceeded = result;
		    });
		    yield return new WaitUntil(() => isDone);
		    if (!isSucceeded)
		    {
			    status.CurrentMatchingErrorType = ErrorType.NetworkLight;
			    if (networkManager.IsReceivedInvitation()) networkManager.ResetReceivedInvitationFlag();
			    if (networkManager.IsAcceptedRematch()) networkManager.ResetAcceptedRematchFlag();
			    yield break;
		    }

		    /* ================================= */
		    /* ======== Find Player ============ */
		    /* ================================= */

		    DateTime currentTime = status.GetCurrentTime();
		    if (status.GetSeasonInfo(currentTime, out var info)) playDataManager.Rankmatch_SeasonId = info.SeasonId;
		    playDataManager.RankMatch_IsCoinUp = status.TimeEventRemainingSpan.TotalSeconds > 0.0;
		    networkManager.InitializeSeasonRankPoint(currentTime, shouldSync: true);
		    networkManager.GetSeasonPlayerInfo(0, out var playerData, out var _, out var _);
		    status.MatchingPlayer1Data = playerData;
		    networkManager.GetPlayMusicInfo(out var musicData);
		    status.MatchingPlayer1Music = musicData;
		    TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.PlayData.GetEnsoMode(out var dst);
		    status.MatchingPlayer1Setting =
			    new EnsoSetting(dst, EnsoData.EnsoLevelType.Easy, status.CurrentMatchingType);
		    status.CurrentMatchingState = MatchingState.SearchingPlayer;
		    var startTime = Time.time;
		    isDone = false;
		    errorMessage = "";

		    if (EnsoData.IsFriendMatch(status.CurrentMatchingType))
		    {
			    if (networkManager.IsAcceptedRematch())
			    {
				    Log.LogInfo("[MatchingProcess] Is Accepted Rematch");

				    isDone = true;
				    isSucceeded = true;
				    networkManager.ResetAcceptedRematchFlag();
			    }
			    else if (networkManager.IsReceivedInvitation())
			    {
				    Log.LogInfo("[MatchingProcess] Start joining friend");

				    isDone = false;
				    networkManager.StartJoiningFriend(delegate(bool result)
				    {
					    isDone = true;
					    isSucceeded = result;
					    if (!isSucceeded) errorMessage = "not joined";
				    });
				    networkManager.ResetReceivedInvitationFlag();
			    }
			    else
			    {
				    Log.LogInfo("[MatchingProcess] Start searching friend");

				    isDone = false;
				    networkManager.StartInvitingFriend(delegate(bool result, string message)
				    {
					    isDone = true;
					    isSucceeded = result;
					    errorMessage = message;
				    });
			    }
		    }
		    else
		    {
			    yield return new WaitForSeconds(setting.matchingWaitingSec);
			    networkManager.StartSearchingPlayer(status.MatchingPlayer1Data.rankType, delegate(bool result)
			    {
				    isDone = true;
				    isSucceeded = result;
			    });
		    }

		    yield return new WaitUntil(() => isDone);
		    if (!isSucceeded)
		    {
			    if (errorMessage == "cancel")
				    inputManager.MoveToTopMenuFromMatching();
			    else if (errorMessage == "not joined")
				    status.CurrentMatchingErrorType = ErrorType.SessionHeavy;
			    else
				    status.CurrentMatchingErrorType = ErrorType.NetworkLight;
			    yield break;
		    }

		    /* ================================= */
		    /* ====== Transceive Account ======= */
		    /* ================================= */

		    Log.LogInfo("[MatchingProcess] Now transceive player info");

		    status.CurrentMatchingState = MatchingState.TransceivePlayerInfo;
		    isDone = false;
		    networkManager.StartTransceivePlayerInfo(1, delegate(bool result)
		    {
			    isDone = true;
			    isSucceeded = result;
		    });
		    yield return new WaitUntil(() => isDone);
		    if (!isSucceeded)
		    {
			    __instance.StartRematch();
			    yield break;
		    }

		    /* ================================= */
		    /* ==== Transceive Song & Diff ===== */
		    /* ================================= */

		    if (status.CurrentMatchingType == EnsoData.RankMatchType.RankMatch &&
		        status.MatchingPlayer2Setting.matchingType == EnsoData.RankMatchType.FriendInvited)
		    {
			    status.CurrentMatchingType = EnsoData.RankMatchType.FriendInviting;
			    status.CurrentTopMenuButtonState = TopMenuButtonState.MatchingFriend;
		    }

		    /* =========== MINE =========== */
		    DecideDifficultyInfo? otherPlayerDiffDecide = null;
		    if (networkManager.IsMatchingHost() && EnsoData.IsFriendMatch(status.CurrentMatchingType))
		    {
			    var otherPlayerDifficulty = default(DecideDifficultyInfo);
			    Log.LogInfo("[MatchingProcess] Now waiting for other player difficulty");
			    yield return new WaitUntil(() => TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.GetLastRecieveData((ReceiveDataType)CustomReceiveDataType.DecideNonHostDifficulty, ref otherPlayerDifficulty));

			    if (otherPlayerDifficulty.HasDecision)
					otherPlayerDiffDecide = otherPlayerDifficulty;

			    Log.LogInfo($"[MatchingProcess] Other played decided, HasDecision: {otherPlayerDifficulty.HasDecision}, Difficulty: {otherPlayerDifficulty.LevelType}");
		    }
		    else if (!networkManager.IsMatchingHost() && EnsoData.IsFriendMatch(status.CurrentMatchingType))
		    {
			    var diff = DecideDifficulty(true);
			    Log.LogInfo("[MatchingProcess] Now sending other player difficulty");
			    var decideDifficultyInfo = new DecideDifficultyInfo
			    {
				    HasDecision = diff.HasValue,
				    LevelType = diff ?? EnsoData.EnsoLevelType.Easy,
			    };
			    TaikoSingletonMonoBehaviour<XboxLiveOnlineManager>.Instance.SendData((ReceiveDataType)CustomReceiveDataType.DecideNonHostDifficulty, decideDifficultyInfo);
		    }
		    /* ============================ */

		    status.CurrentMatchingState = MatchingState.TransceiveSongInfo;
		    if (networkManager.IsMatchingHost())
		    {
			    status.MatchingSongUniqueId = __instance.GetMatchingSongUniqueId(status.MatchingPlayer1Music.playHistory,
				    status.MatchingPlayer1Music.purchasedMusicList, status.MatchingPlayer2Music.playHistory,
				    status.MatchingPlayer2Music.purchasedMusicList);
			    status.GetMusicInfo(status.MatchingSongUniqueId, out var info2);
			    __instance.GetDifficulty(info2, status.MatchingPlayer1Data.rankType, status.MatchingPlayer2Data.rankType,
				    out var level, out var level2);
			    EnsoSetting matchingPlayer1Setting = status.MatchingPlayer1Setting;
			    matchingPlayer1Setting.difficulty = level;
			    status.MatchingPlayer1Setting = matchingPlayer1Setting;
			    EnsoSetting matchingPlayer2Setting = status.MatchingPlayer2Setting;
			    matchingPlayer2Setting.difficulty = level2;

			    if (otherPlayerDiffDecide.HasValue)
				    matchingPlayer2Setting.difficulty = otherPlayerDiffDecide.Value.LevelType;

			    status.MatchingPlayer2Setting = matchingPlayer2Setting;
		    }

		    isDone = false;
		    networkManager.StartTransceiveSongInfo(2, delegate(bool result)
		    {
			    isDone = true;
			    isSucceeded = result;
		    });
		    yield return new WaitUntil(() => isDone);
		    if (!isSucceeded)
		    {
			    __instance.StartRematch();
			    yield break;
		    }

		    /* ================================= */
		    /* ====== Match & Game Set-up ====== */
		    /* ================================= */

		    if (status.CurrentMatchingType == EnsoData.RankMatchType.RankMatch)
		    {
			    var additionalRankPoint =
				    TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.RankmatchData
					    .GetAdditionalRankPoint(DataConst.MatchResultType.Lose, status.MatchingPlayer1Data.rankPoint,
						    status.MatchingPlayer2Data.rankPoint);
			    __instance.SavePenaltyParam(playDataManager.Rankmatch_SeasonId, flag: true, additionalRankPoint,
				    shouldSync: false);
		    }

		    __instance.SaveStatistics((int)((Time.time - startTime) * 100f));
		    __instance.SavePlayHistory(shouldSync: true);

		    /* ================================= */
		    /* ==== Player found animation ===== */
		    /* ================================= */

		    status.CurrentMatchingState = MatchingState.DisplayingDon;
		    yield return new WaitUntil(() => __instance.userInterfaceObjectReadyMessage == "don2");
		    yield return new WaitForSeconds(setting.matchingDisplaySec);

		    status.CurrentMatchingState = MatchingState.PlayingSong;
		    songPlayer.SetupSong(status.MatchingSongUniqueId);
		    yield return new WaitUntil(() => songPlayer.IsSetup());

		    __instance.StopBgm();
		    status.IsPlayedBgm = false;
		    songPlayer.PlaySong(
			    TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MySoundManager.GetVolume(SoundManager.SoundType
				    .OutGameSong));

		    yield return new WaitForSeconds(setting.matchingPlayingSec);
		    status.CurrentMatchingState = MatchingState.Greeting;
		    voicePlayer.PlaySound("v_rank_play_start",
			    TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MySoundManager.GetVolume(SoundManager.SoundType
				    .Voice));

		    yield return new WaitForSeconds(setting.matchingGreetingSec);

		    isDone = false;
		    networkManager.StartRefleshNetworkTime(delegate(bool result)
		    {
			    isDone = true;
			    isSucceeded = result;
		    });
		    yield return new WaitUntil(() => isDone);
		    if (!isSucceeded)
		    {
			    status.CurrentMatchingErrorType = ErrorType.NetworkHeavy;
			    yield break;
		    }

		    /* ================================= */
		    /* ========== Start game =========== */
		    /* ================================= */

		    __instance.SaveEnsoSetting();
		    __instance.SavePlayer1Info();
		    __instance.SavePlayer2Info();
		    networkManager.SyncSaveData();
		    __instance.MoveSceneRelayed("EnsoRankedMatch");
		    status.CurrentMatchingState = MatchingState.None;
	    }

        __result = NewMatchingProcess();

        return false;
    }

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

        var isUraExist = info.Stars[4] > 0;

        Log.LogInfo("[RankedMatchSongSelectPatch] Now choosing difficulty for friend match");
        var friendLevelType = DecideDifficulty(isUraExist);
        if (friendLevelType == null)
	        return true;

        level1 = level2 = friendLevelType.Value;

        return false;
    }

    [HarmonyPatch(typeof(XboxLiveOnlineManager), "ClearAllRecieveData")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPrefix]
    public static bool ClearAllReceiveData_Prefix(XboxLiveOnlineManager __instance)
    {
	    foreach (Queue<object> receiveDatum in __instance.receiveData)
	    {
		    receiveDatum.Clear();
		    receiveDatum.TrimExcess();
	    }
	    __instance.receiveData.Clear();
	    for (var i = 0; i < 99; i++) // HACK: The game only allocates slots for its own message types. We need to allocate more to allow for our network types to be awaited.
	    {
		    __instance.receiveData.Add(new Queue<object>());
	    }

	    return false;
    }

    [HarmonyPatch(typeof(XboxLiveOnlineManager), "SetObject")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPrefix]
    public static bool SetObject_Prefix(XboxLiveOnlineManager __instance, ref byte[] objData, ReceiveDataType type)
    {
	    //Log.LogInfo($"[SetObject] type: {type}");

	    if (type == (ReceiveDataType)CustomReceiveDataType.DecideNonHostDifficulty)
		    __instance.Enqueue<DecideDifficultyInfo>(ref objData, type);

	    /*
	    switch (type)
	    {
		    case ReceiveDataType.AccountInfo:
			    __instance.Enqueue<AccountInfo>(ref objData, type);
			    break;
		    case ReceiveDataType.SelectMusicInfo:
			    __instance.Enqueue<SelectMusicInfo>(ref objData, type);
			    break;
		    case ReceiveDataType.EnsoInfo:
			    __instance.Enqueue<EnsoData.OnlineEnsoInfoForTransfer>(ref objData, type);
			    break;
		    case ReceiveDataType.EnsoResultInfo:
			    __instance.Enqueue<EnsoData.OnlineEnsoResultInfoForTransfer>(ref objData, type);
			    break;
		    case (ReceiveDataType)CustomReceiveDataType.DecideNonHostDifficulty:
			    __instance.Enqueue<DecideDifficultyInfo>(ref objData, type);
			    break;
		    case ReceiveDataType.Unknown:
			    break;
	    }
	    */

	    return true;
    }

    [HarmonyPatch(typeof(XboxLiveOnlineManager), "ClearMatchingInfo")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPrefix]
    public static bool ClearMatchingInfo_Prefix(XboxLiveOnlineManager __instance)
	{
		Log.LogInfo($"[ClearMatchingInfo] Cleared!");
	    __instance.ClearEachRecieveData((ReceiveDataType) CustomReceiveDataType.DecideNonHostDifficulty);
	    return true;
	}

    private static EnsoData.EnsoLevelType? DecideDifficulty(bool isUraExist)
    {
	    var hasConfigDefault = Plugin.Instance.ConfigFriendMatchingDefaultDifficulty.Value != 0;
	    var configDefault = (EnsoData.EnsoLevelType)Plugin.Instance.ConfigFriendMatchingDefaultDifficulty.Value - 1;

	    EnsoData.EnsoLevelType? friendLevelType;
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
		    Log.LogInfo($"[DecideDifficulty] No key pressed, using config default: {configDefault}");
		    if (configDefault == EnsoData.EnsoLevelType.Ura && !isUraExist)
		    {
			    friendLevelType = EnsoData.EnsoLevelType.Mania;
		    }
		    else
		    {
			    friendLevelType = configDefault;
		    }
	    }
	    else
	    {
		    Log.LogInfo("[DecideDifficulty] No key pressed, choosing random difficulty");
		    friendLevelType = null;
	    }

	    return friendLevelType;
    }
}