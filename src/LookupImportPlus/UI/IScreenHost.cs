using LookupImportPlus.App;
using XrmToolBox.Extensibility;

namespace LookupImportPlus.UI
{
    /// <summary>
    /// What the screens use to reach the composition root, navigate, run
    /// long operations on the host's background worker, and raise notifications.
    /// Mirrors navigate(screen, params) + the container from the Code App's
    /// AppContext.
    /// </summary>
    public interface IScreenHost
    {
        /// <summary>Composition root; null until a connection is established.</summary>
        AppContainer Container { get; }

        /// <summary>Show <paramref name="screen"/>, optionally passing state.</summary>
        void Navigate(ScreenName screen, object parameters = null);

        /// <summary>Run a long operation on the host's background worker (never blocks the UI).</summary>
        void ExecuteWork(WorkAsyncInfo info);

        /// <summary>Balloon-tip desktop notification (import finished, …).</summary>
        void Notify(string title, string body);
    }
}
