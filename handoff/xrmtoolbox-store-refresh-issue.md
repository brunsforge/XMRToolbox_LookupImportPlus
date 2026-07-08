# Issue / message to the XrmToolBox maintainer — request Tool Library re-scan

**Where to post:** GitHub issue on `MscrmTools/XrmToolBox`, or the XrmToolBox
community Discord ("report a tool" / general).

---

**Title:** Tool Library catalog stuck on an old version for LookupImportPlus (shows 0.1.6, NuGet has 0.1.15)

**Body:**

Hi, and thanks for XrmToolBox!

The Tool Library / xrmtoolbox.com catalog is showing an outdated version for my
tool **LookupImportPlus** and doesn't seem to pick up new releases.

- **Catalog page:** https://www.xrmtoolbox.com/plugins/LookupImportPlus/ still
  shows **0.1.6** as the latest version.
- **NuGet:** the package is public, tagged `XrmToolBox`, and indexed. The latest
  listed version is **0.1.15** (older versions are unlisted):
  https://www.nuget.org/packages/LookupImportPlus
- Several intermediate releases (0.1.7, 0.1.8, 0.1.11, 0.1.13, 0.1.15) were
  published to NuGet over the past days but the catalog never advanced past 0.1.6.
- The **first** listing (0.1.6) worked fine, so tags/icon/dependency marker are
  correct — it's only the **version updates** that aren't being ingested.

Could you please trigger a re-scan / refresh of the Tool Library catalog for this
package? Happy to provide any additional details.

Thanks!
— brunsforge (owner: AndreasBrunsmann)

Repo: https://github.com/brunsforge/XMRToolbox_LookupImportPlus
