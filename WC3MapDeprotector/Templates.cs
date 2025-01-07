using System.Reflection;

namespace WC3MapDeprotector
{
    public static class Templates
    {
        public static string pcall_wrapper_lua = GetEmbeddedResource("WC3MapDeprotector.Templates.pcall_wrapper.lua");

        private static string GetEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return null;
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}