using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class RotorTurret
        {
            public char Id;
            public RotorTurretSettings Settings;

            public IMyMotorStator AzimuthRotor;
            public Dictionary<IMyMotorStator, List<IMyTerminalBlock>> WeaponElevationMap;
            public List<IMyTerminalBlock> AllWeapons = new List<IMyTerminalBlock>();
            public float DesiredAzimuth, DesiredElevation;

            private Dictionary<IMyMotorStator, MatrixD> _weaponPosCache;

            public RotorTurret(char suggestedId, IMyMotorStator aziRotor, List<IMyMotorStator> eleRotors,
                Dictionary<IMyCubeGrid, List<IMyTerminalBlock>> allTopParts)
            {
                AzimuthRotor = aziRotor;
                Id = suggestedId;
                if (aziRotor.CustomName.Length >= 3 && aziRotor.CustomName[0] == '[' && aziRotor.CustomName[2] == ']')
                    Id = char.ToUpper(aziRotor.CustomName[1]);
                else
                    aziRotor.CustomName = $"[{Id}] {aziRotor.CustomName}";

                WeaponElevationMap = new Dictionary<IMyMotorStator, List<IMyTerminalBlock>>(eleRotors.Count);
                _weaponPosCache = new Dictionary<IMyMotorStator, MatrixD>(eleRotors.Count);
                foreach (var eleRotor in eleRotors)
                {
                    SetBlockId(eleRotor);
                    WeaponElevationMap[eleRotor] = allTopParts[eleRotor.TopGrid];
                    foreach (var wep in WeaponElevationMap[eleRotor])
                    {
                        AllWeapons.Add(wep);
                        SetBlockId(wep);
                    }
                    _weaponPosCache[eleRotor] = MatrixD.Identity;
                }

                if (!I.Settings.TryGetValue(Id, out Settings))
                {
                    Settings = new RotorTurretSettings();
                    I.Settings[Id] = Settings;
                }

                ResetVelocity();
                DesiredAzimuth = MathHelper.ToRadians(Settings.HomeAzimuth);
                DesiredElevation = MathHelper.ToRadians(Settings.HomeElevation);
            }

            public void Update(MyDetectedEntityInfo? gridTarget, IEnumerable<MyDetectedEntityInfo?> weaponTargets)
            {
                if (!AzimuthRotor.IsWorking || AzimuthRotor.Closed)
                    return;

                IMyTerminalBlock checkWep = null;
                float checkRange = -1;
                foreach (var wep in AllWeapons)
                {
                    if (!wep.IsWorking || wep.Closed)
                        continue;
                    var range = WcApi.GetMaxWeaponRange(wep, 0);
                    if (checkWep != null && !(range > checkRange))
                        continue;

                    checkWep = wep;
                    checkRange = range;
                }
                    


                MatrixD wepMatrix;
                if (checkWep == null || !GetWeaponMatrix(out wepMatrix))
                {
                    ResetVelocity();
                    //DebugApi.PrintHUD("Invalid weapon", seconds: 1 / 60f);
                    return;
                }

                Vector3D? targetLeadPos = null;
                BoundingBoxD targetBounds = default(BoundingBoxD);

                // Target info
                {
                    if (gridTarget != null && gridTarget.Value.EntityId != 0)
                    {
                        var leadPos = WcApi.GetPredictedTargetPosition(checkWep, gridTarget.Value.EntityId, 0);
                        var canAim = CanAimAt(leadPos, wepMatrix);

                        if (canAim)
                        {
                            targetLeadPos = leadPos;
                            targetBounds = gridTarget.Value.BoundingBox;
                            targetBounds = targetBounds.Inflate(targetBounds.Size * I.GridAimTolerance - targetBounds.Size);
                        }

                        #if DEBUG
                        DebugApi.PrintHUD($"{(canAim ? "Use" : "Skip")} Grid Target", seconds: 1 / 60f);
                        #endif
                    }
                    
                    if (!Settings.PreferGridTarget || targetLeadPos == null)
                    {
                        var wepTarget = weaponTargets.FirstOrDefault(t => (t?.EntityId ?? 0) != 0 && CanAimAt(I.GetBlockLeadPos(t.Value, checkWep), wepMatrix));
                        
                        //DebugApi.PrintHUD($"Check Weapon Target ({(wepTarget?.EntityId ?? 0) != 0})", seconds: 1 / 60f);

                        if ((wepTarget?.EntityId ?? 0) != 0)
                        {
                            targetLeadPos = (wepTarget?.EntityId ?? 0) == 0 ? null : I.GetBlockLeadPos(wepTarget.Value, checkWep);
                            targetBounds = wepTarget?.BoundingBox ?? default(BoundingBoxD);
                            targetBounds = targetBounds.Inflate(targetBounds.Size * I.BlockAimTolerance - targetBounds.Size);
                        }
                    }
                }

                #if DEBUG
                if (targetLeadPos != null)
                    DebugApi.DrawPoint(targetLeadPos.Value, Color.Red, 2);
                #endif


                // Azimuth
                Utils.AimRotor(AzimuthRotor, wepMatrix, ref DesiredAzimuth, targetLeadPos, float.MinValue, float.MaxValue, Settings.HomeAzimuth, Settings.AziSpeed);

                // Elevation
                foreach (var ele in WeaponElevationMap.Keys)
                    Utils.AimRotor(ele, _weaponPosCache[ele], ref DesiredElevation, targetLeadPos, (float) -Math.PI/2, (float) Math.PI/2, Settings.HomeElevation, Settings.EleSpeed);

                // Weapon firing
                {
                    var maxWepRange = Settings.RangeOverride < 0 ? checkRange : Settings.RangeOverride;
                    targetBounds.Centerize(targetLeadPos ?? Vector3D.Zero);
                    bool anyCanHit = false;
                    #if DEBUG
                    DebugApi.DrawAABB(targetBounds, Color.Red);
                    DebugApi.PrintHUD($"Max Range: {maxWepRange:N0}", seconds: 1/60f);
                    #endif

                    foreach (var weapon in AllWeapons)
                    {
                        if (weapon.Closed)
                            continue;

                        var scope = WcApi.GetWeaponScope(weapon, 0);

                        if (!anyCanHit)
                            anyCanHit = (targetBounds.Intersects(new Ray(scope.Item1, scope.Item2)) ?? double.MaxValue) < maxWepRange;

                        #if DEBUG
                        DebugApi.DrawLine(scope.Item1, scope.Item1 + scope.Item2 * WcApi.GetMaxWeaponRange(weapon, 0),
                            anyCanHit ? Color.Red : Color.White);
                        #else
                        if (anyCanHit)
                            break;
                        #endif
                    }

                    if (targetLeadPos != null && anyCanHit)
                    {
                        foreach (var weapon in AllWeapons)
                            Utils.FireWeapon(weapon, WcApi.IsWeaponReadyToFire(weapon));
                    }
                    else
                    {
                        foreach (var weapon in AllWeapons)
                            Utils.FireWeapon(weapon, false);
                    }
                }

                //#if DEBUG
                //DebugApi.PrintHUD(
                //    $"Azi: {MathHelper.ToDegrees(AzimuthRotor.Angle):F0} Elev: {MathHelper.ToDegrees(WeaponElevationMap.Keys.First().Angle):F0}",
                //    seconds: 1 / 60f);
                //DebugApi.PrintHUD(
                //    $"DAzi: {MathHelper.ToDegrees(DesiredAzimuth):F0} DElev: {MathHelper.ToDegrees(DesiredElevation):F0}",
                //    seconds: 1 / 60f);
                //#endif
            }

            private void ResetVelocity()
            {
                AzimuthRotor.TargetVelocityRad = 0;

                foreach (var ele in WeaponElevationMap.Keys)
                    ele.TargetVelocityRad = 0;
            }

            private void SetBlockId(IMyTerminalBlock block)
            {
                if (block.CustomName.Length >= 3 && block.CustomName[0] == '[' && block.CustomName[2] == ']')
                    block.CustomName = $"[{Id}]{block.CustomName.Substring(3)}";
                else
                    block.CustomName = $"[{Id}] {block.CustomName}";
            }

            private bool GetWeaponMatrix(out MatrixD matrix)
            {
                matrix = MatrixD.Identity;
                Vector3D position = Vector3D.Zero;
                Vector3D direction = Vector3D.Zero;
                int weaponCount = 0;
                foreach (var elev in WeaponElevationMap)
                {
                    if (elev.Key.Closed || !elev.Key.IsWorking)
                        continue;

                    var elevPosition = Vector3D.Zero;
                    var elevDirection = Vector3D.Zero;

                    foreach (var wep in elev.Value)
                    {
                        if (wep.Closed || !wep.IsWorking)
                            continue;
                        var scopeSet = WcApi.GetWeaponScope(wep, 0);
                        position += scopeSet.Item1;
                        elevPosition += scopeSet.Item1;

                        if (direction == Vector3D.Zero)
                            direction = scopeSet.Item2;
                        if (elevDirection == Vector3D.Zero)
                            elevDirection = scopeSet.Item2;

                        weaponCount++;
                    }

                    _weaponPosCache[elev.Key] = MatrixD.CreateWorld(elevPosition / elev.Value.Count, elevDirection, elev.Key.WorldMatrix.Up);
                }
                position /= weaponCount;

                if (direction == Vector3D.Zero)
                    return false;

                Vector3D rightVec = Vector3D.Cross(direction, AzimuthRotor.WorldMatrix.Up);
                direction = Vector3D.Cross(AzimuthRotor.WorldMatrix.Up, rightVec);

                matrix = MatrixD.CreateWorld(position, direction, AzimuthRotor.WorldMatrix.Up);
                return true;
            }

            private bool CanAimAt(Vector3D? position, MatrixD wepMatrix)
            {
                if (position == null)
                    return false;

                var minAzi = AzimuthRotor.LowerLimitRad <= Math.PI ? float.MinValue : Utils.NormalizeAngle(AzimuthRotor.LowerLimitRad);
                var maxAzi = AzimuthRotor.UpperLimitRad >= Math.PI ? float.MaxValue : Utils.NormalizeAngle(AzimuthRotor.UpperLimitRad);
                var aziAngle = Utils.NormalizeAngle(AzimuthRotor.Angle - Utils.GetAziAngleToTarget(wepMatrix, position, 0));

                if (aziAngle < minAzi ||
                    aziAngle > maxAzi)
                {
                    #if DEBUG
                    DebugApi.DrawLine(wepMatrix.Translation, position.Value, Color.Red, 0.01f);
                    #endif
                    return false;
                }

                foreach (var eleRotor in WeaponElevationMap.Keys)
                {
                    var minEle = eleRotor.LowerLimitRad <= Math.PI ? float.MinValue : Utils.NormalizeAngle(eleRotor.LowerLimitRad);
                    var maxEle = eleRotor.UpperLimitRad >= Math.PI ? float.MaxValue : Utils.NormalizeAngle(eleRotor.UpperLimitRad);
                    var eleAngle = Utils.NormalizeAngle(eleRotor.Angle - Utils.GetAziAngleToTarget(_weaponPosCache[eleRotor], position, 0));

                    if (eleAngle >= minEle &&
                        eleAngle <= maxEle)
                    {
                        #if DEBUG
                        DebugApi.DrawLine(wepMatrix.Translation, position.Value, Color.Green, 0.01f);
                        #endif
                        return true;
                    }
                }

                #if DEBUG
                DebugApi.DrawLine(wepMatrix.Translation, position.Value, Color.Orange, 0.01f);
                #endif

                return false;
            }


            public static List<RotorTurret> InitTurrets()
            {
                var staticDefs = new List<MyDefinitionId>();
                WcApi.GetAllCoreStaticLaunchers(staticDefs);
                var allTurretWeps = new List<IMyTerminalBlock>();
                I.GridTerminalSystem.GetBlocksOfType(allTurretWeps, b => b.CubeGrid != I.Me.CubeGrid && WcApi.HasCoreWeapon(b) && staticDefs.Contains(b.BlockDefinition) && !b.CustomName.StartsWith(IgnoreBlockTag));

                var topSubparts = new Dictionary<IMyCubeGrid, List<IMyTerminalBlock>>();
                foreach (var wep in allTurretWeps)
                {
                    if (!topSubparts.ContainsKey(wep.CubeGrid))
                        topSubparts[wep.CubeGrid] = new List<IMyTerminalBlock> { wep };
                    else
                        topSubparts[wep.CubeGrid].Add(wep);
                }

                var allRotors = new List<IMyMotorStator>();
                I.GridTerminalSystem.GetBlocksOfType(allRotors, b => !b.CustomName.StartsWith(IgnoreBlockTag));
                var turretSubgrids = new Dictionary<IMyMotorStator, List<IMyMotorStator>>();
                foreach (var grid in topSubparts.Keys)
                {
                    var eleRotor = allRotors.FirstOrDefault(r => r.TopGrid == grid);
                    if (eleRotor == null)
                        throw new Exception($"No top rotor exists for grid {grid.CustomName}!");
                    var aziRotor = allRotors.FirstOrDefault(r => r.TopGrid == eleRotor.CubeGrid);
                    if (aziRotor == null)
                    {
                        I.Echo($"No elevation rotor exists for grid {grid.CustomName}.");
                        continue;
                    }

                    if (!turretSubgrids.ContainsKey(aziRotor))
                        turretSubgrids[aziRotor] = new List<IMyMotorStator> { eleRotor };
                    else
                        turretSubgrids[aziRotor].Add(eleRotor);
                }

                var toReturn = new Dictionary<char, RotorTurret>(turretSubgrids.Count);
                char suggestedId = 'A';
                foreach (var rotorSet in turretSubgrids)
                {
                    var turret = new RotorTurret(suggestedId, rotorSet.Key, rotorSet.Value, topSubparts);
                    toReturn[turret.Id] = turret;

                    if (suggestedId == turret.Id)
                        while (toReturn.ContainsKey(suggestedId))
                            suggestedId++;
                }

                I.Echo($"Found {allTurretWeps.Count} weapon(s) and {toReturn.Count} turret(s).");
                return toReturn.Values.ToList();
            }
        }
    }
}
