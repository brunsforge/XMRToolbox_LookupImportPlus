using System;
using System.ComponentModel.Composition;
using System.Drawing;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace LookupImportPlus
{
    /// <summary>
    /// Factory that XrmToolBox discovers via MEF and uses to instantiate the
    /// plugin control. Also carries the metadata shown in the tool store /
    /// tool library (name, description, author, links).
    /// </summary>
    [Export(typeof(IXrmToolBoxPlugin))]
    [ExportMetadata("Name", "LookupImportPlus")]
    [ExportMetadata("Description",
        "Auditierbarer Excel-Import in Dataverse, bei dem Lookups deterministisch " +
        "aufgeloest oder an einen Menschen eskaliert werden - nie geraten.")]
    // BackgroundColor / PrimaryFontColor / SmallImageBase64 / BigImageBase64 can
    // be added later for store branding.
    public class Plugin : PluginBase, IGitHubPlugin, IHelpPlugin
    {
        public override IXrmToolBoxPluginControl GetControl()
        {
            return new LookupImportPlusControl();
        }

        // IGitHubPlugin -> "open source" badge + update checks. Points at THIS
        // plugin's repository (distinct from the original Code App source repo).
        public string RepositoryName => "XMRToolbox_LookupImportPlus";
        public string UserName => "brunsforge";

        // IHelpPlugin -> the "?" button in the plugin toolbar.
        public string HelpUrl => "https://github.com/brunsforge/XMRToolbox_LookupImportPlus";

        public Plugin()
        {
            // Reserved for one-time factory setup.
        }
    }
}
