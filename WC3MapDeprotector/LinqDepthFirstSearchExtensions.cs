namespace WC3MapDeprotector
{
    public static class LinqDepthFirstSearchExtensions
    {
        private static IEnumerable<T> AsEnumerable_Lazy<T>(T item)
        {
            yield return item;
        }

        public static IEnumerable<T> DFS_Flatten_Lazy<T>(this T source, Func<T, IEnumerable<T>> getChildren) where T : class
        {
            return AsEnumerable_Lazy(source).DFS_Flatten_Lazy(getChildren);
        }

        public static IEnumerable<T> DFS_Flatten_Lazy<T>(this T source, Func<T, T> getChild) where T : class
        {
            return AsEnumerable_Lazy(source).DFS_Flatten_Lazy(getChild);
        }

        public static IEnumerable<T> DFS_Flatten_Lazy<T>(this IEnumerable<T> source, Func<T, T> getChild) where T : class
        {
            return source.DFS_Flatten_Lazy(x => AsEnumerable_Lazy(getChild(x)));
        }

        public static IEnumerable<T> DFS_Flatten_Lazy<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> getChildren) where T : class
        {
            if (source == null)
            {
                yield break;
            }

            var stack = new Stack<IEnumerator<T>>();
            var enumerator = source.GetEnumerator();

            try
            {
                while (true)
                {
                    if (enumerator.MoveNext())
                    {
                        T element = enumerator.Current;
                        yield return element;

                        stack.Push(enumerator);
                        var nextSource = getChildren(element);
                        if (nextSource != null)
                        {
                            enumerator = nextSource.GetEnumerator();
                        }
                    }
                    else if (stack.Count > 0)
                    {
                        enumerator.Dispose();
                        enumerator = stack.Pop();
                    }
                    else
                    {
                        yield break;
                    }
                }
            }
            finally
            {
                enumerator.Dispose();

                while (stack.Count > 0)
                {
                    enumerator = stack.Pop();
                    enumerator.Dispose();
                }
            }
        }
    }
}