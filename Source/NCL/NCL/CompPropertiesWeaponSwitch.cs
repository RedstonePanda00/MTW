using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace NCL
{
    public class CompPropertiesWeaponSwitch : CompProperties
    {
        public List<TransformData> transformData = new List<TransformData>();
        public TransformData revertData;
        public List<string> sharedComps = new List<string>();
        private HashSet<Type> sharedCompsResolved;

        public CompPropertiesWeaponSwitch()
        {
            compClass = typeof(CompFormChange);
        }

        public HashSet<Type> SharedCompsResolved
        {
            get
            {
                if (sharedCompsResolved == null)
                    ResolveSharedComps();
                return sharedCompsResolved;
            }
        }

        public void ResolveSharedComps()
        {
            sharedCompsResolved ??= new HashSet<Type>();
            sharedCompsResolved.Clear();
            for (int i = 0; i < sharedComps.Count; i++)
            {
                Type t = TypeFromString(sharedComps[i]);
                if (t != null)
                    sharedCompsResolved.Add(t);
            }
        }

        public static Type TypeFromString(string typeString)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeString, false, true);
                if (type != null)
                    return type;
            }
            return Type.GetType(typeString, false, true);
        }
    }
}
