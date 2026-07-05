using System.Windows.Forms;

namespace LookupImportPlus.UI
{
    /// <summary>
    /// Base class for the screen UserControls hosted in the content panel.
    /// Gives each screen access to the host (navigation + Dataverse service)
    /// and a hook to (re)load its data when it becomes visible.
    /// </summary>
    public abstract class ScreenControlBase : UserControl
    {
        protected IScreenHost Host { get; private set; }

        public void Attach(IScreenHost host)
        {
            Host = host;
            Dock = DockStyle.Fill;
        }

        /// <summary>
        /// Called by the shell each time the screen is shown. <paramref name="parameters"/>
        /// is whatever was passed to <see cref="IScreenHost.Navigate"/>.
        /// </summary>
        public virtual void OnActivated(object parameters)
        {
        }
    }
}
