using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using HarmonyLib;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace FastGrenadeThrow
{
    [BepInPlugin("com.vultify.fastgrenadethrow", "Fast Grenade Throw", "1.0.4")]
    public class FastGrenadeThrowPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> DebugLogging;
        public static ConfigEntry<KeyboardShortcut> QuickThrowOverhand;
        public static ConfigEntry<KeyboardShortcut> QuickThrowUnderhand;
        internal static bool ForceLowThrow;
        private static bool _throwInProgress = false;

        private const string UIA_GUID = "com.cj.useFromAnywhere";

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind(
                "Settings",
                "Enabled",
                true,
                "Enable fast grenade throw keybinds");

            DebugLogging = Config.Bind(
                "Settings",
                "Debug Logging",
                false,
                "Enable detailed debug logging to BepInEx/LogOutput.log — use when reporting bugs");

            QuickThrowOverhand = Config.Bind(
                "Keybinds",
                "Quick Throw (Overhand)",
                new KeyboardShortcut(KeyCode.G),
                "Press to instantly overhand throw your selected grenade");

            QuickThrowUnderhand = Config.Bind(
                "Keybinds",
                "Quick Throw (Underhand)",
                new KeyboardShortcut(KeyCode.H),
                "Press to instantly underhand throw your selected grenade");

            var harmony = new Harmony("com.vultify.fastgrenadethrow");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            bool uiaInstalled = Chainloader.PluginInfos.ContainsKey(UIA_GUID);
            if (uiaInstalled)
            {
                Log.LogInfo("Use Items Anywhere detected — hooking into their grenade slot system.");
            }
            else
            {
                Log.LogInfo("Use Items Anywhere not detected — applying backpack grenade support.");
                harmony.Patch(
                    AccessTools.Method(typeof(GClass3373), "GetThrowablePriorityGrenadesList"),
                    prefix: new HarmonyMethod(typeof(BackpackGrenadePatch).GetMethod(nameof(BackpackGrenadePatch.Prefix))));
            }

            Log.LogInfo("Fast Grenade Throw loaded.");
        }

        internal static void DebugLog(string message)
        {
            if (DebugLogging.Value)
                Log.LogInfo($"[FGT Debug] {message}");
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
                TryQuickThrow(player, lowThrow: false);
            else if (Input.GetKeyDown(QuickThrowUnderhand.Value.MainKey))
                TryQuickThrow(player, lowThrow: true);
        }

        private static void TryQuickThrow(Player player, bool lowThrow)
        {
            DebugLog($"Key pressed — lowThrow={lowThrow}, throwInProgress={_throwInProgress}, HandsController={player.HandsController?.GetType().Name ?? "null"}");

            if (_throwInProgress)
            {
                DebugLog("Blocked: throw already in progress.");
                return;
            }

            if (player.HandsController is Player.QuickGrenadeThrowHandsController)
            {
                DebugLog("Blocked: already in QuickGrenadeThrowHandsController.");
                return;
            }

            var grenadeItem = FindGrenade(player);
            if (grenadeItem == null)
            {
                Log.LogWarning("No grenade found in pockets or rig.");
                return;
            }

            if (player.HandsController?.Item == grenadeItem)
            {
                DebugLog("Blocked: grenade is already the active item in HandsController.");
                return;
            }

            ForceLowThrow = lowThrow;
            _throwInProgress = true;
            DebugLog($"Calling Proceed with grenade '{grenadeItem.LocalizedName()}' (templateId={grenadeItem.StringTemplateId})");

            player.Proceed(grenadeItem, delegate(Result<GInterface206> result)
            {
                if (result.Succeed)
                {
                    DebugLog("Proceed succeeded — waiting for throw callback.");
                    result.Value?.SetOnUsedCallback(delegate(Result<GInterface205<ThrowWeapItemClass>> throwResult)
                    {
                        DebugLog($"Throw callback fired — success={throwResult.Succeed}{(throwResult.Succeed ? "" : $", error={throwResult.Error}")}");
                        ForceLowThrow = false;
                        _throwInProgress = false;
                        player.TrySetLastEquippedWeapon();
                        DebugLog("Throw complete — reverted to last weapon.");
                    });
                }
                else
                {
                    ForceLowThrow = false;
                    _throwInProgress = false;
                    Log.LogWarning($"Failed to proceed with quick throw: {result.Error}");
                    DebugLog($"Proceed failed: {result.Error}");
                    player.TrySetLastEquippedWeapon();
                }
            });
        }

        private static ThrowWeapItemClass FindGrenade(Player player)
        {
            var list = GClass3373.GetThrowablePriorityGrenadesList(player.InventoryController);
            var grenade = list?.FirstOrDefault();
            DebugLog($"FindGrenade: found {list?.Count ?? 0} grenade(s), picking '{grenade?.LocalizedName() ?? "none"}' (templateId={grenade?.StringTemplateId ?? "null"})");
            return grenade;
        }
    }

    // only when Use Items Anywhere isn't installed, adds backpack to the grenade search
    public static class BackpackGrenadePatch
    {
        public static bool Prefix(InventoryController inventoryController, ref List<ThrowWeapItemClass> __result)
        {
            var containers = new List<CompoundItem>();
            var equipment = inventoryController.Inventory.Equipment;

            if (equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem is CompoundItem rig)
                containers.Add(rig);
            if (equipment.GetSlot(EquipmentSlot.Pockets).ContainedItem is CompoundItem pockets)
                containers.Add(pockets);
            if (equipment.GetSlot(EquipmentSlot.ArmBand).ContainedItem is CompoundItem armband)
                containers.Add(armband);
            if (equipment.GetSlot(EquipmentSlot.Backpack).ContainedItem is CompoundItem backpack)
                containers.Add(backpack);

            __result = containers
                .SelectMany(c => c.GetAllItems())
                .OfType<ThrowWeapItemClass>()
                .Where(inventoryController.Examined)
                .OrderBy(g => g.ThrowType)
                .ToList();

            return false;
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
                low = true;
        }
    }

    [HarmonyPatch(typeof(Player.BaseGrenadeHandsController), "FindThrowPosition")]
    public static class FlashbangThrowPositionPatch
    {
        private const string M7290_ID = "619256e5f8af2c1a4e1f5d92";

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
