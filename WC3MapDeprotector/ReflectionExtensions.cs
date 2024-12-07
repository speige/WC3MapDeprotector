using System.Collections;

namespace WC3MapDeprotector
{
    public static class ReflectionExtensions
    {
        public static object ToGenericListOfType(this IList source, Type type)
        {
            Type listType = typeof(List<>).MakeGenericType(type);
            var constructor = listType.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                throw new InvalidOperationException($"The type {listType.FullName} does not have a parameterless constructor.");
            }

            object genericList = constructor.Invoke(null);
            var addMethod = listType.GetMethod("Add");
            foreach (var item in source)
            {
                addMethod.Invoke(genericList, new[] { item });
            }

            return genericList;
        }
    }
}