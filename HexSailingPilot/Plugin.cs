using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace HexSailingPilot
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "com.hex.sailing.pilot";
        private const string PluginName = "HexSailingPilot";
        private const string PluginVersion = "1.0.0";

        private Harmony _harmonyInstance;

        internal static ConfigEntry<KeyboardShortcut> AutoPilotHotKey;

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            AutoPilotHotKey = Config.Bind("Hotkeys", "AutoPilotHotKey", new KeyboardShortcut(KeyCode.P, KeyCode.LeftControl), "Hotkey to toggle autopilot.");

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmonyInstance = new Harmony(PluginGuid);
            _harmonyInstance.PatchAll(assembly);

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }

        private void OnDestroy()
        {
            Log.LogInfo($"{PluginName} v{PluginVersion} unloaded.");

            _harmonyInstance?.UnpatchSelf();
            _harmonyInstance = null;
            Instance = null;
            Log = null;
        }

        private void Update()
        {
            Player player = Player.m_localPlayer;

            if (player == null || player.GetControlledShip() == null)
            {
                return;
            }

            if (AutoPilotHotKey.Value.IsDown())
            {
                SailingPilotController.Toggle();
            }
        }
    }
}