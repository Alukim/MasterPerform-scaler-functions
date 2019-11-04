using System.Collections.Generic;

namespace MasterPerform.Utilities
{
    public static class CollectionHelpers
    {
        public static void ShiftLeft<T>(this List<T> collection)
        {
            for (var i = 0; i < collection.Count - 1; ++i)
                collection[i] = collection[i + 1];
        }
    }
}