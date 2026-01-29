using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace labyItems.Helpers;

public static class ReflectionHelper
{
    public static Type? FindEnumTypeByName(string enumName)
    {
        if (string.IsNullOrWhiteSpace(enumName))
            return null;

        foreach (var (assembly, type) in LoopHelper.Flatten(AppDomain.CurrentDomain.GetAssemblies(), GetTypesSafe))
        {
            if (type.IsEnum && string.Equals(type.Name, enumName, StringComparison.OrdinalIgnoreCase))
                return type;
        }

        return null;
    }

    private static IEnumerable<Type> GetTypesSafe(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).Cast<Type>();
        }
    }
}
