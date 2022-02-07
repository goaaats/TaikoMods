﻿using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HarmonyLib.Tools;
using TakoTako.SongSelect;
using UnityEngine;

namespace TakoTako
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public ConfigEntry<bool> ConfigSkipSplashScreen;
        public ConfigEntry<bool> ConfigDisableScreenChangeOnFocus;
        public ConfigEntry<bool> ConfigFixSignInScreen;
        public ConfigEntry<bool> ConfigEnableCustomSongs;

        public ConfigEntry<string> ConfigSongDirectory;
        public ConfigEntry<bool> ConfigSaveEnabled;
        public ConfigEntry<string> ConfigSaveDirectory;
        public ConfigEntry<bool> ConfigDisableCustomDLCSongs;
        public ConfigEntry<string> ConfigOverrideDefaultSongLanguage;
        public ConfigEntry<bool> ConfigApplyGenreOverride;

        public static Plugin Instance;
        private Harmony _harmony;
        public static ManualLogSource Log;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            SetupConfig();

            SetupHarmony();
        }

        private void SetupConfig()
        {
            var userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            ConfigEnableCustomSongs = Config.Bind("CustomSongs",
                "EnableCustomSongs",
                true,
                "When true this will load custom mods");

            ConfigSongDirectory = Config.Bind("CustomSongs",
                "SongDirectory",
                $"{userFolder}/Documents/{typeof(Plugin).Namespace}/customSongs",
                "The directory where custom tracks are stored");

            ConfigSaveEnabled = Config.Bind("CustomSongs",
                "SaveEnabled",
                true,
                "Should there be local saves? Disable this if you want to wipe modded saves with every load");

            ConfigSaveDirectory = Config.Bind("CustomSongs",
                "SaveDirectory",
                $"{userFolder}/Documents/{typeof(Plugin).Namespace}/saves",
                "The directory where saves are stored");

            ConfigDisableCustomDLCSongs = Config.Bind("CustomSongs",
                "DisableCustomDLCSongs",
                false,
                "By default, DLC is enabled for custom songs, this is to reduce any hiccups when playing online with other people. " +
                "Set this to true if you want DLC to be marked as false, be aware that the fact you're playing a custom song will be sent over the internet");

            ConfigOverrideDefaultSongLanguage = Config.Bind("CustomSongs",
                "ConfigOverrideDefaultSongLanguage",
                string.Empty,
                "Set this value to {Japanese, English, French, Italian, German, Spanish, ChineseTraditional, ChineseSimplified, Korean} " +
                "to override all music tracks to a certain language, regardless of your applications language");

            ConfigApplyGenreOverride = Config.Bind("CustomSongs",
                "ConfigApplyGenreOverride",
                true,
                "Set this value to {01 Pop, 02 Anime, 03 Vocaloid, 04 Children and Folk, 05 Variety, 06 Classical, 07 Game Music, 08 Live Festival Mode, 08 Namco Original} " +
                "to override all track's genre in a certain folder. This is useful for TJA files that do not have a genre");

            ConfigFixSignInScreen = Config.Bind("General",
                "FixSignInScreen",
                true,
                "When true this will apply the patch to fix signing into Xbox Live");

            ConfigSkipSplashScreen = Config.Bind("General",
                "SkipSplashScreen",
                true,
                "When true this will skip the intro");

            ConfigDisableScreenChangeOnFocus = Config.Bind("General",
                "DisableScreenChangeOnFocus",
                false,
                "When focusing this wont do anything jank, I thnk");
        }

        private void SetupHarmony()
        {
            // Patch methods
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            if (ConfigSkipSplashScreen.Value)
                _harmony.PatchAll(typeof(SkipSplashScreen));

            if (ConfigFixSignInScreen.Value)
                _harmony.PatchAll(typeof(SignInPatch));

            if (ConfigDisableScreenChangeOnFocus.Value)
                _harmony.PatchAll(typeof(DisableScreenChangeOnFocus));

            if (ConfigEnableCustomSongs.Value)
            {
                _harmony.PatchAll(typeof(MusicPatch));
                MusicPatch.Setup(_harmony);
            }

            _harmony.PatchAll(typeof(RandomRepeatPatch));
            _harmony.PatchAll(typeof(SongSelectFastScrollPatch));
        }

        public void StartCustomCoroutine(IEnumerator enumerator)
        {
            StartCoroutine(enumerator);
        }
    }
}