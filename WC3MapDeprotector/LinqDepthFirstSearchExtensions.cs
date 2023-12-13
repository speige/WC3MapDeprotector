namespace WC3MapDeprotector
{
    public static class LinqDepthFirstSearchExtensions
    {
        private static IEnumerable<T> AsEnumerable<T>(T item)
        {
            yield return item;
        }

        public static IEnumerable<T> DFS_Flatten<T>(this T source, Func<T, IEnumerable<T>> getChildren) where T : class
        {
            return AsEnumerable(source).DFS_Flatten(getChildren);
        }

        public static IEnumerable<T> DFS_Flatten<T>(this T source, Func<T, T> getChild) where T : class
        {
            return AsEnumerable(source).DFS_Flatten(getChild);
        }

        public static IEnumerable<T> DFS_Flatten<T>(this IEnumerable<T> source, Func<T, T> getChild) where T : class
        {
            return source.DFS_Flatten(x => AsEnumerable(getChild(x)));
        }

        public static IEnumerable<T> DFS_Flatten<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> getChildren) where T : class
        {
            if (source == null)
            {
                yield break;
            }

            foreach (T child in source)
            {
                if (child == null)
                {
                    continue;
                }

                yield return child;

                foreach (var subChild in getChildren(child).DFS_Flatten<T>(getChildren))
                {
                    yield return subChild;
                }
            }
        }
   }
}