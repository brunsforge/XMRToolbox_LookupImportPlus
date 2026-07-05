# LookupImportPlus — XrmToolBox Plugin

Auditierbarer Excel-Import in Dataverse als **XrmToolBox-Plugin** (WinForms, .NET Framework 4.8).
Lookups werden **deterministisch aufgelöst oder an einen Menschen eskaliert — nie geraten**.

Portierung der Power-Apps-Code-App
[brunsforge/LookupImportPlus](https://github.com/brunsforge/LookupImportPlus) auf das
Dataverse-**SDK** (statt Web API). Maßgebliche Logik-Spezifikation bleibt der dortige
Quellcode (`src/domain/*`, `src/services/*`).

## Kernregel

Feste Matching-Reihenfolge, erster Treffer gewinnt:

```
1) GUID-Spalte  →  2) Business Key  →  3) Suchfeld + Bedingungen
```

0 Treffer ⇒ NotFound · genau 1 ⇒ aufgelöst · mehrere ⇒ Mehrdeutig → Konfliktstrategie
(`escalate` / `skip` / `fail`).

## Projektstruktur

```
LookupImportPlus.sln
src/LookupImportPlus/
  LookupImportPlus.csproj        SDK-style, net462, WinForms
  Plugin.cs                      IXrmToolBoxPlugin-Factory (MEF-Export + Metadaten)
  LookupImportPlusControl.cs     PluginControlBase-Shell (Navigation + Content)
  Domain/                        Source-of-Truth-Typen (Config, Enums, Conditions, Issues)
  Services/                      MetadataService, LookupResolver (Kern)
  UI/                            IScreenHost, ScreenControlBase, Screens/*
```

## Screens (1:1 zur Code App)

`Configs` (Startseite) · `Editor` (Assistent, 4 Tabs) · `ImportRun` · `Conflicts` ·
`Resolve` · `History`. Siehe `handoff/xrmtoolbox-plugin-handoff.md` bzw. hier
`xrmtoolbox-plugin-handoff.md` für die vollständige Screen-für-Screen-Spezifikation.

## Build

Voraussetzungen: .NET SDK + .NET Framework 4.8 Targeting Pack (bzw. Visual Studio
mit .NET-Desktop-Workload). Der aktuelle XrmToolBox-Host verlangt net48.

```powershell
dotnet restore
dotnet build -c Release
```

Die erzeugte `LookupImportPlus.dll` (plus Nicht-Host-Abhängigkeiten wie ClosedXML) in
den XrmToolBox-`Plugins`-Ordner kopieren; der Host stellt SDK/Extensibility bereit.

## Umsetzungsstand

Gerüst steht (Plugin lädt, Navigation + Screen-Platzhalter, Domain-Modell,
MetadataService funktional, LookupResolver als dokumentiertes Skelett). Reihenfolge
für den Ausbau siehe §7 des Handoffs.
