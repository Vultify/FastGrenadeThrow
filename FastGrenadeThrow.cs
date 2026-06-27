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
    [BepInPlugin("com.vultify.fastgrenadethrow", "Fast Grenade Throw", "1.0.0")]
    public class FastGrenadeThrowPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<KeyboardShortcut> QuickThrowOverhand;
        public static ConfigEntry<KeyboardShortcut> QuickThrowUnderhand;
        internal static bool ForceLowThrow;

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

            if (QuickThrowOverhand.Value.IsDown())
            {
                TryQuickThrow(player, lowThrow: false);
            }
            else if (QuickThrowUnderhand.Value.IsDown())
            {
                TryQuickThrow(player, lowThrow: true);
            }
        }

        private static void TryQuickThrow(Player player, bool lowThrow)
        {
            var grenadeItem = FindGrenade(player);
            if (grenadeItem == null)
            {
                Log.LogWarning("No grenade found in pockets or rig.");
                return;
            }

            ForceLowThrow = lowThrow;

            player.Proceed(grenadeItem, delegate(Result<GInterface206> result)
            {
                if (result.Succeed)
                {
                    result.Value?.SetOnUsedCallback(delegate(Result<GInterface205<ThrowWeapItemClass>> throwResult)
                    {
                        ForceLowThrow = false;
                        player.TrySetLastEquippedWeapon();
                    });
                }
                else
                {
                    ForceLowThrow = false;
                    Log.LogWarning($"Failed to proceed with quick throw: {result.Error}");
                    player.TrySetLastEquippedWeapon();
                }
            });
        }

        private static ThrowWeapItemClass FindGrenade(Player player)
        {
            var equipment = player.Profile?.Inventory?.Equipment;
            if (equipment == null) return null;

            var pockets = equipment.GetSlot(EquipmentSlot.Pockets);
            var rig = equipment.GetSlot(EquipmentSlot.TacticalVest);

            foreach (var slot in new[] { pockets, rig })
            {
                if (slot?.ContainedItem == null) continue;

                var items = slot.ContainedItem.GetAllItems();
                var grenade = items?.FirstOrDefault(i => i is ThrowWeapItemClass) as ThrowWeapItemClass;
                if (grenade != null) return grenade;
            }

            return null;
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


}
