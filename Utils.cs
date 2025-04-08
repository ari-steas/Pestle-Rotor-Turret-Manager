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
        public static class Utils
        {
            public static void AimRotor(IMyMotorStator rotor, MatrixD matrix, ref float desired, Vector3D? targetPos, float lowerLimit, float upperLimit, float homeAngle, float speedMult)
            {
                #if DEBUG
                DebugApi.DrawMatrix(matrix);
                #endif

                var angle = NormalizeAngle(rotor.Angle);
                var newAngle = NormalizeAngle(angle - GetAziAngleToTarget(matrix, targetPos, angle + homeAngle));

                desired = MathHelper.Clamp(newAngle, lowerLimit, upperLimit);
                rotor.TargetVelocityRad = speedMult * NormalizeAngle(desired - angle);

                // Limits
                //var minAngle = Math.Min(angle, desired);
                //var maxAngle = Math.Max(angle, desired);
                //if (Math.Abs(rotor.LowerLimitRad - minAngle) > MaxStepLength) // Rotors lock up if you update limits too frequently.
                //    rotor.LowerLimitRad = minAngle;
                //if (Math.Abs(rotor.UpperLimitRad - maxAngle) > MaxStepLength)
                //    rotor.UpperLimitRad = maxAngle;
            }

            public static void FireWeapon(IMyTerminalBlock weapon, bool enabled)
            {
                if (enabled)
                {
                    if (!weapon.GetValueBool("WC_Shoot"))
                    {
                        weapon.SetValueBool("WC_Shoot", true);
                    }
                }
                else if (weapon.GetValueBool("WC_Shoot"))
                {
                    weapon.SetValueBool("WC_Shoot", false);
                }
            }

            /// <summary>
            /// Returns the angle needed to reach a target.
            /// </summary>
            /// <returns></returns>
            public static float GetAziAngleToTarget(MatrixD thisMatrix, Vector3D? targetPos, float home)
            {
                if (targetPos == null)
                    return home;

                Vector3D vecFromTarget = thisMatrix.Translation - targetPos.Value;

                vecFromTarget = Vector3D.Rotate(vecFromTarget.Normalized(), MatrixD.Invert(thisMatrix));

                double desiredAzimuth = Math.Atan2(vecFromTarget.X, vecFromTarget.Z);
                if (double.IsNaN(desiredAzimuth))
                    desiredAzimuth = Math.PI;

                return (float) desiredAzimuth;
            }

            public static float NormalizeAngle(float angleRads, float limit = (float) (Math.PI))
            {
                if (angleRads > limit)
                    return (angleRads % limit) - limit;
                if (angleRads < -limit)
                    return (angleRads % limit) + limit;
                return angleRads;
            }
        }
    }
}
