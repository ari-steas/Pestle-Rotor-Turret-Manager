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
        /// <summary>
        /// https://github.com/Ash-LikeSnow/WeaponCore/blob/master/Data/Scripts/CoreSystems/Api/CoreSystemsPbApi.cs
        /// </summary>
        public class WcPbApi
        {
            private Action<ICollection<MyDefinitionId>> _getCoreStaticLaunchers;
            private Action<IMyTerminalBlock, IDictionary<long, MyDetectedEntityInfo>> _getSortedThreatsByID;
            private Func<long, int, MyDetectedEntityInfo> _getAiFocus;
            private Func<IMyTerminalBlock, int, MyDetectedEntityInfo> _getWeaponTarget;
            private Func<IMyTerminalBlock, int, float> _getMaxWeaponRange;
            private Func<IMyTerminalBlock, long, int, Vector3D?> _getPredictedTargetPos;
            private Func<IMyTerminalBlock, bool> _hasCoreWeapon;
            private Func<IMyTerminalBlock, int, MyTuple<Vector3D, Vector3D>> _getWeaponScope;

            // Descriptions made by Aristeas, with Sigmund Froid's https://steamcommunity.com/sharedfiles/filedetails/?id=2178802013 as a reference.
            // PR accepted after prolific begging by Aryx
            
            /// <summary>
            /// Activates the WcPbAPI using <see cref="IMyTerminalBlock"/> <paramref name="pbBlock"/>.
            /// </summary>
            /// <remarks>
            /// Recommended to use 'Me' in <paramref name="pbBlock"/> for simplicity.
            /// </remarks>
            /// <param name="pbBlock"></param>
            /// <returns><see cref="true"/>  if all methods assigned correctly, <see cref="false"/>  otherwise</returns>
            /// <exception cref="Exception">Throws exception if WeaponCore is not present</exception>
            public bool Activate(IMyTerminalBlock pbBlock)
            {
                var dict = pbBlock.GetProperty("WcPbAPI")?.As<IReadOnlyDictionary<string, Delegate>>().GetValue(pbBlock);
                if (dict == null) throw new Exception("WcPbAPI failed to activate");
                return ApiAssign(dict);
            }

            /// <summary>
            /// Bulk calls <see cref="AssignMethod" /> for all WcPbAPI methods.
            /// </summary>
            /// <remarks>
            /// Not useful for most scripts, but is public nonetheless.
            /// </remarks>
            /// <param name="delegates"></param>
            /// <returns><see cref="true"/>  if all methods assigned correctly, <see cref="false"/>  otherwise</returns>
            public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
            {
                if (delegates == null)
                    return false;

                // Aristeas trimmed this down a lot.
                AssignMethod(delegates, "GetCoreStaticLaunchers", ref _getCoreStaticLaunchers);
                AssignMethod(delegates, "GetSortedThreatsByID", ref _getSortedThreatsByID);
                AssignMethod(delegates, "GetAiFocus", ref _getAiFocus);
                AssignMethod(delegates, "GetWeaponTarget", ref _getWeaponTarget);
                AssignMethod(delegates, "GetMaxWeaponRange", ref _getMaxWeaponRange);
                AssignMethod(delegates, "GetPredictedTargetPosition", ref _getPredictedTargetPos);
                AssignMethod(delegates, "HasCoreWeapon", ref _hasCoreWeapon);
                AssignMethod(delegates, "GetWeaponScope", ref _getWeaponScope);

                return true;
            }

            /// <summary>
            /// Links method <paramref name="field"/> to internal API method of name <paramref name="name"/>
            /// </summary>
            /// <remarks>
            /// Not useful for most scripts, but is public nonetheless.
            /// </remarks>
            /// <typeparam name="T"></typeparam>
            /// <param name="delegates"></param>
            /// <param name="name"></param>
            /// <param name="field"></param>
            /// <exception cref="Exception"></exception>
            private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
            {
                if (delegates == null)
                {
                    field = null;
                    return;
                }

                Delegate del;
                if (!delegates.TryGetValue(name, out del))
                    throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");

                field = del as T;
                if (field == null)
                    throw new Exception(
                        $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
            }

            /// <summary>
            /// Populates <paramref name="collection"/> with <see cref="MyDefinitionId"/> of all loaded WeaponCore fixed weapons.
            /// </summary>
            /// <param name="collection"></param>
            /// <seealso cref="GetAllCoreWeapons"/>
            /// <seealso cref="GetAllCoreTurrets"/>
            public void GetAllCoreStaticLaunchers(ICollection<MyDefinitionId> collection) =>
                _getCoreStaticLaunchers?.Invoke(collection);

            /// <summary>
            /// Populates <paramref name="collection"/> with contents:
            /// <list type="bullet">
            /// <item>Key: Entity ID</item>
            /// <item>Value: Hostile <see cref="MyDetectedEntityInfo"/> within targeting range of <paramref name="pBlock"/>'s grid</item>
            /// </list>
            /// </summary>
            /// <param name="pBlock"></param>
            /// <param name="collection"></param>
            public void GetSortedThreatsByID(IMyTerminalBlock pBlock, IDictionary<long, MyDetectedEntityInfo> collection) =>
                _getSortedThreatsByID?.Invoke(pBlock, collection);

            /// <summary>
            /// Returns the GridAi Target with priority <paramref name="priority"/> of <see cref="IMyCubeGrid"/> with EntityID <paramref name="shooter"/>.
            /// </summary>
            /// <remarks>
            /// If the grid is valid but does not have a target, an empty <see cref="MyDetectedEntityInfo"/> is returned.
            /// <para>
            /// Default <paramref name="priority"/> = 0 returns the player-selected target.
            /// </para>
            /// </remarks>
            /// <param name="shooter"></param>
            /// <param name="priority"></param>
            /// <returns>Nullable <see cref="MyDetectedEntityInfo"/>. Null if <paramref name="shooter"/> does not exist or lacks GridAi.</returns>
            public MyDetectedEntityInfo? GetAiFocus(long shooter, int priority = 0) => _getAiFocus?.Invoke(shooter, priority);

            /// <summary>
            /// Returns the WeaponAi target of <paramref name="weaponId"/> on <paramref name="weapon"/>.
            /// </summary>
            /// <remarks>
            /// Seems to always return null for static weapons.
            /// </remarks>
            /// <param name="weapon"></param>
            /// <param name="weaponId"></param>
            /// <returns>Nullable <see cref="MyDetectedEntityInfo"/>.</returns>
            public MyDetectedEntityInfo? GetWeaponTarget(IMyTerminalBlock weapon, int weaponId = 0) =>
                _getWeaponTarget?.Invoke(weapon, weaponId);

            /// <summary>
            /// Returns the current Aiming Radius of <paramref name="weaponId"/> on <paramref name="weapon"/>.
            /// </summary>
            /// <param name="weapon"></param>
            /// <param name="weaponId"></param>
            /// <returns><see cref="float"/> range in meters.</returns>
            public float GetMaxWeaponRange(IMyTerminalBlock weapon, int weaponId) =>
                _getMaxWeaponRange?.Invoke(weapon, weaponId) ?? 0f;

            /// <summary>
            /// Returns the lead position of <paramref name="weaponId"/> on <paramref name="weapon"/>, with target EntityId <paramref name="targetEnt"/>.
            /// </summary>
            /// <param name="weapon"></param>
            /// <param name="targetEnt"></param>
            /// <param name="weaponId"></param>
            /// <returns>Nullable <see cref="Vector3D"/> target lead position. Null if target or weapon invalid.</returns>
            public Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
                _getPredictedTargetPos?.Invoke(weapon, targetEnt, weaponId) ?? null;

            /// <summary>
            /// Returns whether or not <see cref="IMyTerminalBlock"/> <paramref name="weapon"/> has a WeaponCore weapon.
            /// </summary>
            /// <param name="entity"></param>
            /// <returns>true if weapon present, false otherwise.</returns>
            public bool HasCoreWeapon(IMyTerminalBlock weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;

            /// <summary>
            /// Returns scope information of <paramref name="weaponId"/> on <paramref name="weapon"/>.
            /// </summary>
            /// <param name="weapon"></param>
            /// <param name="weaponId"></param>
            /// <returns>
            /// <see cref="MyTuple{Vector3D, Vector3D}"/> with contents:
            /// <list type="number">
            /// <item><see cref="Vector3D"/> Position</item>
            /// <item><see cref="Vector3D"/> Direction</item>
            /// </list>
            /// </returns>
            public MyTuple<Vector3D, Vector3D> GetWeaponScope(IMyTerminalBlock weapon, int weaponId) =>
                _getWeaponScope?.Invoke(weapon, weaponId) ?? new MyTuple<Vector3D, Vector3D>();
        }
    }
}
