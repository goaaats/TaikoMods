﻿using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FlutoTaikoMods;
using HarmonyLib;
using HarmonyLib.Tools;

namespace TaikoMods
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public ConfigEntry<bool> ConfigSkipSplashScreen;
        public ConfigEntry<bool> ConfigDisableScreenChangeOnFocus;
        public ConfigEntry<bool> ConfigFixSignInScreen;
        public ConfigEntry<int> ConfigFriendMatchingDefaultDifficulty;
        public ConfigEntry<bool> ConfigEnableCustomSongs;
        public ConfigEntry<string> ConfigSongDirectory;
        public ConfigEntry<string> ConfigSaveDirectory;

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

            ConfigFriendMatchingDefaultDifficulty = Config.Bind("General",
                "FriendMatchingDefaultDifficulty",
                0,
                "Default difficulty for friend matching: 0 = default rank-based, 1 = easy, 2 = normal, 3 = hard, 4 = oni, 5 = ura(if exist)");

            ConfigEnableCustomSongs = Config.Bind("CustomSongs",
                "EnableCustomSongs",
                true,
                "When true this will load custom mods");

            ConfigSongDirectory = Config.Bind("CustomSongs",
                "SongDirectory",
                $"{userFolder}/Documents/TaikoTheDrumMasterMods/customSongs",
                "The directory where custom tracks are stored");

            ConfigSaveDirectory = Config.Bind("CustomSongs",
                "SaveDirectory",
                $"{userFolder}/Documents/TaikoTheDrumMasterMods/saves",
                "The directory where saves are stored");
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

            this._harmony.PatchAll(typeof(RankedMatchSongSelectPatch));
            this._harmony.PatchAll(typeof(RankedMatchNetworkDlcPatch));
        }

        public void StartCustomCoroutine(IEnumerator enumerator)
        {
            StartCoroutine(enumerator);
        }
    }
}