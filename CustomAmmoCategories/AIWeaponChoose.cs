﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Harmony;
using BattleTech;
using BattleTech.AttackDirectorHelpers;
using System.Reflection;
using CustAmmoCategories;
using UnityEngine;
using System.Diagnostics;

namespace CustAmmoCategories {
  public class AmmoModePair {
    public string ammoId { get; set; }
    public string modeId { get; set; }
    public AmmoModePair() {
      ammoId = "";
      modeId = "";
    }
    public AmmoModePair(string ammo,string mode) {
      ammoId = ammo;
      modeId = mode;
    }
    public override bool Equals(object o) {
      if (o == null) { return false; }
      if (o is AmmoModePair) {
        return (this.ammoId == (o as AmmoModePair).ammoId) && (this.modeId == (o as AmmoModePair).modeId);
      }
      return false;
    }
    public override int GetHashCode() {
      return (this.ammoId+"/"+this.modeId).GetHashCode();
    }
    public static bool operator ==(AmmoModePair a, AmmoModePair b) {
      if (((object)a == null) && ((object)b == null)) { return true; };
      if ((object)a == null) { return false; };
      if ((object)b == null) { return false; };
      return (a.ammoId == b.ammoId)&&(a.modeId == b.modeId);
    }
    public static bool operator !=(AmmoModePair a, AmmoModePair b) {
      if (((object)a == null) && ((object)b == null)) { return false; };
      if ((object)a == null) { return true; };
      if ((object)b == null) { return true; };
      return (a.ammoId != b.ammoId) || (a.modeId != b.modeId);
    }
  }
  public class DamagePredictRecord {
    public AmmoModePair Id { get; set; }
    public float HeatDamageCoeff { get; set; }
    public float PredictHeatDamage { get; set; }
    public float NormDamageCoeff { get; set; }
    public DamagePredictRecord() {
      Id = new AmmoModePair();
      HeatDamageCoeff = 0;
      NormDamageCoeff = 0;
      PredictHeatDamage = 0;
    }
    public DamagePredictRecord(string ammo, string mode) {
      Id = new AmmoModePair(ammo, mode);
      HeatDamageCoeff = 0;
      NormDamageCoeff = 0;
      PredictHeatDamage = 0;
    }
  }
  public static partial class CustomAmmoCategories {
    public static void applyWeaponAmmoMode(Weapon weapon,string modeId,string ammoId) {
      ExtWeaponDef extWeapon = CustomAmmoCategories.getExtWeaponDef(weapon.defId);
      CustomAmmoCategoriesLog.Log.LogWrite("applyWeaponAmmoMode("+weapon.defId+","+modeId+","+ammoId+")\n");
      if (extWeapon.Modes.ContainsKey(modeId)) {
        if(CustomAmmoCategories.checkExistance(weapon.StatCollection,CustomAmmoCategories.WeaponModeStatisticName) == false) {
          weapon.StatCollection.AddStatistic<string>(CustomAmmoCategories.WeaponModeStatisticName,modeId);
        } else {
          weapon.StatCollection.Set<string>(CustomAmmoCategories.WeaponModeStatisticName, modeId);
        }
      } else {
        CustomAmmoCategoriesLog.Log.LogWrite("WARNING! "+weapon.defId+" has no mode "+modeId+"\n",true);
      }
      if (CustomAmmoCategories.checkExistance(weapon.StatCollection, CustomAmmoCategories.AmmoIdStatName) == false) {
        weapon.StatCollection.AddStatistic<string>(CustomAmmoCategories.AmmoIdStatName, ammoId);
      } else {
        weapon.StatCollection.Set<string>(CustomAmmoCategories.AmmoIdStatName, ammoId);
      }
    }
    public static HashSet<string> getWeaponAvaibleAmmoForMode(Weapon weapon, string modeId) {
      HashSet<string> result = new HashSet<string>();
      CustomAmmoCategory ammoCategory = CustomAmmoCategories.find(weapon.AmmoCategory.ToString());
      ExtWeaponDef extWeapon = CustomAmmoCategories.getExtWeaponDef(weapon.defId);
      if (extWeapon.AmmoCategory.BaseCategory == weapon.AmmoCategory) { ammoCategory = extWeapon.AmmoCategory; }
      if (extWeapon.Modes.Count < 1) {
        CustomAmmoCategoriesLog.Log.LogWrite("WARNING! " + weapon.defId + " has no modes. Even base mode. This means something is very very wrong\n", true);
        return result;
      }
      if(extWeapon.Modes.ContainsKey(modeId) == false) {
        CustomAmmoCategoriesLog.Log.LogWrite("WARNING! " + weapon.defId + " has no mode "+modeId+".\n", true);
        return result;
      }
      WeaponMode weaponMode = extWeapon.Modes[modeId];
      if (weaponMode.AmmoCategory.Index != ammoCategory.Index) { ammoCategory = weaponMode.AmmoCategory; };
      if (ammoCategory.Index == CustomAmmoCategories.NotSetCustomAmmoCategoty.Index) { result.Add(""); return result; };
      foreach (AmmunitionBox box in weapon.ammoBoxes) {
        if (box.IsFunctional == false) { continue; }
        if (box.CurrentAmmo <= 0) { continue; }
        CustomAmmoCategory boxAmmoCategory = CustomAmmoCategories.getAmmoAmmoCategory(box.ammoDef);
        if (boxAmmoCategory.Index == ammoCategory.Index) {
          if (result.Contains(box.ammoDef.Description.Id) == false) {
            result.Add(box.ammoDef.Description.Id);
          }
        }
      }
      return result;
    }
    public static List<DamagePredictRecord> getWeaponDamagePredict(AbstractActor unit,ICombatant target, Weapon weapon) {
      List<DamagePredictRecord> result = new List<DamagePredictRecord>();
      ExtWeaponDef extWeapon = CustomAmmoCategories.getExtWeaponDef(weapon.defId);
      if(extWeapon.Modes.Count < 1) {
        CustomAmmoCategoriesLog.Log.LogWrite("WARNING! " + weapon.defId + " has no modes. Even base mode. This means something is very very wrong\n",true);
        return result;
      }
      string currentMode = extWeapon.baseModeId;
      if (CustomAmmoCategories.checkExistance(weapon.StatCollection, CustomAmmoCategories.WeaponModeStatisticName) == true) {
        currentMode = weapon.StatCollection.GetStatistic(CustomAmmoCategories.WeaponModeStatisticName).Value<string>();
      }
      string currentAmmo = "";
      if (CustomAmmoCategories.checkExistance(weapon.StatCollection, CustomAmmoCategories.AmmoIdStatName) == true) {
        currentAmmo = weapon.StatCollection.GetStatistic(CustomAmmoCategories.AmmoIdStatName).Value<string>();
      }
      foreach (var mode in extWeapon.Modes) {
        HashSet<string> ammos = CustomAmmoCategories.getWeaponAvaibleAmmoForMode(weapon,mode.Value.Id);
        List<int> hitLocations = null;
        float AverageArmor = float.NaN;
        foreach (var ammo in ammos) {
          DamagePredictRecord record = new DamagePredictRecord(ammo, mode.Value.Id);
          CustomAmmoCategories.fillWeaponPredictRecord(ref record,unit,target,weapon,ref hitLocations,ref AverageArmor);
          result.Add(record);
        }
      }
      CustomAmmoCategories.applyWeaponAmmoMode(weapon,currentMode,currentAmmo);
      return result;
    }

