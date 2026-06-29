using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using HarmonyLib;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace FastGrenadeThrow
{
    [BepInPlugin("com.vultify.fastgrenadethrow", "Fast Grenade Throw", "1.0.3")]
    public class FastGrenadeThrowPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<KeyboardShortcut> QuickThrowOverhand;
        public static ConfigEntry<KeyboardShortcut> QuickThrowUnderhand;
        internal static bool ForceLowThrow;
        private static bool _throwInProgress = false;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind(
                "Settings",
                "Enabled",
                true,
                "Enable fast grenade throw keybinds"
            );

            QuickThrowOverhand = Config.Bind(
                "Keybinds",
                "Quick Throw (Overhand)",
                new KeyboardShortcut(KeyCode.G),
                "Press to instantly overhand throw your selected grenade"
            );

            QuickThrowUnderhand = Config.Bind(
                "Keybinds",
                "Quick Throw (Underhand)",
                new KeyboardShortcut(KeyCode.H),
                "Press to instantly underhand throw your selected grenade"
            );

            new Harmony("com.vultify.fastgrenadethrow").PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo("Fast Grenade Throw loaded.");
        }

        private void Update()
        {
            if (!Enabled.Value) return;

            var gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld == null) return;

            var player = gameWorld.MainPlayer;
            if (player == null || !player.IsYourPlayer) return;
            if (!player.HealthController.IsAlive) return;

            if (Input.GetKeyDown(QuickThrowOverhand.Value.MainKey))
            {
                TryQuickThrow(player, lowThrow: false);
            }
            else if (Input.GetKeyDown(QuickThrowUnderhand.Value.MainKey))
            {
                TryQuickThrow(player, lowThrow: true);
            }
        }

        private static void TryQuickThrow(Player player, bool lowThrow)
        {
            if (_throwInProgress) return;

            if (player.HandsController is Player.QuickGrenadeThrowHandsController)
                return;

            var grenadeItem = FindGrenade(player);
            if (grenadeItem == null)
            {
                Log.LogWarning("No grenade found in pockets or rig.");
                return;
            }

            if (player.HandsController?.Item == grenadeItem)
                return;

            ForceLowThrow = lowThrow;
            _throwInProgress = true;

            player.Proceed(grenadeItem, delegate(Result<GInterface206> result)
            {
                if (result.Succeed)
                {
                    result.Value?.SetOnUsedCallback(delegate(Result<GInterface205<ThrowWeapItemClass>> throwResult)
                    {
                        ForceLowThrow = false;
                        _throwInProgress = false;
                        player.TrySetLastEquippedWeapon();
                    });
                }
                else
                {
                    ForceLowThrow = false;
                    _throwInProgress = false;
                    Log.LogWarning($"Failed to proceed with quick throw: {result.Error}");
                    player.TrySetLastEquippedWeapon();
                }
            });
        }

        private static ThrowWeapItemClass FindGrenade(Player player)
        {
            var equipment = player.Profile?.Inventory?.Equipment;
            if (equipment == null) return null;

            return equipment.GetAllItems()
                .FirstOrDefault(i => i is ThrowWeapItemClass) as ThrowWeapItemClass;
        }
    }

    [HarmonyPatch(typeof(Player.BaseGrenadeHandsController), "vmethod_1")]
    public static class ForceLowThrowPatch
    {
        [HarmonyPrefix]
        public static void Prefix(
            Player.BaseGrenadeHandsController __instance,
            ref bool low)
        {
            if (!FastGrenadeThrowPlugin.Enabled.Value) return;
            if (!FastGrenadeThrowPlugin.ForceLowThrow) return;

            var playerField = AccessTools.Field(typeof(Player.BaseGrenadeHandsController), "_player");
            if (playerField?.GetValue(__instance) is not Player player) return;

            if (__instance is Player.QuickGrenadeThrowHandsController && player.IsYourPlayer)
            {
                low = true;
            }
        }
    }

    [HarmonyPatch(typeof(Player.BaseGrenadeHandsController), "FindThrowPosition")]
    public static class FlashbangThrowPositionPatch
    {
        private const string M7290_ID = "619256e5f8af2c1a4e1f5d92";
        private const string ZARYA_ID = "5a0c27731526d80618476ac4";

        [HarmonyPostfix]
        public static void Postfix(
            Player.BaseGrenadeHandsController __instance,
            ref Vector3 __result)
        {
            if (!FastGrenadeThrowPlugin.Enabled.Value) return;
            if (__instance is not Player.QuickGrenadeThrowHandsController) return;

            var playerField = AccessTools.Field(typeof(Player.BaseGrenadeHandsController), "_player");
            if (playerField?.GetValue(__instance) is not Player player) return;
            if (!player.IsYourPlayer) return;

            var item = __instance.Item;
            if (item == null) return;

            if (item.StringTemplateId == M7290_ID)
            {
                __result += player.Transform.up * 1.2f;
                __result += player.Transform.forward * 0.2f;
            }
        }
    }
}
