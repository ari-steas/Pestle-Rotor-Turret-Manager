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
            private readonly float _homeAzimuth, _homeElevation;
            public readonly float RangeOverride, AziSpeed, EleSpeed;
            public float HomeAzimuth => _homeAzimuth == -1 ? Settings.DefaultHomeAzimuth : _homeAzimuth;
            public float HomeElevation => _homeElevation == -1 ? Settings.DefaultHomeElevation : _homeElevation;

            public bool PreferGridTarget;

            public RotorTurretSettings()
            {
                _homeAzimuth = -1;
                _homeElevation = -1;
                PreferGridTarget = false;
                RangeOverride = -1;
                AziSpeed = 1; 
                EleSpeed = 1;
            }

            public RotorTurretSettings(string section, MyIni ini) : this()
            {
                _homeAzimuth = ini.Get(section, "HomeAzimuth").ToSingle(_homeAzimuth);
                _homeElevation = ini.Get(section, "HomeElevation").ToSingle(_homeElevation);
                PreferGridTarget = ini.Get(section, "PreferGridTarget").ToBoolean(PreferGridTarget);
                RangeOverride = ini.Get(section, "RangeOverride").ToSingle(RangeOverride);
                AziSpeed = ini.Get(section, "AziSpeed").ToSingle(AziSpeed);
                EleSpeed = ini.Get(section, "EleSpeed").ToSingle(EleSpeed);
            }

            public void Write(char id, MyIni ini)
            {
                var section = id + "";
                ini.AddSection(section);
                ini.Set(section, "HomeAzimuth", _homeAzimuth);
                ini.Set(section, "HomeElevation", _homeElevation);
                ini.Set(section, "PreferGridTarget", PreferGridTarget);
                ini.Set(section, "RangeOverride", RangeOverride);
                ini.Set(section, "AziSpeed", AziSpeed);
                ini.Set(section, "EleSpeed", EleSpeed);
            }
        }
    }
}
