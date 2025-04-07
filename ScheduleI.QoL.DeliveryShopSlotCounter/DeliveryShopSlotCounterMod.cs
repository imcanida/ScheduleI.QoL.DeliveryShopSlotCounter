using System;
using System.Collections.Generic;
using MelonLoader;
using ScheduleOne.UI.Phone.Delivery;
using UnityEngine.UI;
using UnityEngine;
using HarmonyLib;
using ScheduleI.QoL.DeliveryShopSlotCounter;

[assembly: MelonInfo(typeof(DeliveryShopSlotCounterMod), "Schedule I - QoL Delivery Shop Slot Counter", "1.0.0", "Dreous")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ScheduleI.QoL.DeliveryShopSlotCounter
{
    public class DeliveryShopSlotCounterMod : MelonMod
    {
        //public override void OnInitializeMelon()
        //{
        //    var harmony = new HarmonyLib.Harmony("com.dreous.ScheduleIDeliveryShopSlotCounter");
        //    harmony.PatchAll();
        //}

        [HarmonyPatch(typeof(DeliveryShop))]
        public static class DeliveryShopPatches
        {
            // Store the original anchored position from the "Items" row.
            private static bool s_positionsInitialized = false;
            private static Vector2 s_posItems;

            [HarmonyPatch(nameof(DeliveryShop.RefreshCart))]
            [HarmonyPostfix]
            public static void RefreshCart_Postfix(DeliveryShop __instance)
            {
                try
                {
                    var listingEntriesField = AccessTools.Field(typeof(DeliveryShop), "listingEntries");
                    if (!(listingEntriesField.GetValue(__instance) is List<ListingEntry> listingEntries))
                        return;

                    int totalSlotsUsed = 0;
                    int capacity = DeliveryShop.DELIVERY_VEHICLE_SLOT_CAPACITY;
                    foreach (var entry in listingEntries)
                    {
                        if (entry == null || entry.MatchingListing?.Item == null)
                            continue;
                        int qty = entry.SelectedQuantity;
                        if (qty <= 0)
                            continue;
                        int stackLimit = entry.MatchingListing.Item.StackLimit;
                        if (stackLimit < 1)
                            stackLimit = 1;
                        totalSlotsUsed += Mathf.CeilToInt(qty / (float)stackLimit);
                    }

                    // Bit weak, perhaps a better method?
                    var panel = FindChildRecursive(
                        __instance.transform,
                        t => t.name.Equals("Panel", StringComparison.OrdinalIgnoreCase)
                    );

                    if (panel == null)
                    {
                        MelonLogger.Msg("[SlotsCounter] Panel not found, aborting.");
                        return;
                    }

                    var itemsRow = panel.Find("Items");           // Use Items Total as the base reference.
                    var deliveryRow = panel.Find("Delivery");     // Delivery Fee
                    var orderLimitRow = panel.Find("OrderLimit"); // Order Total

                    if (itemsRow == null || deliveryRow == null || orderLimitRow == null)
                    {
                        MelonLogger.Msg("[SlotsCounter] Could not find one or more of [Items, Delivery, OrderLimit]. Aborting layout.");
                        return;
                    }

                    var rtItems = itemsRow.GetComponent<RectTransform>();
                    var rtDelivery = deliveryRow.GetComponent<RectTransform>();
                    var rtOrderLimit = orderLimitRow.GetComponent<RectTransform>();
                    if (!s_positionsInitialized)
                    {
                        s_positionsInitialized = true;
                        s_posItems = rtItems.anchoredPosition;
                    }

                    var slotsUsedRow = panel.Find("SlotsUsed");
                    if (slotsUsedRow == null)
                    {
                        GameObject newRowObj = GameObject.Instantiate(itemsRow.gameObject, panel);
                        newRowObj.name = "SlotsUsed";
                        slotsUsedRow = newRowObj.transform;
                        Text labelText = newRowObj.GetComponent<Text>();
                        if (labelText != null)
                        {
                            labelText.text = "Slots Used";
                        }
                        else
                        {
                            var labelChild = newRowObj.transform.Find("Label");
                            if (labelChild != null)
                            {
                                Text childLabel = labelChild.GetComponent<Text>();
                                if (childLabel != null)
                                    childLabel.text = "Slots Used";
                            }
                        }
                    }
                    // Update the text for "SlotsUsed"
                    var amountText = slotsUsedRow.Find("Amount")?.GetComponent<Text>();
                    if (amountText != null)
                    {
                        amountText.text = $"{totalSlotsUsed} / {capacity}";
                        // Set color based on condition: if over capacity, set to red, otherwise green.
                        if (totalSlotsUsed > capacity)
                            amountText.color = Color.red;
                        else
                            amountText.color = Color.green;
                    }

                    var rtSlotsUsed = slotsUsedRow.GetComponent<RectTransform>();
                    // We want:
                    //   SlotsUsed to appear exactly at s_posItems.
                    //   Then, Items (which is the original Total row) just below it,
                    //   Delivery (Delivery Fee) below that,
                    //   OrderLimit (Order Total) below that.
                    var rowSpacing = 29f; // best fit with 4 items in the column.
                    rtSlotsUsed.anchoredPosition = s_posItems;
                    rtItems.anchoredPosition = new Vector2(s_posItems.x, s_posItems.y - rowSpacing);
                    rtDelivery.anchoredPosition = new Vector2(s_posItems.x, s_posItems.y - rowSpacing * 2);
                    rtOrderLimit.anchoredPosition = new Vector2(s_posItems.x, s_posItems.y - rowSpacing * 3);

                    Canvas.ForceUpdateCanvases();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"RefreshCart_Postfix Error: {ex}");
                }
            }

            public static Transform FindChildRecursive(Transform parent, Func<Transform, bool> predicate)
            {
                if (predicate(parent))
                    return parent;
                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform child = parent.GetChild(i);
                    Transform found = FindChildRecursive(child, predicate);
                    if (found != null)
                        return found;
                }
                return null;
            }
        }

    }
}