    public static int getWeaponPierceLocations(List<int> hitLocations, ICombatant target,float DamagePerShot) {
      int result = 0;
      CustomAmmoCategoriesLog.Log.LogWrite("getWeaponPierceLocations " + target.DisplayName + " : "+DamagePerShot+"\n");
      foreach (int hitLocation in hitLocations) {
        if(target.ArmorForLocation(hitLocation) <= DamagePerShot) {
          CustomAmmoCategoriesLog.Log.LogWrite(" location "+hitLocation+" pierced\n");
          ++result;
        }
      }
      return result;
    }
    public static float getTargetAvarageArmor(List<int> hitLocations, ICombatant target) {
      CustomAmmoCategoriesLog.Log.LogWrite("getTargetAvarageArmor "+target.DisplayName+"\n");
      float result = 0.0f;
      foreach(int hitLocation in hitLocations) {
        CustomAmmoCategoriesLog.Log.LogWrite(" location "+hitLocation+" : "+ target.ArmorForLocation(hitLocation) + "\n");
        result += target.ArmorForLocation(hitLocation);
      }
      if(hitLocations.Count > 0) {
        result /= (float)hitLocations.Count;
      } else {
        result = 1.0f;
      }
      if (result < Epsilon) { result = 1.0f; }
      return result;
    }

