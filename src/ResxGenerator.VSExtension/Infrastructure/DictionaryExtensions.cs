using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResxGenerator.VSExtension.Infrastructure
{
    public static class DictionaryExtensions
    {
        public static V? GetValueOrDefault<K, V>(this Dictionary<K, V> dict, K key)
        {
            return dict.TryGetValue(key, out var value)
                ? value
                : default;
        }
    }
}
