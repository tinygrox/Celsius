using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace Celsius
{
    static class TemperatureUtility
    {
        public const float TemperatureChangePrecision = 0.01f;
        public const float MinFreezingTemperature = -3;

        public static TemperatureInfo TemperatureInfo(this Map map) => map.GetComponent<TemperatureInfo>();

        #region TEMPERATURE

        public static float GetTemperatureForCell(this IntVec3 cell, Map map)
        {
            TemperatureInfo tempInfo = map.TemperatureInfo();
            if (tempInfo == null)
                return map.mapTemperature.OutdoorTemp;
            return tempInfo.GetTemperatureForCell(cell);
        }

        public static float GetTemperature(this Room room)
        {
            if (room == null)
            {
                LogUtility.Log($"Trying to GetTemperature for null room!", LogLevel.Error);
                return TemperatureTuning.DefaultTemperature;
            }
            TemperatureInfo temperatureInfo = room.Map?.TemperatureInfo();
            if (temperatureInfo == null)
            {
                LogUtility.Log($"TemperatureInfo unavailable for {room.Map}.", LogLevel.Error);
                return room.Temperature;
            }
            if (room.TouchesMapEdge)
                return room.Map.mapTemperature.OutdoorTemp;
            return room.Cells.Average(cell => temperatureInfo.GetTemperatureForCell(cell));
        }

        #endregion TEMPERATURE

        #region DIFFUSION

        static float airConductivity;

        static float airLerpFactor;

        internal static void RecalculateAirProperties()
        {
            airConductivity = Settings.AirHeatConductivity * Settings.HeatConductivityFactor * Settings.ConvectionConductivityEffect;
            airLerpFactor = Mathf.Min(1 - Mathf.Pow(1 - airConductivity / Settings.AirHeatCapacity, Celsius.TemperatureInfo.SecondsPerUpdate), 0.25f);
            LogUtility.Log($"Air conductivity: {airConductivity:F2}. Air lerp factor: {airLerpFactor:P1}.");
        }

        internal static float DiffusionTemperatureChangeSingle(float oldTemp, float neighbourTemp, float capacity, float conductivity, bool log = false)
        {
            if (Mathf.Abs(oldTemp - neighbourTemp) < TemperatureChangePrecision)
                return 0;
            float finalTemp = (oldTemp + neighbourTemp) / 2;
            float effectiveConductivity, lerpFactor;
            if (capacity == Settings.AirHeatCapacity && conductivity == Settings.AirHeatConductivity)
            {
                effectiveConductivity = airConductivity;
                lerpFactor = airLerpFactor;
            }
            else
            {
                effectiveConductivity = conductivity * Settings.HeatConductivityFactor;
                lerpFactor = Mathf.Min(1 - Mathf.Pow(1 - effectiveConductivity / capacity, Celsius.TemperatureInfo.SecondsPerUpdate), 0.25f);
            }

            if (log)
            {
                LogUtility.Log($"Old temperature: {oldTemp:F1}C. Neighbour temperature: {neighbourTemp:F1}C. Heat capacity: {capacity}. Conductivity: {effectiveConductivity}.");
                LogUtility.Log($"Final temperature: {finalTemp:F1}C. Lerp factor: {lerpFactor:P1}.");
            }

            return lerpFactor * (finalTemp - oldTemp);
        }

        internal static (float, float) DiffusionTemperatureChangeMutual(float temp1, float capacity1, float conductivity1, float temp2, float capacity2, float conductivity2, bool log = false)
        {
            if (Mathf.Abs(temp1 - temp2) < TemperatureChangePrecision)
                return (0, 0);
            float finalTemp = GenMath.WeightedAverage(temp1, capacity1, temp2, capacity2);
            float conductivity, lerpFactor1, lerpFactor2;

            if (capacity1 == Settings.AirHeatCapacity && capacity2 == Settings.AirHeatCapacity && conductivity1 == Settings.AirHeatConductivity && conductivity2 == Settings.AirHeatConductivity)
            {
                conductivity = airConductivity;
                lerpFactor1 = lerpFactor2 = airLerpFactor;
            }

            //else if (props1 == props2)
            //{
            //    LogUtility.Log($"Both objects have the same thermal props: {props1}");
            //    conductivity = props1.conductivity * Settings.HeatConductivityFactor;
            //    lerpFactor1 = Mathf.Min(lerpFactor2 = 1 - Mathf.Pow(1 - conductivity / props1.heatCapacity, Celsius.TemperatureInfo.SecondsPerUpdate), 0.25f);
            //}

            else
            {
                conductivity = Mathf.Sqrt(conductivity1 * conductivity2) * Settings.HeatConductivityFactor;
                lerpFactor1 = Mathf.Min(1 - Mathf.Pow(1 - conductivity / capacity1, Celsius.TemperatureInfo.SecondsPerUpdate), 0.25f);
                lerpFactor2 = Mathf.Min(1 - Mathf.Pow(1 - conductivity / capacity2, Celsius.TemperatureInfo.SecondsPerUpdate), 0.25f);
            }

            if (log)
            {
                LogUtility.Log($"Object 1: t = {temp1:F1}C, capacity = {capacity1}, conductivity = {conductivity1}");
                LogUtility.Log($"Object 2: t = {temp2:F1}C, capacity = {capacity2}, conductivity = {conductivity2}");
                LogUtility.Log($"Final temperature: {finalTemp:F1}C. Overall conductivity: {conductivity:F1}. Lerp factor 1: {lerpFactor1:P1}. Lerp factor 2: {lerpFactor2:P1}.");
            }

            return (lerpFactor1 * (finalTemp - temp1), lerpFactor2 * (finalTemp - temp2));
        }

        #endregion DIFFUSION

        #region THERMAL PROPERTIES

        public static Thing GetCellThermalThing(this IntVec3 cell, Map map) =>
            cell.InBounds(map) ? cell.GetThingList(map).Find(thing => thing.GetStatValue(DefOf.HeatCapacity) > 0) : null;

        public static (float heatCapacity, float heatConductivity) GetThermalProperties(this IntVec3 cell, Map map)
        {
            Thing thing = cell.GetCellThermalThing(map);
            return thing != null ? (thing.GetStatValue(DefOf.HeatCapacity), thing.GetStatValue(DefOf.HeatConductivity)) : (Settings.AirHeatCapacity, Settings.AirHeatConductivity);
        }

        /// <summary>
        /// Returns heat capacity for a cell
        /// </summary>
        public static float GetHeatCapacity(this IntVec3 cell, Map map)
        {
            Thing thing = cell.GetCellThermalThing(map);
            return thing != null ? thing.GetStatValue(DefOf.HeatCapacity) : Settings.AirHeatCapacity;
        }

        public static float GetHeatConductivity(this IntVec3 cell, Map map)
        {
            Thing thing = cell.GetCellThermalThing(map);
            return thing != null ? thing.GetStatValue(DefOf.HeatConductivity) : Settings.AirHeatConductivity;
        }

        public static ThingDef GetUnderlyingStuff(this Thing thing) => thing.Stuff ?? thing.def.defaultStuff;

        #endregion THERMAL PROPERTIES

        #region TERRAIN

        //public static ThingThermalProperties GetTerrainThermalProperties(this IntVec3 cell, Map map) =>
        //    cell.GetTerrain(map).GetModExtension<ThingThermalProperties>() ?? ThingThermalProperties.Empty;

        public static bool HasTerrainTemperature(this IntVec3 cell, Map map) => cell.GetTerrain(map).StatBaseDefined(DefOf.HeatCapacity);

        public static float FreezingPoint(this TerrainDef water)
        {
            switch (water.defName)
            {
                case "WaterOceanDeep":
                case "WaterOceanShallow":
                    return -2;

                case "WaterMovingChestDeep":
                    return -3;

                case "WaterMovingShallow":
                    return -2;
            }
            return 0;
        }

        #endregion TERRAIN

        #region HEAT PUSH

        public static bool TryPushHeat(IntVec3 cell, Map map, float energy)
        {
            if (UI.MouseCell() == cell || energy < 0)
                LogUtility.Log($"Pushing {energy} heat at {cell}.");
            TemperatureInfo temperatureInfo = map.TemperatureInfo();
            if (temperatureInfo == null)
            {
                LogUtility.Log($"TemperatureInfo for {map} unavailable!");
                return false;
            }
            temperatureInfo.SetTempteratureForCell(cell, temperatureInfo.GetTemperatureForCell(cell) + energy * GenTicks.TicksPerRealSecond * Settings.HeatPushEffect / cell.GetHeatCapacity(map));
            return true;
        }

        #endregion HEAT PUSH
    }
}
