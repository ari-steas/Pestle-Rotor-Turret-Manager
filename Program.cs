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
        public Dictionary<char, RotorTurretSettings> AllTurretSettings;

        private bool _hasInited = false;
        private Dictionary<long, MyDetectedEntityInfo> _threatBuffer = new Dictionary<long, MyDetectedEntityInfo>();

        public Program()
        {
            Echo($"'Pestle' Rotor Turret Manager\n====================\nTime: {DateTime.Now:T}");
            I = this;
            #if DEBUG
            DebugApi = new DebugAPI(this, true);
            #endif
            if (!WcApi.Activate(Me))
                throw new Exception("Failed to initialize WcPbApi!");

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Init()
        {
            // Initing with a delay to avoid a bug where weaponcore doesn't recognize weapons when first placed
            AllTurretSettings = Settings.Read();
            RotorTurrets = RotorTurret.InitTurrets();
            Settings.Write();

            if (Settings.SourceWeaponGroup != "")
                GridTerminalSystem.GetBlockGroupWithName(Settings.SourceWeaponGroup)?.GetBlocksOfType(TargetSourceTurrets, b => WcApi.HasCoreWeapon(b) && !b.CustomName.StartsWith(Settings.IgnoreBlockTag));

            _hasInited = true;
            Echo("Ready!\n\nThis won't update while the script is running, so don't worry if it looks frozen.");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            #if DEBUG
            DebugApi.RemoveDraw();
            #endif

            if (!_hasInited)
                Init();

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
