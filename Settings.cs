using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    internal static class Settings
    {
        internal static List<IIniSetting> AllSettings = new List<IIniSetting>();

        public static IniSetting<bool> UseGridTarget = new IniSetting<bool>(
            "UseGridTarget",
            "Should the script consider grid (scroll wheel) target?",
            true);

        public static IniSetting<string> SourceWeaponGroup = new IniSetting<string>(
            "SourceWeaponGroup",
            "Name for terminal group the script should pull targeting info from. Optional.",
            "Rotor Turret Source");

        public static IniSetting<float> GridAimTolerance = new IniSetting<float>(
            "GridAimTolerance",
            "Multiplier for grid aim tolerance; based on grid WorldAABB.",
            1);

        public static IniSetting<float> BlockAimTolerance = new IniSetting<float>(
            "BlockAimTolerance",
            "Multiplier for block aim tolerance; based on block WorldAABB.",
            3);

        public const string IgnoreBlockTag = "[PestleIgnore]";

        public static Dictionary<char, Program.RotorTurretSettings> Read()
        {
            var ini = new MyIni();
            ini.TryParse(Program.I.Me.CustomData);

            foreach (var setting in AllSettings)
                setting.Read(ini, "General Config");

            var settings = new Dictionary<char, Program.RotorTurretSettings>();

            var iniSections = new List<string>();
            ini.GetSections(iniSections);
            foreach (var section in iniSections)
            {
                if (section == "General Config")
                    continue;
                settings[section[0]] = new Program.RotorTurretSettings(section, ini);
            }

            return settings;
        }

        public static void Write()
        {
            var ini = new MyIni();
            ini.AddSection("General Config");
            ini.SetSectionComment("General Config", " 'Pestle' Rotor Turret Manager Settings\n\n Set default values for turrets tagged with that section's letter below,\n   then recompile.\n Delete a line to reset it to default.\n ");
        
            foreach (var setting in AllSettings)
                setting.Write(ini, "General Config");

            foreach (var turret in Program.I.RotorTurrets)
            {
                turret.TurretSettings.Write(turret.Id, ini);
                ini.SetSectionComment("" + turret.Id, $" {turret.WeaponElevationMap.Count} elevation rotor(s) & {turret.WeaponElevationMap.Values.Sum(l => l.Count)} weapon(s)");
            }

            Program.I.Me.CustomData = ini.ToString();
        }
    }

    internal class IniSetting<TValue> : IIniSetting
    {
        public string Name;
        public string Description;
        public TValue Value;

        public IniSetting(string name, string description, TValue value)
        {
            Name = name;
            Description = description;
            Value = value;
            Settings.AllSettings.Add(this);
        }

        public void Write(MyIni ini, string section)
        {
            ini.Set(section, Name, Value.ToString());
            ini.SetComment(section, Name, Description);
        }

        public void Read(MyIni ini, string section)
        {
            if (Value is string)
                Value = (TValue) (object) ini.Get(section, Name).ToString((string) (object) Value);
            else if (Value is bool)
                Value = (TValue) (object) ini.Get(section, Name).ToBoolean((bool) (object) Value); // the devil has a name and it is keen software house
            else if (Value is byte)
                Value = (TValue) (object) ini.Get(section, Name).ToByte((byte) (object) Value);
            else if (Value is char)
                Value = (TValue) (object) ini.Get(section, Name).ToChar((char) (object) Value);
            else if (Value is decimal)
                Value = (TValue) (object) ini.Get(section, Name).ToDecimal((decimal) (object) Value);
            else if (Value is double)
                Value = (TValue) (object) ini.Get(section, Name).ToDouble((double) (object) Value);
            else if (Value is short)
                Value = (TValue) (object) ini.Get(section, Name).ToInt16((short) (object) Value);
            else if (Value is int)
                Value = (TValue) (object) ini.Get(section, Name).ToInt32((int) (object) Value);
            else if (Value is long)
                Value = (TValue) (object) ini.Get(section, Name).ToInt64((long) (object) Value);
            else if (Value is sbyte)
                Value = (TValue) (object) ini.Get(section, Name).ToSByte((sbyte) (object) Value);
            else if (Value is float)
                Value = (TValue) (object) ini.Get(section, Name).ToSingle((float) (object) Value);
            else if (Value is ushort)
                Value = (TValue) (object) ini.Get(section, Name).ToUInt16((ushort) (object) Value);
            else if (Value is uint)
                Value = (TValue) (object) ini.Get(section, Name).ToUInt32((uint) (object) Value);
            else if (Value is ulong)
                Value = (TValue) (object) ini.Get(section, Name).ToUInt64((ulong) (object) Value);
            else
                throw new Exception("Invalid setting TValue " + typeof(TValue).FullName);
        }

        public static implicit operator TValue(IniSetting<TValue> setting) => setting.Value;
    }

    internal interface IIniSetting
    {
        void Write(MyIni ini, string section);
        void Read(MyIni ini, string section);
    }
}
