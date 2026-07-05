using Microsoft.Xrm.Sdk;

namespace LookupImportPlus.UI
{
    /// <summary>
    /// Abstraction the screens use to navigate and to reach the host's
    /// authenticated <see cref="IOrganizationService"/> without depending on the
    /// concrete PluginControlBase. Mirrors navigate(screen, params) from the
    /// Code App's AppContext.
    /// </summary>
    public interface IScreenHost
    {
        /// <summary>Authenticated Dataverse connection provided by the host.</summary>
        IOrganizationService Service { get; }

        /// <summary>Base URL of the connected org, for record deep-links.</summary>
        string WebApplicationUrl { get; }

        /// <summary>Show <paramref name="screen"/>, optionally passing state.</summary>
        void Navigate(ScreenName screen, object parameters = null);
    }
}
