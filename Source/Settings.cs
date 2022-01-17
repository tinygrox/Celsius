﻿using Verse;

namespace Celsius
{
    public class Settings : ModSettings
    {
        public static bool ShowTemperatureMap;
        public static bool FreezingAndMeltingEnabled;
        public static bool AutoignitionEnabled;
        public static float AirHeatCapacity;
        public static float AirHeatConductivity = 0.03f;
        public static float HeatConductivityFactor;
        public static float ConvectionConductivityEffect;
        public static float HeatPushEffect;
        public static bool DebugMode = Prefs.LogVerbose;

        public const float AirHeatCapacity_Default = 1200;
        public const float ConvectionConductivityEffect_Default = 10;
        public const float HeatPushEffect_Base = 5;

        public Settings() => Reset();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShowTemperatureMap, "ShowTemperatureMap");
            Scribe_Values.Look(ref FreezingAndMeltingEnabled, "FreezingAndMeltingEnabled", true);
            Scribe_Values.Look(ref AutoignitionEnabled, "AutoignitionEnabled", true);
            Scribe_Values.Look(ref AirHeatCapacity, "AirHeatCapacity", AirHeatCapacity_Default);
            Scribe_Values.Look(ref HeatConductivityFactor, "HeatConductivityFactor", 1);
            Scribe_Values.Look(ref ConvectionConductivityEffect, "ConvectionConductivityEffect", ConvectionConductivityEffect_Default);
            Scribe_Values.Look(ref HeatPushEffect, "HeatPushEffect", HeatPushEffect_Base);
            Scribe_Values.Look(ref DebugMode, "DebugMode", forceSave: true);
        }

        public static void Reset()
        {
            ShowTemperatureMap = false;
            FreezingAndMeltingEnabled = true;
            AutoignitionEnabled = true;
            AirHeatCapacity = AirHeatCapacity_Default;
            HeatConductivityFactor = 1;
            ConvectionConductivityEffect = ConvectionConductivityEffect_Default;
            HeatPushEffect = HeatPushEffect_Base;
            TemperatureUtility.RecalculateAirProperties();
        }
    }
}
