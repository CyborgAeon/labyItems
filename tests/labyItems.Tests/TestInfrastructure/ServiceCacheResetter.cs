using System;
using System.Reflection;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using labyItems.Services.Specialisations;

namespace labyItems.Tests;

internal static class ServiceCacheResetter
{
    public static void ResetAll()
    {
        ResetField(typeof(SpellService), "_cache");
        ResetField(typeof(MiracleService), "_cache");
        ResetField(typeof(DruidEvocationService), "_cache");
        ResetField(typeof(EvolutionService), "_cache");
        ResetField(typeof(EvolutionService), "_abilityCache");
        ResetField(typeof(ClassService), "_cache");
        ResetField(typeof(GuildsService), "_cache");
        ResetField(typeof(PeopleService), "_cache");
        ResetField(typeof(LifeScalesService), "_cache");
        ResetField(typeof(SpecialisationService), "_cache");
        ResetField(typeof(SpecialisationDefinitionRepository), "_cache");
        ResetField(typeof(AbilityDetailsLookupService), "_lookup");
        ResetField(typeof(AbilityDefinitionLookupService), "_lookup");
        ResetField(typeof(AdvanceAbilitySearchVm), "_cachedAbilities");
        ResetField(typeof(AdvanceAbilitySearchVm), "_cachedAbilityLookup");
        ResetField(typeof(AdvanceAbilitySearchVm), "_cachedSourceBooks");
    }

    private static void ResetField(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        field?.SetValue(null, null);
    }
}
