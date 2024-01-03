using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ResourceInfo {
    [HarmonyPatch(typeof(UIItemTip))]
    [HarmonyPatch("SetTip")]
    static class Patch_UIItemTip_SetTip {
        const int RECIPE_ENTRY_ARR_SIZE = 32;
        const int RECIPE_ICON_SIZE = 40;
        const int RECIPE_PADDING = 25;
        const int RECIPE_OFFSET = 11;
        const int RECIPE_START = 12;
        const int RECIPE_ARROW = 1;

        static void Prefix(UIItemTip __instance, int __0, int __1, Vector2 __2, Transform __3) {
            ref UIRecipeEntry[] recipeEntryArr = ref AccessTools.FieldRefAccess<UIItemTip, UIRecipeEntry[]>(__instance, "recipeEntryArr");

            // Safe 'clean up' of existing recipes in the list.
            if (recipeEntryArr != null) {
                for (int recipeEntryNum = 0; recipeEntryNum < recipeEntryArr.Length; recipeEntryNum++) {
                    if (recipeEntryArr[recipeEntryNum] != null) {
                        recipeEntryArr[recipeEntryNum].gameObject.SetActive(false);
                    }
                }
            }
        }

        static void Postfix(UIItemTip __instance, int __0, int __1, Vector2 __2, Transform __3) {
            int itemId = __0;

            int protoId;
            int recipeId;

            // Safe item identifier handling.
            if (itemId > 0) {
                protoId = itemId;
                recipeId = 0;
            } else if (itemId < 0) {
                protoId = 0;
                recipeId = -itemId;
            } else {
                protoId = 0;
                recipeId = 0;
            }

            // Fetch the data from item/recipe database.
            ItemProto itemProto = LDB.items.Select(protoId);
            RecipeProto recipeProto = LDB.recipes.Select(recipeId);
            List<RecipeProto> tempRecipeList = new List<RecipeProto>();

            // Trigger additional recipes appearance in UI.
            if (Input.GetKey(MainPlugin.HotkeyPerMinute.Value)) {

                // Recipe array fallback (to avoid exception).
                if (recipeProto != null) {
                    tempRecipeList.Add(null);
                    tempRecipeList[0] = recipeProto;
                }

                // Get actual list of recipes for the entry.
                List<RecipeProto> recipeList = (itemProto != null) ? itemProto.recipes : ((recipeProto != null) ? tempRecipeList : null);
                ref UIRecipeEntry recipeEntry = ref AccessTools.FieldRefAccess<UIItemTip, UIRecipeEntry>(__instance, "recipeEntry");
                ref UIRecipeEntry[] recipeEntryArr = ref AccessTools.FieldRefAccess<UIItemTip, UIRecipeEntry[]>(__instance, "recipeEntryArr");

                // Fail-safe measures against missing object reference.
                if (recipeEntry == null) recipeEntry = new UIRecipeEntry();
                if (recipeEntryArr == null) recipeEntryArr = new UIRecipeEntry[32];

                // Expand recipe array to avoid touching original offsets.
                if (recipeEntryArr != null && recipeEntryArr.Length == RECIPE_ENTRY_ARR_SIZE) {
                    Array.Resize(ref recipeEntryArr, RECIPE_ENTRY_ARR_SIZE * 2);
                }

                // Work on the recipe list, only if it exists and has something.
                if (recipeList != null && recipeList.Count > 0) {
                    int validRecipeOffset = 0;
                    float recipeMaxWidth = 0;

                    // Find longest recipe in the list.
                    for (int recipeOffset = 0; recipeOffset < recipeList.Count; recipeOffset++) {
                        if (recipeList[recipeOffset].Type == ERecipeType.Fractionate) continue;
                        float recipeWidth = (recipeList[recipeOffset].Results.Length + 
                            recipeList[recipeOffset].Items.Length + RECIPE_ARROW) * 
                            RECIPE_ICON_SIZE + RECIPE_PADDING;
                        if (recipeWidth > recipeMaxWidth) recipeMaxWidth = recipeWidth;
                    }

                    // Populate recipe list with additional recipes.
                    for (int recipeOffset = 0; recipeOffset < recipeList.Count; recipeOffset++) {
                        int newEntriesCount = GetEntriesNum(recipeList[recipeOffset].Type);

                        // Add new recipes based on available facility tiers.
                        for (int modRecipeOffset = 0; modRecipeOffset < newEntriesCount; modRecipeOffset++) {

                            // Instantiate new recipe entry, if entry doesn't exist.
                            if (recipeEntryArr[RECIPE_ENTRY_ARR_SIZE + validRecipeOffset] == null) {
                                recipeEntryArr[RECIPE_ENTRY_ARR_SIZE + validRecipeOffset] =
                                UnityEngine.Object.Instantiate<UIRecipeEntry>(recipeEntry, __instance.transform);
                            }

                            // Update instantiated recipe entry with new data and position.
                            SetRecipePerMin(recipeEntryArr[RECIPE_ENTRY_ARR_SIZE + validRecipeOffset], recipeList[recipeOffset], modRecipeOffset + 1);
                            recipeEntryArr[RECIPE_ENTRY_ARR_SIZE + validRecipeOffset].rectTrans.anchoredPosition = new Vector2(
                                recipeEntryArr[0].rectTrans.anchoredPosition.x + recipeMaxWidth,
                                recipeEntryArr[0].rectTrans.anchoredPosition.y - 
                                validRecipeOffset * RECIPE_ICON_SIZE);
                            recipeEntryArr[RECIPE_ENTRY_ARR_SIZE + validRecipeOffset].gameObject.SetActive(true);
                            
                            // Increase valid entries counter.
                            validRecipeOffset++;
                        }
                    }

                    // Access UI and calculate UI expansion values.
                    ref RectTransform uiRecipeTransform = ref AccessTools.FieldRefAccess<UIItemTip, RectTransform>(__instance, "trans");
                    float uiHeightExpansion = Mathf.Max(0, validRecipeOffset - recipeList.Count) * RECIPE_ICON_SIZE;
                    float uiWidthExpansion = Mathf.Max(0, recipeMaxWidth * 2 - uiRecipeTransform.sizeDelta.x);

                    // Perform actual UI expansion, only when there is need to do so.
                    if (uiWidthExpansion > 0 || uiHeightExpansion > 0) {
                        uiRecipeTransform.sizeDelta = new Vector2(
                            uiRecipeTransform.sizeDelta.x + uiWidthExpansion, 
                            uiRecipeTransform.sizeDelta.y + uiHeightExpansion);
                        uiRecipeTransform.SetParent(UIRoot.instance.itemTipTransform, true);
                        Rect tipRect = UIRoot.instance.itemTipTransform.rect;
                        float refWidth = (float)Mathf.RoundToInt(tipRect.width);
                        float refHeight = (float)Mathf.RoundToInt(tipRect.height);
                        float xOffset = uiRecipeTransform.anchorMin.x * 
                            refWidth + uiRecipeTransform.anchoredPosition.x;
                        float yOffset = uiRecipeTransform.anchorMin.y * 
                            refHeight + uiRecipeTransform.anchoredPosition.y;
                        Rect uiRecipeRect = uiRecipeTransform.rect;
                        uiRecipeRect.x += xOffset;
                        uiRecipeRect.y += yOffset;
                        Vector2 reSize = Vector2.zero;
                        if (uiRecipeRect.xMin < 0f) reSize.x -= uiRecipeRect.xMin;
                        if (uiRecipeRect.yMin < 0f) reSize.y -= uiRecipeRect.yMin;
                        if (uiRecipeRect.xMax > refWidth) reSize.x -= uiRecipeRect.xMax - refWidth;
                        if (uiRecipeRect.yMax > refHeight) reSize.y -= uiRecipeRect.yMax - refHeight;
                        uiRecipeTransform.anchoredPosition = uiRecipeTransform.anchoredPosition + reSize;
                        uiRecipeTransform.anchoredPosition = new Vector2(
                            uiRecipeTransform.anchoredPosition.x, 
                            uiRecipeTransform.anchoredPosition.y);
                        uiRecipeTransform.localScale = new Vector3(1f, 1f, 1f);
                    }
                }
            } else if (Input.GetKey(MainPlugin.HotkeyRelatedComponents.Value) 
                || Input.GetKey(MainPlugin.HotkeyRelatedBuildings.Value)) {

                // Recipe array fallback (to avoid exception).
                if (recipeProto != null) {
                    tempRecipeList.Add(null);
                    tempRecipeList[0] = recipeProto;
                }

                // Get actual list of recipes for the entry + additional configs.
                List<RecipeProto> recipeList = (itemProto != null) ? itemProto.recipes : ((recipeProto != null) ? tempRecipeList : null);
                ref UIRecipeEntry recipeEntry = ref AccessTools.FieldRefAccess<UIItemTip, UIRecipeEntry>(__instance, "recipeEntry");
                ref UIRecipeEntry[] recipeEntryArr = ref AccessTools.FieldRefAccess<UIItemTip, UIRecipeEntry[]>(__instance, "recipeEntryArr");
                bool isComponentLookup = Input.GetKey(MainPlugin.HotkeyRelatedComponents.Value);
                bool isBuildingLookup = Input.GetKey(MainPlugin.HotkeyRelatedBuildings.Value);
                int refProtoId = itemProto != null ? protoId : GetRecipeProto(recipeId);
                List<RecipeProto> useRecipeList = new List<RecipeProto>();

                // Fail-safe measures against missing object reference.
                if (recipeEntry == null) recipeEntry = new UIRecipeEntry();
                if (recipeEntryArr == null) recipeEntryArr = new UIRecipeEntry[32];

                // Expand recipe array to avoid touching original offsets.
                if (recipeEntryArr != null && recipeEntryArr.Length == RECIPE_ENTRY_ARR_SIZE) {
                    Array.Resize(ref recipeEntryArr, RECIPE_ENTRY_ARR_SIZE * 2);
                }

                // Cycle through all recipes and check which of them uses selected item.
                foreach (RecipeProto refRecipe in LDB.recipes.dataArray) {
                    if (refRecipe.Items.Contains(refProtoId)) {

                        // Safely reference item from the recipe.
                        ItemProto refItem = refRecipe.Results.Length > 1 ?
                            LDB.items.Select(GetRecipeProto(refRecipe.ID)) :
                            LDB.items.Select(refRecipe.Results[0]);

                        // Will show components and/or buildings recipes based on settings.
                        if (!((isBuildingLookup && IsBuilding(refItem)) ||
                            (isComponentLookup && !IsBuilding(refItem)) ||
                            (MainPlugin.AllowCombinedRecipes.Value && 
                            isBuildingLookup && isComponentLookup))) continue;

                        // Will ignore locked technology recipes, based on settings.
                        if (!GameMain.history.recipeUnlocked.Contains(refRecipe.ID) &&
                            !MainPlugin.IgnoreResearchUnlock.Value) continue;

                        // Finally, add recipe to the list.
                        useRecipeList.Add(refRecipe);
                    }
                }

                // Work on the recipe list, only if it exists and has something.
                if (useRecipeList != null && useRecipeList.Count > 0) {
                    bool itemHasRecipes = recipeList != null && recipeList.Count > 0;
                    float recipeMaxWidth = 0;
                    float addMaxWidth = 0;

                    // Find longest recipe in the original list, if it has something.
                    if (itemHasRecipes) {
                        for (int recipeOffset = 0; recipeOffset < recipeList.Count; recipeOffset++) {
                            float recipeWidth = (recipeList[recipeOffset].Results.Length +
                                recipeList[recipeOffset].Items.Length + RECIPE_ARROW) *
                                RECIPE_ICON_SIZE + RECIPE_PADDING;
                            if (recipeWidth > recipeMaxWidth) recipeMaxWidth = recipeWidth;
                        }
                    }

                    // Find longest recipe in the newly generated list.
                    for (int useOffset = 0; useOffset < useRecipeList.Count; useOffset++) {
                        float recipeWidth = (useRecipeList[useOffset].Results.Length +
                            useRecipeList[useOffset].Items.Length + RECIPE_ARROW) *
                            RECIPE_ICON_SIZE + RECIPE_PADDING;
                        if (recipeWidth > addMaxWidth) addMaxWidth = recipeWidth;
                    }

                    // Populate recipe list with additional related recipes.
                    for (int useOffset = 0; useOffset < useRecipeList.Count; useOffset++) {

                        // Instantiate new recipe entry, if entry doesn't exist.
                        if (recipeEntryArr[RECIPE_ENTRY_ARR_SIZE + useOffset] == null) {
                            recipeEntryArr[RECIPE_ENTRY_ARR_SIZE + useOffset] =
                            UnityEngine.Object.Instantiate<UIRecipeEntry>(recipeEntry, __instance.transform);
                        }

                        // Update instantiated recipe entry with new data and position.
                        recipeEntryArr[RECIPE_ENTRY_ARR_SIZE + useOffset].SetRecipe(useRecipeList[useOffset]);
                        recipeEntryArr[RECIPE_ENTRY_ARR_SIZE + useOffset].rectTrans.anchoredPosition = new Vector2(
                            (itemHasRecipes ? recipeEntryArr[0].rectTrans.anchoredPosition.x : 
                            RECIPE_START) + recipeMaxWidth,
                            (itemHasRecipes ? recipeEntryArr[0].rectTrans.anchoredPosition.y : 
                            __instance.trans.rect.y - RECIPE_OFFSET) - useOffset * RECIPE_ICON_SIZE);
                        recipeEntryArr[RECIPE_ENTRY_ARR_SIZE + useOffset].gameObject.SetActive(true);

                        // Add recipe separation line to UI without initial recipes.
                        if (!itemHasRecipes) {
                            __instance.sepLine.gameObject.SetActive(true);
                            __instance.sepLine.anchoredPosition = new Vector2(RECIPE_START, __instance.trans.rect.y);
                        }
                    }

                    // Access UI and calculate UI expansion values.
                    ref RectTransform uiRecipeTransform = ref AccessTools.FieldRefAccess<UIItemTip, RectTransform>(__instance, "trans");
                    float uiHeightExpansion = Mathf.Max(0, useRecipeList.Count - recipeList.Count) * 
                        RECIPE_ICON_SIZE + (itemHasRecipes ? 0 : RECIPE_OFFSET * 2);
                    float uiWidthExpansion = Mathf.Max(0, recipeMaxWidth + addMaxWidth - uiRecipeTransform.sizeDelta.x);

                    // Perform actual UI expansion, only when there is need to do so.
                    if (uiWidthExpansion > 0 || uiHeightExpansion > 0) {
                        uiRecipeTransform.sizeDelta = new Vector2(
                            uiRecipeTransform.sizeDelta.x + uiWidthExpansion,
                            uiRecipeTransform.sizeDelta.y + uiHeightExpansion);
                        uiRecipeTransform.SetParent(UIRoot.instance.itemTipTransform, true);
                        Rect tipRect = UIRoot.instance.itemTipTransform.rect;
                        float refWidth = (float)Mathf.RoundToInt(tipRect.width);
                        float refHeight = (float)Mathf.RoundToInt(tipRect.height);
                        float xOffset = uiRecipeTransform.anchorMin.x * 
                            refWidth + uiRecipeTransform.anchoredPosition.x;
                        float yOffset = uiRecipeTransform.anchorMin.y * 
                            refHeight + uiRecipeTransform.anchoredPosition.y;
                        Rect uiRecipeRect = uiRecipeTransform.rect;
                        uiRecipeRect.x += xOffset;
                        uiRecipeRect.y += yOffset;
                        Vector2 reSize = Vector2.zero;
                        if (uiRecipeRect.xMin < 0f) reSize.x -= uiRecipeRect.xMin;
                        if (uiRecipeRect.yMin < 0f) reSize.y -= uiRecipeRect.yMin;
                        if (uiRecipeRect.xMax > refWidth) reSize.x -= uiRecipeRect.xMax - refWidth;
                        if (uiRecipeRect.yMax > refHeight) reSize.y -= uiRecipeRect.yMax - refHeight;
                        uiRecipeTransform.anchoredPosition = uiRecipeTransform.anchoredPosition + reSize;
                        uiRecipeTransform.anchoredPosition = new Vector2(
                            uiRecipeTransform.anchoredPosition.x,
                            uiRecipeTransform.anchoredPosition.y);
                        uiRecipeTransform.localScale = new Vector3(1f, 1f, 1f);
                    }
                }
            }

            // Show belts and sorters speed/cycles per minute.
            if (itemProto != null && ((!MainPlugin.DefaultBeltsToMinutes.Value && Input.GetKey(MainPlugin.HotkeyBeltSpeeds.Value)) 
                || (MainPlugin.DefaultBeltsToMinutes.Value && !Input.GetKey(MainPlugin.HotkeyBeltSpeeds.Value)))) {

                // Find and replace belt speed value.
                if (itemProto.prefabDesc.isBelt) {
                    StringBuilder sb = new StringBuilder("         ", 12);
                    String origText = itemProto.GetPropValue(1, sb, 0);
                    String perMinute = ((double)itemProto.prefabDesc.beltSpeed * 60.0 / 10.0 * 60).ToString("0.##") + "/min";
                    ref Text valueText = ref AccessTools.FieldRefAccess<UIItemTip, Text>(__instance, "valuesText");
                    valueText.text = valueText.text.Replace(origText, perMinute);
                }

                // Find and replace sorter cycle value.
                else if (itemProto.prefabDesc.isInserter) {
                    StringBuilder sb = new StringBuilder("         ", 12);
                    String origText = itemProto.GetPropValue(1, sb, 0);
                    String perMinute = ((300000.0 / (double)itemProto.prefabDesc.inserterSTT) * 60).ToString("0.##") + " trip/min/grid";
                    ref Text valueText = ref AccessTools.FieldRefAccess<UIItemTip, Text>(__instance, "valuesText");
                    valueText.text = valueText.text.Replace(origText, perMinute);
                }
            }
        }
        static bool IsBuilding(ItemProto targetProto) {
            return targetProto.prefabDesc.hasBuildCollider;
        }
        static int GetRecipeProto(int recipeId) {
            switch (recipeId) {
                case 16: return 1114; // Plasma Refining (Refined Oil)
                case 29: return 1126; // Casimir Crystal (Advanced)
                case 32: return 1123; // Graphene (Advanced)
                case 35: return 1123; // Carbon Nanotube (Advanced)
                case 54: return 1117; // Organic Crystal (Original)
                case 58: return 1120; // X-ray Cracking (Hydrogen)
                case 61: return 1112; // Diamond (Advanced)
                case 62: return 1113; // Crystal Silicon (Advanced)
                case 69: return 1404; // Photon Combiner (Advanced)
                case 74: return 1122; // Mass-Energy Storage (Antimatter)
                case 100: return 1206; // Particle Container (Advanced)
                case 115: return 1121; // Fractionation (Deuterium)
                case 121: return 1114; // Reformed Refinement (Refined Oil)
                default: return 0;
            }
        }
        static int GetEntriesNum(ERecipeType rType) {
            switch (rType) {
                case ERecipeType.Assemble: return 4;
                case ERecipeType.Smelt: return 3;
                case ERecipeType.Chemical: return 2;
                case ERecipeType.Research: return 2;
                case ERecipeType.Fractionate: return 0;
                default: return 1;
            }
        }
        static string GetFacilityGen(int facTier) {
            switch (facTier) {
                case 0: return "60 s";
                case 1: return "Mk.I";
                case 2: return "Mk.II";
                case 3: return "Mk.III";
                case 4: return "Mk.IV";
                default: return "?";
            }
        }
        static void SetRecipePerMin(UIRecipeEntry uiRecipeEntry, RecipeProto recipeProto, int facilityTier = 0) {
            int uiOffset = 0;
            int entryNum = 0;
            int outputNum = 0;
            int inputNum = 0;
            double buildSpeed = -1;

            // Clamp recipe boundaries.
            if (uiRecipeEntry.timeText.rectTransform.sizeDelta.y > 37) {
                uiRecipeEntry.timeText.rectTransform.sizeDelta = new Vector2(uiRecipeEntry.timeText.rectTransform.sizeDelta.x, 37);
            }

            // Update recipe time text based on facility tier.
            uiRecipeEntry.timeText.text = GetFacilityGen(0);

            // Get relevant production speed multiplier and tier text.
            switch (recipeProto.Type) {
                case ERecipeType.Assemble:
                    buildSpeed = MainPlugin.SpeedAssembler[Mathf.Clamp(facilityTier, 1, MainPlugin.SpeedAssembler.Count)];
                    uiRecipeEntry.timeText.text = GetFacilityGen(facilityTier);
                    break;
                case ERecipeType.Smelt:
                    buildSpeed = MainPlugin.SpeedSmelter[Mathf.Clamp(facilityTier, 1, MainPlugin.SpeedSmelter.Count)];
                    uiRecipeEntry.timeText.text = GetFacilityGen(facilityTier);
                    break;
                case ERecipeType.Chemical:
                    buildSpeed = MainPlugin.SpeedChemical[Mathf.Clamp(facilityTier, 1, MainPlugin.SpeedChemical.Count)];
                    uiRecipeEntry.timeText.text = GetFacilityGen(facilityTier);
                    break;
                case ERecipeType.Research:
                    buildSpeed = MainPlugin.SpeedLaboratory[Mathf.Clamp(facilityTier, 1, MainPlugin.SpeedLaboratory.Count)];
                    uiRecipeEntry.timeText.text = GetFacilityGen(facilityTier);
                    break;
                default: buildSpeed = 1.0; break;
            }

            // Add recipe output items.
            while (outputNum < recipeProto.Results.Length && entryNum < 7) {
                ItemProto outputProto = LDB.items.Select(recipeProto.Results[outputNum]);

                if (outputProto != null) uiRecipeEntry.icons[entryNum].sprite = outputProto.iconSprite; 
                else uiRecipeEntry.icons[entryNum].sprite = null;

                uiRecipeEntry.countTexts[entryNum].text = (60f / (float)recipeProto.TimeSpend * 60f * recipeProto.ResultCounts[outputNum] * buildSpeed).ToString();
                uiRecipeEntry.icons[entryNum].rectTransform.anchoredPosition = new Vector2((float)uiOffset, 0f);
                uiRecipeEntry.icons[entryNum].gameObject.SetActive(true);
                uiOffset += 40;
                outputNum++;
                entryNum++;
            }

            // Add production arrow.
            uiRecipeEntry.arrow.anchoredPosition = new Vector2((float)uiOffset, -27f);
            uiOffset += 40;

            // Add recipe input items.
            while (inputNum < recipeProto.Items.Length && entryNum < 7) {
                ItemProto inputProto = LDB.items.Select(recipeProto.Items[inputNum]);

                if (inputProto != null) uiRecipeEntry.icons[entryNum].sprite = inputProto.iconSprite;
                else uiRecipeEntry.icons[entryNum].sprite = null;

                uiRecipeEntry.countTexts[entryNum].text = (60f / (float)recipeProto.TimeSpend * 60f * recipeProto.ItemCounts[inputNum] * buildSpeed).ToString();
                uiRecipeEntry.icons[entryNum].rectTransform.anchoredPosition = new Vector2((float)uiOffset, 0f);
                uiRecipeEntry.icons[entryNum].gameObject.SetActive(true);
                uiOffset += 40;
                inputNum++;
                entryNum++;
            }

            // Make item icons non-interactive.
            for (int i = entryNum; i < 7; i++) {
                uiRecipeEntry.icons[i].gameObject.SetActive(false);
            }
        }
    }

    [HarmonyPatch(typeof(UIItemTip))]
    [HarmonyPatch("OnDisable")]
    static class Patch_UIItemTip_OnDisable {
        static void Postfix(UIItemTip __instance) {
            ref UIRecipeEntry[] recipeEntryArr = ref AccessTools.FieldRefAccess<UIItemTip, UIRecipeEntry[]>(__instance, "recipeEntryArr");

            // Disable recipe entries, once UI is hidden.
            if (recipeEntryArr != null) {
                for (int recipeEntryNum = 0; recipeEntryNum < recipeEntryArr.Length; recipeEntryNum++) {
                    if (recipeEntryArr[recipeEntryNum] != null) {
                        recipeEntryArr[recipeEntryNum].gameObject.SetActive(false);
                    }
                }
            }
        }
    }
}