    public static void fillWeaponPredictRecord(ref DamagePredictRecord record, AbstractActor unit, ICombatant target, Weapon weapon,ref List<int> hitLocations, ref float AverageArmor) {
      CustomAmmoCategoriesLog.Log.LogWrite("fillWeaponPredictRecord "+unit.DisplayName+" target "+target.DisplayName+" weapon "+weapon.defId+"\n");
      CustomAmmoCategories.applyWeaponAmmoMode(weapon,record.Id.modeId,record.Id.ammoId);
      AbstractActor targetActor = target as AbstractActor;
      if(hitLocations == null) {
        hitLocations = target.GetPossibleHitLocations(unit);
        foreach (int hitLocation in hitLocations) {
          CustomAmmoCategoriesLog.Log.LogWrite("Hit Location "+hitLocation+"\n");
        }
      }
      if (float.IsNaN(AverageArmor)) {
        AverageArmor = CustomAmmoCategories.getTargetAvarageArmor(hitLocations, target);
      }
      float toHit = 0;
      if (weapon.WillFireAtTargetFromPosition(target,unit.CurrentPosition) == true) {
        toHit = weapon.GetToHitFromPosition(target, 1, unit.CurrentPosition, target.CurrentPosition, true, (targetActor != null) ? targetActor.IsEvasive : false, false);
      }
      if (toHit < Epsilon) { record.HeatDamageCoeff = 0f; record.NormDamageCoeff = 0f; record.PredictHeatDamage = 0f; };
      float coolDownCoeff = 1.0f / (1.0f + CustomAmmoCategories.getWeaponCooldown(weapon));
      float jammCoeff = 1.0f - CustomAmmoCategories.getWeaponFlatJammingChance(weapon);
      float damageJammCoeff = CustomAmmoCategories.getWeaponDamageOnJamming(weapon) ? 0.5f : 1.0f;
      float damageShotsCount = (float)weapon.ShotsWhenFired;
      float damagePerShot = weapon.DamagePerShot;
      float heatPerShot = weapon.HeatDamagePerShot;
      if (weapon.componentDef.ComponentTags.Contains("wr-clustered_shots")||(CustomAmmoCategories.getWeaponDisabledClustering(weapon) == false)){
        damageShotsCount *= (float)weapon.ProjectilesPerShot;
      }
      float piercedLocationsCount = (float)CustomAmmoCategories.getWeaponPierceLocations(hitLocations, target, damagePerShot);
      float hitLocationsCount = (hitLocations.Count > 0) ? (float)hitLocations.Count : 1.0f;
      float clusterCoeff = 1.0f + ((piercedLocationsCount / hitLocationsCount) * damageShotsCount)*CustomAmmoCategories.Settings.ClusterAIMult;
      float pierceCoeff = 1.0f;
      if(AverageArmor > damagePerShot) {
        pierceCoeff += (damagePerShot / AverageArmor) * CustomAmmoCategories.Settings.PenetrateAIMult;
      }
      record.NormDamageCoeff = damagePerShot * damageShotsCount * toHit * coolDownCoeff * jammCoeff * damageJammCoeff * clusterCoeff * pierceCoeff;
      record.HeatDamageCoeff = heatPerShot * damageShotsCount * toHit * jammCoeff * damageJammCoeff;
      record.PredictHeatDamage = heatPerShot * damageShotsCount * toHit;
      CustomAmmoCategoriesLog.Log.LogWrite(" toHit = " + toHit + "\n");
      CustomAmmoCategoriesLog.Log.LogWrite(" coolDownCoeff = "+ coolDownCoeff + "\n");
      CustomAmmoCategoriesLog.Log.LogWrite(" jammCoeff = " + jammCoeff + "\n");
      CustomAmmoCategoriesLog.Log.LogWrite(" damageJammCoeff = " + damageJammCoeff + "\n");
      CustomAmmoCategoriesLog.Log.LogWrite(" damageShotsCount = " + damageShotsCount + "\n");
      CustomAmmoCategoriesLog.Log.LogWrite(" damagePerShot = " + damagePerShot + "\n");
      CustomAmmoCategoriesLog.Log.LogWrite(" heatPerShot = " + heatPerShot + "\n");
      CustomAmmoCategoriesLog.Log.LogWrite(" piercedLocationsCount = " + piercedLocationsCount + "\n");
      CustomAmmoCategoriesLog.Log.LogWrite(" hitLocationsCount = " + hitLocationsCount + "\n");
      CustomAmmoCategoriesLog.Log.LogWrite(" AverageArmor = " + AverageArmor + "\n");
      CustomAmmoCategoriesLog.Log.LogWrite(" clusterCoeff = " + clusterCoeff + "\n");
      CustomAmmoCategoriesLog.Log.LogWrite(" pierceCoeff = " + pierceCoeff + "\n");
      CustomAmmoCategoriesLog.Log.LogWrite(" NormDamageCoeff = " + record.NormDamageCoeff + "\n");
      CustomAmmoCategoriesLog.Log.LogWrite(" HeatDamageCoeff = " + record.HeatDamageCoeff + "\n");
    }
    public static void ChooseBestWeaponForTarget(AbstractActor unit, ICombatant target, bool isStationary) {
      Stopwatch stopWatch = new Stopwatch();
      Dictionary<string, Weapon> weapons = new Dictionary<string, Weapon>();
      Dictionary<string, List<DamagePredictRecord>> damagePredict = new Dictionary<string, List<DamagePredictRecord>>();
      foreach (Weapon weapon in unit.Weapons) {
        weapons.Add(weapon.uid, weapon);
        damagePredict.Add(weapon.uid, CustomAmmoCategories.getWeaponDamagePredict(unit, target, weapon));
      }
      foreach (var weapon in weapons) {
        CustomAmmoCategoriesLog.Log.LogWrite("Weapon " + weapon.Key+" " + weapon.Value.defId + "\n");
        foreach (var fireType in damagePredict[weapon.Key]) {
          CustomAmmoCategoriesLog.Log.LogWrite(" mode:" + fireType.Id.modeId + " ammo:" + fireType.Id.ammoId + " heat:"+fireType.HeatDamageCoeff+" dmg:"+fireType.NormDamageCoeff+"\n");
        }
      }
      Mech targetMech = target as Mech;
      if (targetMech != null) {
        CustomAmmoCategoriesLog.Log.LogWrite("Try overheat\n");
        float overallPredictHeatDamage = 0f;
        Dictionary<string, int> weaponsWithHeatFireMode = new Dictionary<string, int>();
        foreach (var weapon in weapons) {
          if (damagePredict.ContainsKey(weapon.Key) == false) {
            CustomAmmoCategoriesLog.Log.LogWrite("WARNING! " + weapon.Value.defId + " has no predict damage record something is very very wrong\n", true);
            continue;
          }
          if (damagePredict[weapon.Key].Count <= 0) {
            CustomAmmoCategoriesLog.Log.LogWrite("WARNING! " + weapon.Value.defId + " has empty predict damage record something is very very wrong\n", true);
            continue;
          }
          float HeatDamageCoeff = damagePredict[weapon.Key][0].HeatDamageCoeff;
          int heatDamageIndex = 0;
          bool haveDiffHeatMode = false;
          for (int index = 1; index < damagePredict[weapon.Key].Count; ++index) {
            DamagePredictRecord fireMode = damagePredict[weapon.Key][index];
            if (HeatDamageCoeff < fireMode.HeatDamageCoeff) { HeatDamageCoeff = fireMode.HeatDamageCoeff; heatDamageIndex = index; haveDiffHeatMode = true; };
          }
          overallPredictHeatDamage += damagePredict[weapon.Key][heatDamageIndex].PredictHeatDamage;
          if (haveDiffHeatMode) {
            weaponsWithHeatFireMode.Add(weapon.Key, heatDamageIndex);
          }
        }
        CustomAmmoCategoriesLog.Log.LogWrite(" Current target heat:"+targetMech.CurrentHeat+" predicted:"+ overallPredictHeatDamage + "\n");
        if ((targetMech.CurrentHeat + overallPredictHeatDamage) > targetMech.OverheatLevel) {
          CustomAmmoCategoriesLog.Log.LogWrite(" worth it\n");
          foreach (var weapon in weaponsWithHeatFireMode) {
            CustomAmmoCategories.applyWeaponAmmoMode(weapons[weapon.Key], damagePredict[weapon.Key][weapon.Value].Id.modeId, damagePredict[weapon.Key][weapon.Value].Id.ammoId);
            weapons.Remove(weapon.Key);
            damagePredict.Remove(weapon.Key);
          }
        } else {
          CustomAmmoCategoriesLog.Log.LogWrite(" not worth it\n");
        }
      }
      CustomAmmoCategoriesLog.Log.LogWrite("Normal damage\n");
      foreach (var weapon in weapons) {
        CustomAmmoCategoriesLog.Log.LogWrite("Weapon " + weapon.Key + " " + weapon.Value.defId + "\n");
        foreach (var fireType in damagePredict[weapon.Key]) {
          CustomAmmoCategoriesLog.Log.LogWrite(" mode:" + fireType.Id.modeId + " ammo:" + fireType.Id.ammoId + " heat:" + fireType.HeatDamageCoeff + " dmg:" + fireType.NormDamageCoeff + "\n");
        }
      }
      foreach (var weapon in weapons) {
        if (damagePredict.ContainsKey(weapon.Key) == false) {
          CustomAmmoCategoriesLog.Log.LogWrite("WARNING! " + weapon.Value.defId + " has no predict damage record something is very very wrong\n", true);
          continue;
        }
        if (damagePredict[weapon.Key].Count <= 0) {
          CustomAmmoCategoriesLog.Log.LogWrite("WARNING! " + weapon.Value.defId + " has empty predict damage record something is very very wrong\n", true);
          continue;
        }
        float DamageCoeff = damagePredict[weapon.Key][0].HeatDamageCoeff;
        int DamageIndex = 0;
        for (int index = 1; index < damagePredict[weapon.Key].Count; ++index) {
          DamagePredictRecord fireMode = damagePredict[weapon.Key][index];
          if (DamageCoeff < fireMode.NormDamageCoeff) { DamageCoeff = fireMode.NormDamageCoeff; DamageIndex = index; };
        }
        CustomAmmoCategories.applyWeaponAmmoMode(weapons[weapon.Key], damagePredict[weapon.Key][DamageIndex].Id.modeId, damagePredict[weapon.Key][DamageIndex].Id.ammoId);
      }
    }
      /*public static bool isWeaponHasDiffirentAmmoModes(Weapon weapon) {
        ExtWeaponDef extWeapon = CustomAmmoCategories.getExtWeaponDef(weapon.defId);
        if (extWeapon.Modes.Count > 1) { return true; };
        if (weapon.ammoBoxes.Count == 0) { return false; }
        string ammoId = "";
        for (int index = 0; index < weapon.ammoBoxes.Count; ++index) {
          if (weapon.ammoBoxes[index].CurrentAmmo <= 0) { continue; }
          if (weapon.ammoBoxes[index].IsFunctional == false) { continue; }
          if (string.IsNullOrEmpty(ammoId)) { ammoId = weapon.ammoBoxes[index].ammoDef.Description.Id; continue; };
          if (ammoId != weapon.ammoBoxes[index].ammoDef.Description.Id) { return true; }
        }
        return false;
      }
      public static HashSet<string> getWeaponAvaibleAmmoForMode(Weapon weapon,string modeId) {
        CustomAmmoCategory ammoCategory = CustomAmmoCategories.find(weapon.AmmoCategory.ToString());
        ExtWeaponDef extWeapon = CustomAmmoCategories.getExtWeaponDef(weapon.defId);
        if(extWeapon.AmmoCategory.BaseCategory == weapon.AmmoCategory) { ammoCategory = extWeapon.AmmoCategory; }
        WeaponMode weaponMode = CustomAmmoCategories.getWeaponMode(weapon);
        if (weaponMode.AmmoCategory.Index != ammoCategory.Index) { ammoCategory = weaponMode.AmmoCategory; };
        HashSet<string> result = new HashSet<string>();
        if (ammoCategory.Index == CustomAmmoCategories.NotSetCustomAmmoCategoty.Index) { result.Add(""); return result; };
        foreach(AmmunitionBox box in weapon.ammoBoxes) {
          if (box.IsFunctional == false) { continue; }
          if (box.CurrentAmmo <= 0) { continue; }
          CustomAmmoCategory boxAmmoCategory = CustomAmmoCategories.getAmmoAmmoCategory(box.ammoDef);
          if (boxAmmoCategory.Index == ammoCategory.Index) { result.Add(box.ammoDef.Description.Id); }
        }
        return result;
      }
      public static float getWeaponPredictionToHeatModifier(WeaponDef weaponDef,string ammoId, string modeId) {
        float result = 0;

        return result;
      }
      public static bool isWeaponHasDiffirentModes(Weapon weapon) {
        return CustomAmmoCategories.getExtWeaponDef(weapon.defId).Modes.Count > 1;
      }
      public static bool isWeaponHasHeatAmmoMode(Weapon weapon) {
        if (weapon.ammoBoxes.Count == 0) { return false; }
        float HeatPerShot = 0;
        bool HeatSetted = false;
        for (int index = 0; index < weapon.ammoBoxes.Count; ++index) {
          if (weapon.ammoBoxes[index].CurrentAmmo <= 0) { continue; }
          if (weapon.ammoBoxes[index].IsFunctional == false) { continue; }
          ExtAmmunitionDef extAmmo = CustomAmmoCategories.findExtAmmo(weapon.ammoBoxes[index].ammoDef.Description.Id);
          if (HeatSetted == false) { HeatSetted = true; HeatPerShot = extAmmo.HeatDamagePerShot; continue; };
          if (HeatPerShot != extAmmo.HeatDamagePerShot) { return true; }
        }
        return false;
      }
      public static bool isWeaponHasClusterAmmo(Weapon weapon) {
        if (weapon.ammoBoxes.Count == 0) { return false; }
        int ClusterCount = 0;
        bool ClusterSetted = false;
        for (int index = 0; index < weapon.ammoBoxes.Count; ++index) {
          if (weapon.ammoBoxes[index].CurrentAmmo <= 0) { continue; }
          if (weapon.ammoBoxes[index].IsFunctional == false) { continue; }
          ExtAmmunitionDef extAmmo = CustomAmmoCategories.findExtAmmo(weapon.ammoBoxes[index].ammoDef.Description.Id);
          int ammoShots = (weapon.weaponDef.ShotsWhenFired + extAmmo.ShotsWhenFired) * (weapon.weaponDef.ProjectilesPerShot + extAmmo.ProjectilesPerShot);
          if (ClusterSetted == false) { ClusterSetted = true; ClusterCount = ammoShots; continue; };
          if (ClusterCount != ammoShots) { return true; }
        }
        return false;
      }
      public static void swtichToBestToHitMode(Weapon weapon,AbstractActor unit, ICombatant target) {
        ExtWeaponDef extWeapon = CustomAmmoCategories.getExtWeaponDef(weapon.defId);
        AbstractActor targetActor = (target as AbstractActor);
        string modeId = extWeapon.baseModeId;
        float delta = 1;
        CustomAmmoCategoriesLog.Log.LogWrite("detecting best "+weapon.UIName+" mode to hit "+target.DisplayName+"\n");
        foreach (var mode in extWeapon.Modes) {
          if(CustomAmmoCategories.checkExistance(weapon.StatCollection,CustomAmmoCategories.WeaponModeStatisticName) == false) {
            weapon.StatCollection.AddStatistic<string>(CustomAmmoCategories.WeaponModeStatisticName, mode.Key);
          }else {
            weapon.StatCollection.Set<string>(CustomAmmoCategories.WeaponModeStatisticName, mode.Key);
          }
          float toHit = 0;
          if (unit.HasLOFToTargetUnit(target, weapon.MaxRange, CustomAmmoCategories.getIndirectFireCapable(weapon))) {
            toHit = weapon.GetToHitFromPosition(target, 1, unit.CurrentPosition, target.CurrentPosition, true, (targetActor != null) ? targetActor.IsEvasive : false, false);
          }
          CustomAmmoCategoriesLog.Log.LogWrite(" Mode "+mode.Key+"\n");
          float curDelta = Math.Abs(toHit - mode.Value.AIHitChanceCap);
          CustomAmmoCategoriesLog.Log.LogWrite(" toHit: " + toHit + "\n");
          CustomAmmoCategoriesLog.Log.LogWrite(" AIHitCap: " + mode.Value.AIHitChanceCap + "\n");
          CustomAmmoCategoriesLog.Log.LogWrite(" delta: " + curDelta + "\n");
          if (curDelta < delta) {
            delta = curDelta;
            modeId = mode.Key;
          } else
          if(Math.Abs(curDelta-delta) < Epsilon) {
            if(mode.Value.isBaseMode == true) {
              delta = curDelta;
              modeId = mode.Key;
              CustomAmmoCategoriesLog.Log.LogWrite(" base mode\n");
            }
          }
        }
        CustomAmmoCategoriesLog.Log.LogWrite(" Mode choosed\n");
        weapon.StatCollection.Set<string>(CustomAmmoCategories.WeaponModeStatisticName, modeId);
      }
      public static void switchToMostHeatAmmo(Weapon weapon) {
        if (weapon.ammoBoxes.Count == 0) { return; }
        float HeatPerShot = 0;
        string ammoId = "";
        for (int index = 0; index < weapon.ammoBoxes.Count; ++index) {
          if (weapon.ammoBoxes[index].CurrentAmmo <= 0) { continue; }
          if (weapon.ammoBoxes[index].IsFunctional == false) { continue; }
          ExtAmmunitionDef extAmmo = CustomAmmoCategories.findExtAmmo(weapon.ammoBoxes[index].ammoDef.Description.Id);
          if (string.IsNullOrEmpty(ammoId)) {
            ammoId = weapon.ammoBoxes[index].ammoDef.Description.Id;
            HeatPerShot = extAmmo.HeatDamagePerShot;
            continue;
          };
          if (HeatPerShot < extAmmo.HeatDamagePerShot) {
            ammoId = weapon.ammoBoxes[index].ammoDef.Description.Id;
            HeatPerShot = extAmmo.HeatDamagePerShot;
          }
        }
        if (string.IsNullOrEmpty(ammoId) == false) {
          if (CustomAmmoCategories.checkExistance(weapon.StatCollection, CustomAmmoCategories.AmmoIdStatName) == false) {
            weapon.StatCollection.AddStatistic<string>(CustomAmmoCategories.AmmoIdStatName, ammoId);
          } else {
            weapon.StatCollection.Set<string>(CustomAmmoCategories.AmmoIdStatName, ammoId);
          }
        }
      }
      public static void switchToMostClusterAmmo(Weapon weapon) {
        if (weapon.ammoBoxes.Count == 0) { return; }
        int ClusterShot = 0;
        string ammoId = "";
        for (int index = 0; index < weapon.ammoBoxes.Count; ++index) {
          if (weapon.ammoBoxes[index].CurrentAmmo <= 0) { continue; }
          if (weapon.ammoBoxes[index].IsFunctional == false) { continue; }
          ExtAmmunitionDef extAmmo = CustomAmmoCategories.findExtAmmo(weapon.ammoBoxes[index].ammoDef.Description.Id);
          int ammoShots = (weapon.weaponDef.ShotsWhenFired + extAmmo.ShotsWhenFired) * (weapon.weaponDef.ProjectilesPerShot + extAmmo.ProjectilesPerShot);
          if (string.IsNullOrEmpty(ammoId)) {
            ammoId = weapon.ammoBoxes[index].ammoDef.Description.Id;
            ClusterShot = ammoShots;
            continue;
          };
          if (ClusterShot < ammoShots) {
            ammoId = weapon.ammoBoxes[index].ammoDef.Description.Id;
            ClusterShot = ammoShots;
          }
        }
        if (string.IsNullOrEmpty(ammoId) == false) {
          if (CustomAmmoCategories.checkExistance(weapon.StatCollection, CustomAmmoCategories.AmmoIdStatName) == false) {
            weapon.StatCollection.AddStatistic<string>(CustomAmmoCategories.AmmoIdStatName, ammoId);
          } else {
            weapon.StatCollection.Set<string>(CustomAmmoCategories.AmmoIdStatName, ammoId);
          }
        }
      }
      public static float calcHeatCoeff(AbstractActor unit, ICombatant target) {
        AbstractActor targetActor = (target as AbstractActor);
        //if (targetActor == null) { return 0.0f; };
        float result = 0;
        foreach (Weapon weapon in unit.Weapons) {
          if (weapon.HeatDamagePerShot == 0) { continue; }
          float toHit = 0f;
          if (unit.HasLOFToTargetUnit(target, weapon.MaxRange, CustomAmmoCategories.getIndirectFireCapable(weapon))) {
            toHit = weapon.GetToHitFromPosition(target, 1, unit.CurrentPosition, target.CurrentPosition, true, (targetActor != null) ? targetActor.IsEvasive : false, false);
          }
          result += (
              weapon.ShotsWhenFired
              * weapon.ProjectilesPerShot
              * weapon.HeatDamagePerShot
              * toHit
          );
        }
        return result;
      }
      public static bool hasHittableLocations(Weapon weapon, List<int> hitLocations, Mech target) {
        foreach (int location in hitLocations) {
          if (weapon.DamagePerShot * 2 >= target.ArmorForLocation(location)) {
            return true;
          }
        }
        return false;
      }
      public static List<string> getAvaibleEffectiveAmmo(Weapon weapon) {
        List<string> result = new List<string>();
        for (int index = 0; index < weapon.ammoBoxes.Count; ++index) {
          if (weapon.ammoBoxes[index].CurrentAmmo <= 0) { continue; }
          if (weapon.ammoBoxes[index].IsFunctional == false) { continue; }
          if (result.IndexOf(weapon.ammoBoxes[index].ammoDef.Description.Id) < 0) {
            result.Add(weapon.ammoBoxes[index].ammoDef.Description.Id);
          }
        }
        return result;
      }
      public static void SetWeaponAmmo(Weapon weapon, string ammoId) {
        if (CustomAmmoCategories.checkExistance(weapon.StatCollection, CustomAmmoCategories.AmmoIdStatName) == false) {
          weapon.StatCollection.AddStatistic<string>(CustomAmmoCategories.AmmoIdStatName, ammoId);
        } else {
          weapon.StatCollection.Set<string>(CustomAmmoCategories.AmmoIdStatName, ammoId);
        }
      }
      public static void ChooseBestWeaponForTarget(AbstractActor unit, ICombatant target, bool isStationary) {
        /*List<Weapon> ammoWeapons = new List<Weapon>();
        foreach (Weapon weapon in unit.Weapons) {
          if (CustomAmmoCategories.isWeaponHasDiffirentAmmo(weapon) == true) {
            CustomAmmoCategoriesLog.Log.LogWrite(" " + weapon.UIName + " has ammo to choose\n");
            ammoWeapons.Add(weapon);
          }
        }
        if (ammoWeapons.Count > 0) {
          if (target is Mech) {
            CustomAmmoCategoriesLog.Log.LogWrite(" Target is mech\n");
            Mech targetMech = (target as Mech);
            List<Weapon> ammoHeatWeapons = new List<Weapon>();
            foreach (Weapon weapon in ammoWeapons) {
              if (CustomAmmoCategories.isWeaponHasHeatAmmo(weapon) == true) {
                CustomAmmoCategories.switchToMostHeatAmmo(weapon);
                CustomAmmoCategoriesLog.Log.LogWrite(" " + weapon.UIName + " has hit ammo\n");
                ammoHeatWeapons.Add(weapon);
              }
            }
            float expectedHeat = CustomAmmoCategories.calcHeatCoeff(unit, target);
            CustomAmmoCategoriesLog.Log.LogWrite(" Expected heat " + expectedHeat + "\n");
            if ((targetMech.CurrentHeat + expectedHeat) > targetMech.OverheatLevel) {
              foreach (Weapon weapon in ammoHeatWeapons) {
                CustomAmmoCategoriesLog.Log.LogWrite(" " + weapon.UIName + " - ammo choosed\n");
                ammoWeapons.Remove(weapon);
              }
            }
            List<int> hitLocations = targetMech.GetPossibleHitLocations(unit);
            List<Weapon> ammoClusterWeapon = new List<Weapon>();
            foreach (Weapon weapon in ammoWeapons) {
              if (CustomAmmoCategories.isWeaponHasClusterAmmo(weapon)) {
                CustomAmmoCategories.switchToMostClusterAmmo(weapon);
                CustomAmmoCategoriesLog.Log.LogWrite(" " + weapon.UIName + " has cluster ammo\n");
                ammoClusterWeapon.Add(weapon);
              }
            }
            foreach (Weapon weapon in ammoClusterWeapon) {
              float toHit = 0f;
              if (weapon.parent.HasLOFToTargetUnit(target, weapon.MaxRange, CustomAmmoCategories.getIndirectFireCapable(weapon))) {
                toHit = weapon.GetToHitFromPosition(target, 1, unit.CurrentPosition, target.CurrentPosition, true, targetMech.IsEvasive, false);
              }
              if (toHit < 0.4f) {
                CustomAmmoCategoriesLog.Log.LogWrite(" " + weapon.UIName + " cluster toHit is too low " + toHit + "\n");
                continue;
              }
              if (CustomAmmoCategories.hasHittableLocations(weapon, hitLocations, targetMech) == true) {
                CustomAmmoCategoriesLog.Log.LogWrite(" " + weapon.UIName + " can crit one of locations\n");
                ammoWeapons.Remove(weapon);
              }
            }
          }
          AbstractActor targetActor = (target as AbstractActor);
          foreach (Weapon weapon in ammoWeapons) {
            List<string> avaibleAmmo = CustomAmmoCategories.getAvaibleEffectiveAmmo(weapon);
            if (avaibleAmmo.Count == 0) { continue; };
            CustomAmmoCategoriesLog.Log.LogWrite(" " + weapon.UIName + " choose ammo default algorithm\n");
            string bestAmmo = "";
            float expectedDamage = 0;
            foreach (string ammoId in avaibleAmmo) {
              CustomAmmoCategories.SetWeaponAmmo(weapon, ammoId);
              float toHit = 0f;
              if (unit.HasLOFToTargetUnit(target, weapon.MaxRange, CustomAmmoCategories.getIndirectFireCapable(weapon))) {
                toHit = weapon.GetToHitFromPosition(target, 1, unit.CurrentPosition, target.CurrentPosition, true, (targetActor != null) ? targetActor.IsEvasive : false, false);
              }
              float nonClusterCoeff = 1f;
              int numberOfShots = weapon.ShotsWhenFired * weapon.ProjectilesPerShot;
              if ((toHit > 0.6f) && (numberOfShots == 1)) {
                nonClusterCoeff = 1.2f;
              }
              float tempExpectedDamage = numberOfShots * weapon.DamagePerShot * toHit * nonClusterCoeff;
              CustomAmmoCategoriesLog.Log.LogWrite(" " + weapon.UIName + " toHit " + toHit + " expectedDamage:" + tempExpectedDamage + "\n");
              if (tempExpectedDamage > expectedDamage) {
                expectedDamage = tempExpectedDamage;
                bestAmmo = ammoId;
              }
            }
            if (string.IsNullOrEmpty(bestAmmo) == false) {
              CustomAmmoCategories.SetWeaponAmmo(weapon, bestAmmo);
              CustomAmmoCategoriesLog.Log.LogWrite(" " + weapon.UIName + " best ammo choosed\n");
            }
          }
        }else {
          CustomAmmoCategoriesLog.Log.LogWrite(" No ammo to choose\n");
        }
        List<Weapon> modeWeapons = new List<Weapon>();
        foreach (Weapon weapon in unit.Weapons) {
          if (CustomAmmoCategories.isWeaponHasDiffirentModes(weapon) == true) {
            CustomAmmoCategoriesLog.Log.LogWrite(" " + weapon.UIName + " has mode to choose\n");
            modeWeapons.Add(weapon);
          }
        }
        foreach (Weapon weapon in modeWeapons) {
          CustomAmmoCategories.swtichToBestToHitMode(weapon,unit,target);
        }
      }*/
    }
  }

