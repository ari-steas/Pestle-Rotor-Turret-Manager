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
        public class RotorTurretSettings
        {
            public readonly float HomeAzimuth, HomeElevation, AziSpeed, EleSpeed;
            public bool PreferGridTarget;

            public RotorTurretSettings()
            {
                HomeAzimuth = 0;
                HomeElevation = 0;
                PreferGridTarget = false;
                AziSpeed = 1; 
                EleSpeed = 1;
            }

            public RotorTurretSettings(string section, MyIni ini) : this()
            {
                HomeAzimuth = ini.Get(section, "HomeAzimuth").ToSingle(HomeAzimuth);
                HomeElevation = ini.Get(section, "HomeElevation").ToSingle(HomeElevation);
                PreferGridTarget = ini.Get(section, "PreferGridTarget").ToBoolean(PreferGridTarget);
                AziSpeed = ini.Get(section, "AziSpeed").ToSingle(AziSpeed);
                EleSpeed = ini.Get(section, "EleSpeed").ToSingle(EleSpeed);
            }

            public void Write(char id, MyIni ini)
            {
                var section = id + "";
                ini.AddSection(section);
                ini.Set(section, "HomeAzimuth", HomeAzimuth);
                ini.Set(section, "HomeElevation", HomeElevation);
                ini.Set(section, "PreferGridTarget", PreferGridTarget);
                ini.Set(section, "AziSpeed", AziSpeed);
                ini.Set(section, "EleSpeed", EleSpeed);
            }
        }
    }
}
