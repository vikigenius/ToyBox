﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.EntitySystem.Entities;
//using Kingmaker.UI.LevelUp.Phase;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.Utility;
using UnityEngine.SceneManagement;
using static ModKit.Utility.ReflectionCache;
using UnityModManager = UnityModManagerNet.UnityModManager;
using ModKit;
using Kingmaker.UnitLogic.ActivatableAbilities.Restrictions;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Kingdom.Settlements.BuildingComponents;

namespace ToyBox.Multiclass {
    public enum ProgressionPolicy {
        PrimaryClass = 0,
        Average = 1,
        Largest = 2,
        Sum = 3,
    };
    public class Mod {
        //public HashSet<Type> AbilityCasterCheckerTypes { get; } =
        //    new HashSet<Type>(Assembly.GetAssembly(typeof(IAbilityCasterChecker)).GetTypes()
        //        .Where(type => typeof(IAbilityCasterChecker).IsAssignableFrom(type) && !type.IsInterface));

        public HashSet<Type> ActivatableAbilityRestrictionTypes { get; } =
            new HashSet<Type>(Assembly.GetAssembly(typeof(ActivatableAbilityRestriction)).GetTypes()
                .Where(type => type.IsSubclassOf(typeof(ActivatableAbilityRestriction))));

        public HashSet<Type> BuildingRestrictionTypes { get; } =
            new HashSet<Type>(Assembly.GetAssembly(typeof(BuildingRestriction)).GetTypes()
                .Where(type => type.IsSubclassOf(typeof(BuildingRestriction))).Except(new[] { typeof(DLCRestriction) }));

        public HashSet<Type> EquipmentRestrictionTypes { get; } =
            new HashSet<Type>(Assembly.GetAssembly(typeof(EquipmentRestriction)).GetTypes()
                .Where(type => type.IsSubclassOf(typeof(EquipmentRestriction))));

        public HashSet<Type> PrerequisiteTypes { get; } =
            new HashSet<Type>(Assembly.GetAssembly(typeof(Prerequisite)).GetTypes()
                .Where(type => type.IsSubclassOf(typeof(Prerequisite))));

        public HashSet<BlueprintCharacterClass> AppliedMulticlassSet { get; internal set; }
            = new HashSet<BlueprintCharacterClass>();

        public HashSet<BlueprintProgression> UpdatedProgressions { get; internal set; }
            = new HashSet<BlueprintProgression>();

        public LevelUpController LevelUpController { get; internal set; }

        public HashSet<MethodBase> LockedPatchedMethods { get; internal set; } = new HashSet<MethodBase>();

        public bool IsLevelLocked { get; internal set; }

        public Scene ActiveScene => SceneManager.GetActiveScene();

        public BlueprintCharacterClass[] CharacterClasses => Game.Instance.BlueprintRoot.Progression.CharacterClasses.ToArray();

        public BlueprintCharacterClass[] MythicClasses => Game.Instance.BlueprintRoot.Progression.CharacterMythics.ToArray();

        public BlueprintCharacterClass[] AllClasses => CharacterClasses.Concat(MythicClasses).ToArray();

        public BlueprintScriptableObject LibraryObject => typeof(ResourcesLibrary).GetFieldValue<BlueprintScriptableObject>("s_LoadedResources");//("s_LibraryObject");

        public Player Player => Game.Instance.Player;
        //public static bool IsCharGen() => !Main.IsInGame && Game.Instance.LevelUpController.State.Mode == CharBuildMode.CharGen;
    }

    public static class MulticlassUtils {
        public static Settings settings = Main.settings;
        public static Player player = Game.Instance.Player;
        public static UnityModManager.ModEntry.ModLogger modLogger = ModKit.Logger.modLogger;

        public static bool IsCharGen(this LevelUpState state) {
            return state.Mode == LevelUpState.CharBuildMode.CharGen || state.Unit.CharacterName == "Player Character";
        }

        public static bool IsLevelUp(this LevelUpState state) {
            return state.Mode == LevelUpState.CharBuildMode.LevelUp;
        }

        public static bool IsPreGen(this LevelUpState state) {
            return state.IsPregen;
        }
        static public bool IsClassGestalt(this UnitEntityData ch, BlueprintCharacterClass cl) {
            if (ch.HashKey() == null) return false;
            var excludeSet = Main.settings.excludeClassesFromCharLevelSets.GetValueOrDefault(ch.HashKey(), new HashSet<string>());
            return excludeSet.Contains(cl.AssetGuid.ToString());
        }

        static public void SetClassIsGestalt(this UnitEntityData ch, BlueprintCharacterClass cl, bool isGestalt) {
            if (ch.HashKey() == null) return;
            var classID = cl.AssetGuid.ToString();
            var excludeSet = Main.settings.excludeClassesFromCharLevelSets.GetValueOrDefault(ch.HashKey(), new HashSet<string>());
            if (isGestalt) excludeSet.Add(classID);
            else excludeSet.Remove(classID);
            modLogger.Log($"Set - key: {classID} -> {isGestalt} excludeSet: ({String.Join(" ", excludeSet.ToArray())})");
            Main.settings.excludeClassesFromCharLevelSets[ch.HashKey()] = excludeSet;
        }

        static public bool IsClassGestalt(this UnitDescriptor ch, BlueprintCharacterClass cl) {
            if (ch.HashKey() == null) return false;
            var excludeSet = Main.settings.excludeClassesFromCharLevelSets.GetValueOrDefault(ch.HashKey(), new HashSet<string>());
            return excludeSet.Contains(cl.AssetGuid.ToString());
        }

        static public void SetClassIsGestalt(this UnitDescriptor ch, BlueprintCharacterClass cl, bool exclude) {
            if (ch.HashKey() == null) return;
            var classID = cl.AssetGuid.ToString();
            var excludeSet = Main.settings.excludeClassesFromCharLevelSets.GetValueOrDefault(ch.HashKey(), new HashSet<string>());
            if (exclude) excludeSet.Add(classID);
            else excludeSet.Remove(classID);
            // modLogger.Log($"Set - key: {classID} -> {exclude} excludeSet: ({String.Join(" ", excludeSet.ToArray())})");
            Main.settings.excludeClassesFromCharLevelSets[ch.HashKey()] = excludeSet;
        }
        static public bool IsClassGestalt(this UnitProgressionData progression, BlueprintCharacterClass cl) {
            var chars = Game.Instance.Player.AllCharacters;
            foreach (var ch in chars) {
                //Main.Log($"   {ch.Progression.Owner} vs { progression.Owner}");
                if (ch.Progression.Owner == progression.Owner) {
                    modLogger.Log($"   found: {ch.HashKey()} - {ch.Progression.Owner}");
                    return ch.IsClassGestalt(cl);
                }
            }
            return false;
        }
    }
}