namespace CustomAmmoCategoriesPatches {
  [HarmonyPatch(typeof(AttackEvaluator))]
  [HarmonyPatch("MakeAttackOrderForTarget")]
  [HarmonyPatch(MethodType.Normal)]
  public static class AttackEvaluator_MakeAttackOrderForTarget {
    public static bool Prefix(AbstractActor unit, ICombatant target, int enemyUnitIndex, bool isStationary) {
      CustomAmmoCategoriesLog.Log.LogWrite(unit.DisplayName + " choosing best weapon for target " + target.DisplayName + "\n");
      try {
        CustomAmmoCategories.ChooseBestWeaponForTarget(unit, target, isStationary);
        return true;
      } catch (Exception e) {
        CustomAmmoCategoriesLog.Log.LogWrite("Exception " + e.ToString() + "\nFallback to default\n");
        return true;
      }
    }
  }
  [HarmonyPatch(typeof(AttackEvaluator))]
  [HarmonyPatch("MakeAttackOrder")]
  [HarmonyPatch(MethodType.Normal)]
  public static class AttackEvaluator_MakeAttackOrder {
    public static void Postfix(AbstractActor unit, bool isStationary, BehaviorTreeResults __result) {
      CustomAmmoCategoriesLog.Log.LogWrite("Choose result for " + unit.DisplayName + "\n");
      try {
        if (__result.nodeState == BehaviorNodeState.Failure) {
          CustomAmmoCategoriesLog.Log.LogWrite("  AI choosed not attack\n");
        } else
        if (__result.orderInfo is AttackOrderInfo) {
          CustomAmmoCategoriesLog.Log.LogWrite("  AI choosed to attack " + (__result.orderInfo as AttackOrderInfo).TargetUnit.DisplayName + "\n");
          CustomAmmoCategories.ChooseBestWeaponForTarget(unit, (__result.orderInfo as AttackOrderInfo).TargetUnit, isStationary);
        } else {
          CustomAmmoCategoriesLog.Log.LogWrite("  AI choosed something else beside attaking\n");
        }
        return;
      } catch (Exception e) {
        CustomAmmoCategoriesLog.Log.LogWrite("Exception " + e.ToString() + "\nFallback to default\n");
        return;
      }
    }
  }
}
