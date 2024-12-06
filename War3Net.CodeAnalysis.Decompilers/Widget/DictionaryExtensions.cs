// ------------------------------------------------------------------------------
// <copyright file="MapUnitsDecompiler.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Collections.Generic;

namespace War3Net.CodeAnalysis.Decompilers
{
    public static class DictionaryExtensions
    {
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key) where TValue : new()
        {
            if (!dictionary.ContainsKey(key))
            {
                dictionary[key] = new TValue();
            }
            return dictionary[key];
        }
    }
}