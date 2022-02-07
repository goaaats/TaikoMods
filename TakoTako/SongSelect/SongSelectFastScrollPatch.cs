using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace TakoTako.SongSelect;

/// <summary>
/// This patch prevents the game from advancing to the course select after rolling a random song
/// </summary>
[HarmonyPatch(typeof(SongSelectManager))]
[HarmonyPatch("UpdateSongSelect")]
public class SongSelectFastScrollPatch
{
    private static DateTimeOffset lastChange;
    private static DateTimeOffset lastTrigger;

    private static bool shiftArmed;
    private static bool shiftOk;

    public static bool Prefix(SongSelectManager __instance)
    {
        if (__instance.CurrentState != SongSelectManager.State.SongSelect /* || __instance.isKanbanMoving */|| __instance.SongList.Count <= 0)
        {
            return true;
        }

        var dir = TaikoSingletonMonoBehaviour<ControllerManager>.Instance.GetDirectionButton(ControllerManager.ControllerPlayerNo.Player1, ControllerManager.Prio.None);

        var shiftDown = dir is ControllerManager.Dir.Up or ControllerManager.Dir.Down;

        if (shiftDown && !shiftArmed)
        {
            shiftArmed = true;
            UnityEngine.Debug.Log("Armed");
        }

        if (!shiftDown && shiftArmed)
        {
            shiftOk = true;
            shiftArmed = false;
            lastChange = DateTimeOffset.Now;
            UnityEngine.Debug.Log("Ok");
        }

        const int delay = 200;
        /*
        if (shiftOk && !shiftDown)
        {
            timeSinceLastShift++;
        }
        else */if (shiftDown && shiftOk && (DateTimeOffset.Now - lastChange).TotalMilliseconds < delay)
        {
            UnityEngine.Debug.Log("Is trigger");
            var songIndex = (dir == ControllerManager.Dir.Up ? __instance.SelectedSongIndex - 10 : __instance.SelectedSongIndex + 10) % __instance.SongList.Count;
            //__instance.SelectSong(songIndex, SongSelectManager.KanbanMoveType.MoveUp, SongSelectManager.KanbanMoveSpeed.Skip);
            __instance.SelectedSongIndex = songIndex;

            __instance.UpdateCenterKanbanSurface(isDecidedSong: true);
            __instance.kanbans[0].RootAnim.Play("SelectOn", 0, 1f);
            __instance.kanbans[0].EffectBonusL.InitAnim();
            __instance.sortBarView.ShowView();
            __instance.UpdateSortBarSurface();
            __instance.Score1PObject.FadeIn();
            if (__instance.status.Is2PActive)
            {
                __instance.Score2PObject.FadeIn();
            }
            __instance.UpdateScoreDisplay();
            __instance.SongSelectBg.SetGenre((EnsoData.SongGenre)__instance.SongList[__instance.SelectedSongIndex].SongGenre);

            __instance.UpdateKanbanSurface();
            __instance.isKanbanMoving = false;
            __instance.kanbanMoveCount = 0;

            lastTrigger = DateTimeOffset.Now;

            TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MySoundManager.CommonSePlay("fast", isExclusive: false);

            shiftArmed = false;
            shiftOk = false;

            //return true;
        }
        else if (shiftOk && (DateTimeOffset.Now - lastChange).TotalMilliseconds >= delay)
        {
            shiftArmed = false;
            shiftOk = false;
            UnityEngine.Debug.Log("Discard");
        }

        /*
        if ((DateTimeOffset.Now - lastChange).TotalMilliseconds < 300)
        {
            UnityEngine.Debug.Log("Is trigger");
            IEnumerator SkipTenSongs()
            {
                for (var i = 0; i < 10; i++)
                {
                    var songIndex = (__instance.SelectedSongIndex + 1) % __instance.SongList.Count;
                    __instance.SelectSong(songIndex, SongSelectManager.KanbanMoveType.MoveDown, SongSelectManager.KanbanMoveSpeed.Normal);

                    yield return new WaitForSeconds(0.05f);
                }
            }

            __instance.StartCoroutine(SkipTenSongs());

            return false;
        }

        UnityEngine.Debug.Log("Is set");

        lastChange = DateTimeOffset.Now;
        */
        return !((DateTimeOffset.Now - lastTrigger).TotalMilliseconds < 500);
    }
}