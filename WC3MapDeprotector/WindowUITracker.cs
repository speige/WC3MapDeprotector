using Jot;

namespace WC3MapDeprotector
{
    public static class WindowUITracker
    {
            public static Tracker Tracker = new Tracker();

            static WindowUITracker()
            {
                Tracker.Configure<Form>()
                    .Id(w => w.Name)
                    .Properties(w => new { w.Height, w.Width, w.Left, w.Top, w.WindowState })
                    .PersistOn(nameof(Form.FormClosed));
            }
    }
}
