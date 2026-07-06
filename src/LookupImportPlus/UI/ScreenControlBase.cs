using System.Windows.Forms;

namespace LookupImportPlus.UI
{
    /// <summary>
    /// Base class for screen UserControls hosted in the content panel. Gives each
    /// screen access to the host (navigation, container, background work) and a
    /// hook to (re)load its data when it becomes visible.
    /// </summary>
    public abstract class ScreenControlBase : UserControl
    {
        protected IScreenHost Host { get; private set; }

        public void Attach(IScreenHost host)
        {
            Host = host;
            Dock = DockStyle.Fill;
        }

        /// <summary>Called by the shell each time the screen is shown.</summary>
        public virtual void OnActivated(object parameters)
        {
        }
    }
}
