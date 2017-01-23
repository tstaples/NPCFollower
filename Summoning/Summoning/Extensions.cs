using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Summoning
{
    public static class ListExtensions
    {
        public static void InsertAt<T>(this List<T> list, T element, Predicate<T> predicate)
        {
            int i = list.FindIndex(predicate);
            if (i == -1)
                list.Add(element);
            else
                list.Insert(i, element);
        }
    }
}
