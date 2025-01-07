using System.Collections.ObjectModel;

namespace WC3MapDeprotector
{
    public class DisposableCollection<T> : IDisposable where T : IDisposable
    {
        public ReadOnlyCollection<T> Collection {  get; }
        private bool _disposed = false;

        public DisposableCollection(ReadOnlyCollection<T> collection)
        {
            Collection = collection;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var disposable in Collection)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                }
            }

            _disposed = true;
        }
    }
}