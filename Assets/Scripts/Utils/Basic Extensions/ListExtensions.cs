using System.Collections.Generic;
using UnityEngine;

namespace UnityExtensions
{
    public static class ListExtensions
    {
        // Add More than one element at a time to a List
        public static void AddMany<T>(this List<T> list, params T[] elements)
        {
            list.AddRange(elements);
        }

        //Randomply shuffle a list's elemnts' position
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int rng = Random.Range(0, list.Count);
                T value = list[rng];
                list[rng] = list[n];
                list[n] = value;
            }
        }

        // Gets a random element from a list
        public static T RandomElement<T>(this List<T> list)
        {
            return list[Random.Range(0, list.Count)];
        }
    }
}
