using System.Collections.Generic;
using Verse;

namespace NCL
{
  // Tracks buildings with CompSteelResource that still need hauling, so haul work givers avoid scanning all colonist buildings.
  public class MapComponent_SteelResourceAutofill : MapComponent
  {
    private readonly HashSet<Building> buildingsNeedingAutofill = new HashSet<Building>();
    private bool cacheBuilt;

    public MapComponent_SteelResourceAutofill(Map map)
      : base(map)
    {
    }

    public static MapComponent_SteelResourceAutofill Get(Map map)
    {
      return map?.GetComponent<MapComponent_SteelResourceAutofill>();
    }

    public static MapComponent_SteelResourceAutofill GetOrCreate(Map map)
    {
      if (map == null)
      {
        return null;
      }

      MapComponent_SteelResourceAutofill registry = map.GetComponent<MapComponent_SteelResourceAutofill>();
      if (registry == null)
      {
        registry = new MapComponent_SteelResourceAutofill(map);
        map.components.Add(registry);
      }

      if (!registry.cacheBuilt)
      {
        registry.RebuildCache();
        registry.cacheBuilt = true;
      }

      return registry;
    }

    public IReadOnlyCollection<Building> BuildingsNeedingAutofill => buildingsNeedingAutofill;

    public void NotifyChanged(CompSteelResource comp)
    {
      if (comp?.parent == null)
      {
        return;
      }

      Building building = comp.parent as Building;
      if (building == null)
      {
        return;
      }

      if (comp.NeedsAutofillWork)
      {
        buildingsNeedingAutofill.Add(building);
      }
      else
      {
        buildingsNeedingAutofill.Remove(building);
      }
    }

    public void Unregister(Building building)
    {
      if (building != null)
      {
        buildingsNeedingAutofill.Remove(building);
      }
    }

    private void RebuildCache()
    {
      buildingsNeedingAutofill.Clear();
      if (map == null)
      {
        return;
      }

      List<Building> buildings = map.listerBuildings.allBuildingsColonist;
      for (int i = 0; i < buildings.Count; i++)
      {
        Building building = buildings[i];
        CompSteelResource comp = building?.TryGetComp<CompSteelResource>();
        if (comp != null && comp.NeedsAutofillWork)
        {
          buildingsNeedingAutofill.Add(building);
        }
      }
    }

    public override void MapComponentTick()
    {
      base.MapComponentTick();

      if (map == null || buildingsNeedingAutofill.Count == 0)
      {
        return;
      }

      if (Find.TickManager.TicksGame % 300 != 0)
      {
        return;
      }

      PruneInvalidEntries();
    }

    private void PruneInvalidEntries()
    {
      buildingsNeedingAutofill.RemoveWhere(building =>
      {
        if (building == null || building.Destroyed || !building.Spawned)
        {
          return true;
        }

        CompSteelResource comp = building.TryGetComp<CompSteelResource>();
        return comp == null || !comp.NeedsAutofillWork;
      });
    }
  }
}
