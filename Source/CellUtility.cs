﻿using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Noise;

namespace Celsius
{
    static class CellUtility
    {
        public static IEnumerable<IntVec3> AdjacentCells(this IntVec3 cell)
        {
            yield return cell + IntVec3.North;
            yield return cell + IntVec3.South;
            yield return cell + IntVec3.West;
            yield return cell + IntVec3.East;
        }

        /// <summary>
        /// Returns best guess for what kind of water terrain should be placed in a cell (if Ice melts there)
        /// </summary>
        public static TerrainDef BestUnderIceTerrain(this IntVec3 cell, Map map)
        {
            TerrainDef terrain = map.terrainGrid.UnderTerrainAt(cell), underTerrain;
            if (terrain != null)
                return terrain;

            bool foundGround = false;
            foreach (IntVec3 c in cell.AdjacentCells())
            {
                if (!c.InBounds(map))
                    continue;
                terrain = c.GetTerrain(map);
                if (terrain.IsWater)
                    return terrain;
                underTerrain = map.terrainGrid.UnderTerrainAt(c);
                if (underTerrain != null && underTerrain.IsWater)
                    return underTerrain;
                if (terrain != TerrainDefOf.Ice || (underTerrain != null && underTerrain != TerrainDefOf.Ice))
                    foundGround = true;
            }
            
            if (foundGround)
                return map.Biome == BiomeDefOf.SeaIce ? TerrainDefOf.WaterOceanShallow : TerrainDefOf.WaterShallow;
            return map.Biome == BiomeDefOf.SeaIce ? TerrainDefOf.WaterOceanDeep : TerrainDefOf.WaterDeep;
        }

        public static void FreezeTerrain(this IntVec3 cell, Map map, bool log = false)
        {
            TerrainDef terrain = cell.GetTerrain(map);
            if (log)
                LogUtility.Log($"{terrain} freezes at {cell}.");
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Ice);
            map.terrainGrid.SetUnderTerrain(cell, terrain);
        }

        public static void MeltTerrain(this IntVec3 cell, Map map, TerrainDef meltedTerrain, bool log = false)
        {
            // Removing things that can't stay on the melted terrain
            List<Thing> things = cell.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                Thing thing = things[i];
                TerrainAffordanceDef terrainAffordance = thing.TerrainAffordanceNeeded;
                if (terrainAffordance != null)
                    LogUtility.Log($"{thing.LabelCap}'s terrain affordance: {terrainAffordance}. {meltedTerrain.LabelCap} provides: {meltedTerrain.affordances.Select(def => def.defName).ToCommaList()}.");
                if (meltedTerrain.passability == Traversability.Impassable)
                    if (thing is Pawn pawn)
                    {
                        LogUtility.Log($"{pawn.LabelCap} sinks in {meltedTerrain.label} and dies.");
                        if (pawn.Faction != null && pawn.Faction.IsPlayer)
                            Find.LetterStack.ReceiveLetter($"{pawn.LabelShortCap} sunk", $"{pawn.NameShortColored} sunk in {meltedTerrain.label} when ice melted.", LetterDefOf.Death, new LookTargets(cell, map));
                        pawn.Kill(null);
                        pawn.Corpse.Destroy();
                    }
                    else
                    {
                        LogUtility.Log($"{thing.LabelCap} sinks in {meltedTerrain.label}.");
                        thing.Destroy();
                    }
                else if (thing is Building_Grave grave && grave.HasAnyContents)
                {
                    LogUtility.Log($"Grave with {grave.ContainedThing?.LabelShort} is uncovered due to melting.");
                    grave.EjectContents();
                    grave.Destroy();
                }
                else if (terrainAffordance != null && !meltedTerrain.affordances.Contains(terrainAffordance))
                {
                    LogUtility.Log($"{thing.LabelCap} can't stand on {meltedTerrain.label} and is destroyed.");
                    thing.Destroy();
                }
                else if (thing is Filth filth && !FilthMaker.TerrainAcceptsFilth(meltedTerrain, thing.def))
                {
                    LogUtility.Log($"Removing filth {thing.Label} from {meltedTerrain.label}.");
                    filth.Destroy();
                }
            }

            // Changing terrain
            if (map.terrainGrid.UnderTerrainAt(cell) == null)
                map.terrainGrid.SetUnderTerrain(cell, meltedTerrain);
            if (log)
                LogUtility.Log($"Ice melts at {cell} into {map.terrainGrid.UnderTerrainAt(cell)?.defName}.");
            map.terrainGrid.RemoveTopLayer(cell, false);
            map.snowGrid.SetDepth(cell, 0);
        }
    }
}
