using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GrapplingHook
{
    [BepInPlugin(Guid, "Grappling Hook", "0.1.0")]
    public class GrapplingPlugin : BasePlugin
    {
        public const string Guid = "com.dani.grapplinghook";

        internal static ManualLogSource Logger { get; private set; }
        internal static GrapplingSettings Settings { get; private set; }

        public override void Load()
        {
            Logger = Log;

            // The config file lives next to the dll instead of BepInEx\config, so the whole
            // mod is one folder you can copy between installs and keep your binds.
            string folder = Path.GetDirectoryName(typeof(GrapplingPlugin).Assembly.Location) ?? Paths.ConfigPath;
            string path = Path.Combine(folder, "GrapplingHook.cfg");

            Settings = new GrapplingSettings(new ConfigFile(path, true));
            Settings.Validate();

            ClassInjector.RegisterTypeInIl2Cpp<GrapplingBehaviour>();

            var host = new GameObject("GrapplingHook");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<GrapplingBehaviour>();

            Logger.LogInfo($"Loaded. {Settings.Summary()}");
            Logger.LogInfo($"Settings file: {path}");
        }

        public override bool Unload()
        {
            Settings?.Save();
            return true;
        }
    }
}
