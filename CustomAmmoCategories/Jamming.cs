﻿using BattleTech;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustAmmoCategories;
using Random = UnityEngine.Random;
using UnityEngine;

//This part of code is modified code of original WeaponRealizer by Joel Meador under MIT LICENSE

namespace CustAmmoCategories {
  public static class StatisticHelper {
    public static Statistic GetOrCreateStatisic<StatisticType>(StatCollection collection, string statName, StatisticType defaultValue) {
      Statistic statistic = collection.GetStatistic(statName);
      if (statistic == null) {
        statistic = collection.AddStatistic<StatisticType>(statName, defaultValue);
      }
      return statistic;
    }
  }
  [HarmonyPatch(typeof(AbstractActor), "OnActivationEnd", MethodType.Normal)]
  public static class unJammingEnabler {
    public static bool Prefix(AbstractActor __instance) {
      var actor = __instance;
      foreach (Weapon weapon in actor.Weapons) {
        if (weapon.roundsSinceLastFire <= 0) { continue; };
        if (CustomAmmoCategories.IsCooldown(weapon) > 0) {
          var removedJam = CustomAmmoCategories.AttemptToRemoveCooldown(actor, weapon);
          CustomAmmoCategoriesLog.Log.LogWrite($"Removed cooldown? {removedJam}\n");
        }
      }
      if (actor.IsShutDown) return true;

      foreach (Weapon weapon in actor.Weapons) {
        if (weapon.roundsSinceLastFire <= 0) { continue; };
        if (CustomAmmoCategories.IsJammed(weapon)) {
          var removedJam = CustomAmmoCategories.AttemptToRemoveJam(actor, weapon);
          CustomAmmoCategoriesLog.Log.LogWrite($"Removed Jam? {removedJam}\n");
        }
      }
      return true;
    }
  }
  [HarmonyPatch(typeof(AttackDirector))]
  [HarmonyPatch("OnAttackComplete")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(MessageCenterMessage) })]
  public static class JammingEnabler {
    private static Queue<AbstractActor> jammQueue = new Queue<AbstractActor>();
    public static bool Prefix(AttackDirector __instance, MessageCenterMessage message) {
      AttackCompleteMessage attackCompleteMessage = (AttackCompleteMessage)message;
      int sequenceId = attackCompleteMessage.sequenceId;
      AttackDirector.AttackSequence attackSequence = __instance.GetAttackSequence(sequenceId);
      if (attackSequence == null) {
        return true;
      }
      CustomAmmoCategoriesLog.Log.LogWrite($"Try jamm weapon of " + attackSequence.attacker.DisplayName + "\n");
      JammingEnabler.jammQueue.Enqueue(attackSequence.attacker);
      return true;
    }
    public static void Postfix(AttackDirector __instance, MessageCenterMessage message) {
      CustomAmmoCategoriesLog.Log.LogWrite($"Try jamm weapon\n");
      while (JammingEnabler.jammQueue.Count > 0) {
        AbstractActor actor = JammingEnabler.jammQueue.Dequeue();
        CustomAmmoCategoriesLog.Log.LogWrite($"Try jamm weapon of " + actor.DisplayName + "\n");
        foreach (Weapon weapon in actor.Weapons) {
          CustomAmmoCategoriesLog.Log.LogWrite($"  weapon " + weapon.UIName + " rounds since last fire "+ weapon.roundsSinceLastFire + "\n");
          if (weapon.roundsSinceLastFire > 0) { continue; }
          float flatJammingChance = CustomAmmoCategories.getWeaponFlatJammingChance(weapon);
          CustomAmmoCategoriesLog.Log.LogWrite($"  flatJammingChance " +flatJammingChance + "\n");
          if (flatJammingChance > CustomAmmoCategories.Epsilon) {
            CustomAmmoCategoriesLog.Log.LogWrite($"  Try jamm weapon " + weapon.UIName + "\n");
            float Roll = Random.Range(0.0f, 1.0f);
            CustomAmmoCategoriesLog.Log.LogWrite($" Jamming chance " + flatJammingChance + " roll " + Roll + "\n");
            if (Roll < flatJammingChance) {
              CustomAmmoCategoriesLog.Log.LogWrite($" Jammed!\n");
              CustomAmmoCategories.AddJam(actor, weapon);
            }
          }
          if(CustomAmmoCategories.getWeaponCooldown(weapon) > 0) {
            CustomAmmoCategories.AddCooldown(actor, weapon);
          }
        }
      }
    }
  }


  public static partial class CustomAmmoCategories {
    public static string JammedWeaponStatisticName = "CAC-JammedWeapon";
    public static string CooldownWeaponStatisticName = "CAC-CooldownWeapon";
    public static string TemporarilyDisabledStatisticName = "TemporarilyDisabled";
    public static float Epsilon = 0.001f;
    public static void AddJam(AbstractActor actor, Weapon weapon) {
      if (CustomAmmoCategories.getWeaponDamageOnJamming(weapon) == false) {
        if (CustomAmmoCategories.checkExistance(weapon.StatCollection, CustomAmmoCategories.JammedWeaponStatisticName) == false) {
          weapon.StatCollection.AddStatistic<bool>(CustomAmmoCategories.JammedWeaponStatisticName, false);
        }
        weapon.StatCollection.Set<bool>(CustomAmmoCategories.JammedWeaponStatisticName, true);
        weapon.StatCollection.Set<bool>(CustomAmmoCategories.TemporarilyDisabledStatisticName, true);
        actor.Combat.MessageCenter.PublishMessage(
            new AddSequenceToStackMessage(
                new ShowActorInfoSequence(actor, $"{weapon.Name} Jammed!", FloatieMessage.MessageNature.Debuff,
                    true)));
      }else {
        var isDestroying = weapon.DamageLevel != ComponentDamageLevel.Functional;
        var damageLevel = isDestroying ? ComponentDamageLevel.Destroyed : ComponentDamageLevel.Penalized;
        var fakeHit = new WeaponHitInfo(-1, -1, -1, -1, string.Empty, string.Empty, -1, null, null, null, null, null, null, null, AttackDirection.None, Vector2.zero, null);
        weapon.DamageComponent(fakeHit, damageLevel, true);
        var message = isDestroying
            ? $"{weapon.Name} misfire: Destroyed!"
            : $"{weapon.Name} misfire: Damaged!";
        actor.Combat.MessageCenter.PublishMessage(
            new AddSequenceToStackMessage(
                new ShowActorInfoSequence(actor, message, FloatieMessage.MessageNature.Debuff, true)));
      }
    }
    public static void AddCooldown(AbstractActor actor, Weapon weapon) {
      if (CustomAmmoCategories.checkExistance(weapon.StatCollection, CustomAmmoCategories.CooldownWeaponStatisticName) == false) {
        weapon.StatCollection.AddStatistic<int>(CustomAmmoCategories.CooldownWeaponStatisticName, 0);
      }
      if (CustomAmmoCategories.getWeaponCooldown(weapon) > 0) {
        weapon.StatCollection.Set<int>(CustomAmmoCategories.CooldownWeaponStatisticName, CustomAmmoCategories.getWeaponCooldown(weapon));
        weapon.StatCollection.Set<bool>(CustomAmmoCategories.TemporarilyDisabledStatisticName, true);
        actor.Combat.MessageCenter.PublishMessage(
            new AddSequenceToStackMessage(
                new ShowActorInfoSequence(actor, $"{weapon.Name} Cooldown!", FloatieMessage.MessageNature.Debuff,
                    true)));
      }
    }
    public static bool IsJammed(Weapon weapon) {
      var statistic = StatisticHelper.GetOrCreateStatisic<bool>(weapon.StatCollection, JammedWeaponStatisticName, false);
      return statistic.Value<bool>();
    }
    public static int IsCooldown(Weapon weapon) {
      var statistic = StatisticHelper.GetOrCreateStatisic<int>(weapon.StatCollection, CooldownWeaponStatisticName, 0);
      return statistic.Value<int>();
    }
    public static float getWeaponFlatJammingChance(Weapon weapon) {
      float result = 0;
      float mult = 0;
      float baseval = 0f;
      if(CustomAmmoCategories.checkExistance(weapon.StatCollection,CustomAmmoCategories.AmmoIdStatName) == true) {
        string ammoId = weapon.StatCollection.GetStatistic(CustomAmmoCategories.AmmoIdStatName).Value<string>();
        ExtAmmunitionDef extAmmo = CustomAmmoCategories.findExtAmmo(ammoId);
        result += extAmmo.FlatJammingChance;
        mult += extAmmo.GunneryJammingMult;
        if (extAmmo.GunneryJammingBase > 0) { baseval = extAmmo.GunneryJammingBase; };
      }
      ExtWeaponDef extWeapon = CustomAmmoCategories.getExtWeaponDef(weapon.defId);
      result += extWeapon.FlatJammingChance;
      mult += extWeapon.GunneryJammingMult;
      if ((extWeapon.GunneryJammingBase > 0)&&(baseval == 0f)) { baseval = extWeapon.GunneryJammingBase; };
      if (extWeapon.Modes.Count > 0) {
        string modeId = "";
        if (CustomAmmoCategories.checkExistance(weapon.StatCollection, CustomAmmoCategories.WeaponModeStatisticName) == true) {
          modeId = weapon.StatCollection.GetStatistic(CustomAmmoCategories.WeaponModeStatisticName).Value<string>();
        }else {
          modeId = extWeapon.baseModeId;
        }
        if (extWeapon.Modes.ContainsKey(modeId)) {
          result += extWeapon.Modes[modeId].FlatJammingChance;
          mult += extWeapon.Modes[modeId].GunneryJammingMult;
          if ((extWeapon.Modes[modeId].GunneryJammingBase > 0) && (baseval == 0f)) { baseval = extWeapon.Modes[modeId].GunneryJammingBase; };
        }
      }
      if(weapon.parent != null) {
        if (baseval == 0f) { baseval = 5f; }
        result += ((baseval - weapon.parent.SkillGunnery) * mult);
      }
      return result;
    }
    public static bool getWeaponDamageOnJamming(Weapon weapon) {
      TripleBoolean result = TripleBoolean.NotSet;
      ExtWeaponDef extWeapon = CustomAmmoCategories.getExtWeaponDef(weapon.defId);
      if (extWeapon.Modes.Count > 0) {
        string modeId = "";
        if (CustomAmmoCategories.checkExistance(weapon.StatCollection, CustomAmmoCategories.WeaponModeStatisticName) == true) {
          modeId = weapon.StatCollection.GetStatistic(CustomAmmoCategories.WeaponModeStatisticName).Value<string>();
        } else {
          modeId = extWeapon.baseModeId;
        }
        if (extWeapon.Modes.ContainsKey(modeId)) {
          result = extWeapon.Modes[modeId].DamageOnJamming;
        }
      }
      if (result == TripleBoolean.NotSet) {
        result = extWeapon.DamageOnJamming;
      }
      return result == TripleBoolean.True;
    }

    public static int getWeaponCooldown(Weapon weapon) {
      return CustomAmmoCategories.getWeaponMode(weapon).Cooldown;
    }
    public static bool AttemptToRemoveJam(AbstractActor actor, Weapon weapon) {
      var skill = actor.SkillGunnery;
      var mitigationRoll = Random.Range(1, 10);
      StringBuilder sb = new StringBuilder();
      sb.AppendLine($"gunneryskill: {skill}");
      sb.AppendLine($"mitigationRoll: {mitigationRoll}");
      if (skill >= mitigationRoll) {
        CustomAmmoCategoriesLog.Log.LogWrite(sb.ToString()+"\n");
        RemoveJam(actor, weapon);
        return true;
      }

      CustomAmmoCategoriesLog.Log.LogWrite(sb.ToString()+"\n");
      return false;
    }
    public static bool AttemptToRemoveCooldown(AbstractActor actor, Weapon weapon) {
      int cooldown = weapon.StatCollection.GetStatistic(CustomAmmoCategories.CooldownWeaponStatisticName).Value<int>();
      if(cooldown <= 1) {
        weapon.StatCollection.Set<int>(CustomAmmoCategories.CooldownWeaponStatisticName, 0);
        weapon.StatCollection.Set<bool>(TemporarilyDisabledStatisticName, false);
        CustomAmmoCategoriesLog.Log.LogWrite($" Weapon " + weapon.UIName + " - operational\n");
        actor.Combat.MessageCenter.PublishMessage(
            new AddSequenceToStackMessage(
                new ShowActorInfoSequence(actor, $"{weapon.Name} Ready!", FloatieMessage.MessageNature.Buff,
                    true)));
        return true;
      } else {
        weapon.StatCollection.Set<int>(CustomAmmoCategories.CooldownWeaponStatisticName, cooldown-1);
        weapon.StatCollection.Set<bool>(TemporarilyDisabledStatisticName, true);
        CustomAmmoCategoriesLog.Log.LogWrite($" Weapon " + weapon.UIName + " - cooldown "+(cooldown-1)+"\n");
        return false;
      }
    }
    private static void RemoveJam(AbstractActor actor, Weapon weapon) {
      weapon.StatCollection.Set<bool>(JammedWeaponStatisticName, false);
      weapon.StatCollection.Set<bool>(TemporarilyDisabledStatisticName, false);
      actor.Combat.MessageCenter.PublishMessage(
          new AddSequenceToStackMessage(
              new ShowActorInfoSequence(actor, $"{weapon.Name} Unjammed!", FloatieMessage.MessageNature.Buff,
                  true)));
    }
  }
}
