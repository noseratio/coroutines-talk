// https://github.com/noseratio/coroutines-talk

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Coroutines
{
    public static class CoroutineCombinator<T>
    {
        public static IEnumerable<T> Combine(params Func<IEnumerable<T>>[] coroutines)
        {
            var list = coroutines.Select(c => c().GetEnumerator()).ToList();
            try
            {
                while (list.Count > 0)
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        var coroutine = list[i];
                        if (coroutine.MoveNext())
                        {
                            yield return coroutine.Current;
                        }
                        else
                        {
                            coroutine.Dispose();
                            list.RemoveAt(i);
                        }
                    }
                }
            }
            finally
            {
                list.ForEach(c => c.Dispose());
            }
        }
    }
}
