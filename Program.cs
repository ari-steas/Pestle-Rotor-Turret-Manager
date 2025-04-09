using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
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
    partial class Program : MyGridProgram
    {
        #region mdk preserve


        
            // You probably shouldn't touch below this line if you aren't familiar with scripting,         //
          // but I wish you the best of luck if you do! @aristeas. on discord if you have questions. //
        // - Aristeas                                                                                                                            //


        #endregion

        public static WcPbApi WcApi = new WcPbApi();
        public static Program I;
        #if DEBUG
        public static DebugAPI DebugApi;
        #endif

        public List<RotorTurret> RotorTurrets;
        public List<IMyTerminalBlock> TargetSourceTurrets = new List<IMyTerminalBlock>();
        public Dictionary<char, RotorTurretSettings> Settings;

        public bool UseGridTarget = true;
        public string SourceWeaponGroup = "Rotor Turret Source";
        public float GridAimTolerance = 1;
        public float BlockAimTolerance = 3;
        public const string IgnoreBlockTag = "[PestleIgnore]";

        private Dictionary<long, MyDetectedEntityInfo> _threatBuffer = new Dictionary<long, MyDetectedEntityInfo>();

        public Program()
        {
            I = this;
            #if DEBUG
            DebugApi = new DebugAPI(this, true);
            #endif
            if (!WcApi.Activate(Me))
                throw new Exception("Failed to initialize WcPbApi!");

            Settings = ReadSettings();
            RotorTurrets = RotorTurret.InitTurrets();
            WriteSettings();

            if (SourceWeaponGroup != "")
                GridTerminalSystem.GetBlockGroupWithName(SourceWeaponGroup)?.GetBlocksOfType(TargetSourceTurrets, b => WcApi.HasCoreWeapon(b) && !b.CustomName.StartsWith(IgnoreBlockTag));

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            #if DEBUG
            DebugApi.RemoveDraw();
            #endif

            _threatBuffer.Clear();
            WcApi.GetSortedThreatsByID(Me, _threatBuffer);

            var gridTarget = WcApi.GetAiFocus(Me.CubeGrid.EntityId);
            var wepTargets = TargetSourceTurrets.Select(w => WcApi.GetWeaponTarget(w)).ToList();

            #if DEBUG
            DebugApi.PrintHUD($"GridTarget: {gridTarget?.EntityId ?? 0}", seconds: 1 / 60f);
            DebugApi.PrintHUD($"WepTargets: {string.Join(",", wepTargets.Select(t => t?.EntityId ?? -1))}", seconds: 1 / 60f);
            #endif

            foreach (var turret in RotorTurrets)
                turret.Update(gridTarget, wepTargets);
        }

        public Dictionary<char, RotorTurretSettings> ReadSettings()
        {
            var ini = new MyIni();
            ini.TryParse(Me.CustomData);

            var settings = new Dictionary<char, RotorTurretSettings>();

            var iniSections = new List<string>();
            ini.GetSections(iniSections);
            foreach (var section in iniSections)
            {
                if (section == "General Config")
                {
                    UseGridTarget = ini.Get(section, "UseGridTarget").ToBoolean(UseGridTarget);
                    SourceWeaponGroup = ini.Get(section, "SourceWeaponGroup").ToString(SourceWeaponGroup);
                    GridAimTolerance = ini.Get(section, "GridAimTolerance").ToSingle(GridAimTolerance);
                    BlockAimTolerance = ini.Get(section, "BlockAimTolerance").ToSingle(BlockAimTolerance);
                }
                else
                    settings[section[0]] = new RotorTurretSettings(section, ini);
            }

            return settings;
        }

        public void WriteSettings()
        {
            var ini = new MyIni();
            ini.AddSection("General Config");
            ini.SetSectionComment("General Config", " 'Pestle' Rotor Turret Manager Settings\n\n Set default values for turrets tagged with that section's letter below,\n   then recompile.\n Delete a line to reset it to default.\n ");
            
            ini.Set("General Config", "UseGridTarget", UseGridTarget);
            ini.SetComment("General Config", "UseGridTarget", "Should the script consider grid (scroll wheel) target?");

            ini.Set("General Config", "SourceWeaponGroup", SourceWeaponGroup);
            ini.SetComment("General Config", "SourceWeaponGroup", "Name for terminal group the script should pull targeting info from. Optional.");

            ini.Set("General Config", "GridAimTolerance", GridAimTolerance);
            ini.SetComment("General Config", "GridAimTolerance", "Multiplier for grid aim tolerance; based on grid WorldAABB.");

            ini.Set("General Config", "BlockAimTolerance", BlockAimTolerance);
            ini.SetComment("General Config", "BlockAimTolerance", "Multiplier for block aim tolerance; based on block WorldAABB.");

            foreach (var turret in RotorTurrets)
            {
                turret.Settings.Write(turret.Id, ini);
                ini.SetSectionComment("" + turret.Id, $" {turret.WeaponElevationMap.Count} elevation rotor(s) & {turret.WeaponElevationMap.Values.Sum(l => l.Count)} weapon(s)");
            }

            Me.CustomData = ini.ToString();
        }

        public Vector3D? GetBlockLeadPos(MyDetectedEntityInfo blockInfo, IMyTerminalBlock weapon)
        {
            if (blockInfo.EntityId == 0)
                return null;

            var threatToUse = default(MyDetectedEntityInfo);
            foreach (var threat in _threatBuffer)
            {
                if (threat.Value.BoundingBox.Contains(blockInfo.Position) == ContainmentType.Disjoint)
                    continue;
                threatToUse = threat.Value;
                break;
            }

            if (threatToUse.Equals(default(MyDetectedEntityInfo)))
                return blockInfo.Position;

            var gridPredict = WcApi.GetPredictedTargetPosition(weapon, threatToUse.EntityId, 0);
            if (gridPredict == null)
                return blockInfo.Position;

            return gridPredict.Value - threatToUse.Position + blockInfo.Position;
        }
    }
}
