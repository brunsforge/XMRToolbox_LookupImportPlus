# Veröffentlichen & Aktualisieren

Wie das Plugin in den **XrmToolBox Tool Store** kommt und wie Updates funktionieren.

## Wie der Store funktioniert

Der XrmToolBox Tool Store ist kein eigener Upload-Ort. XrmToolBox **scannt nuget.org**
nach Paketen mit dem Tag `XrmToolBox Plugin` und listet sie. Du veröffentlichst also
nach **nuget.org**, und XrmToolBox holt es sich von dort.

```
dein Rechner ──push──> nuget.org ──scan/index──> XrmToolBox Tool Store ──> Nutzer
```

## Was du brauchst (einmalig)

1. **nuget.org-Konto** – kostenlos, Anmeldung mit Microsoft- oder GitHub-Konto:
   <https://www.nuget.org>.
2. **NuGet-API-Key** – auf nuget.org unter *Account → API Keys → Create*:
   - **Scope:** Push
   - **Glob Pattern:** `LookupImportPlus` (oder `*`)
   - **Expiration:** setzen (max. 365 Tage)
   - Den Key **sicher aufbewahren** – du brauchst ihn für jedes Update wieder.

Ein separates XrmToolBox-Konto ist **nicht** nötig. Kein manueller Review – die Listung
erfolgt automatisch anhand des Tags.

## Erstveröffentlichung

```powershell
build\release.ps1 0.1.0
dotnet nuget push deploy\LookupImportPlus.0.1.0.nupkg -s https://api.nuget.org/v3/index.json -k <API_KEY>
```

- Der erste Push unter der ID `LookupImportPlus` **reserviert dir diese ID** (an dein
  Konto gebunden).
- nuget.org indexiert das Paket in wenigen Minuten; im XrmToolBox-Store erscheint es beim
  nächsten Refresh (bis zu ~24 h).

## Aktualisieren (neue Version)

Versionen auf nuget.org sind **unveränderlich** – man überschreibt nie, sondern
veröffentlicht eine **höhere** Version.

```powershell
build\release.ps1 0.1.1
dotnet nuget push deploy\LookupImportPlus.0.1.1.nupkg -s https://api.nuget.org/v3/index.json -k <API_KEY>
git commit -am "Release 0.1.1"  ;  git tag v0.1.1
```

`build\release.ps1` setzt die Version an **einer** Stelle (`Directory.Build.props`) – von
dort erben sowohl die **Assembly-Version** der `LookupImportPlus.dll` als auch die
**Paket-Version**.

> **Wichtig:** XrmToolBox erkennt Updates, indem es die **Assembly-Version** der
> installierten DLL mit der im Store vergleicht. Deshalb muss die Version steigen –
> genau das erledigt das Release-Skript. Nutzer mit älterer Version bekommen dann
> automatisch „Update verfügbar".

## SemVer-Leitfaden

- **Patch** (`0.1.0 → 0.1.1`): Bugfix, keine Verhaltensänderung.
- **Minor** (`0.1.0 → 0.2.0`): neue, abwärtskompatible Funktion.
- **Major** (`0.1.0 → 1.0.0`): inkompatible Änderung.
- **Vorabversion:** `1.0.0-beta1` (SemVer-Suffix ist erlaubt).

## Fehler passiert?

Eine bereits veröffentlichte Version lässt sich auf nuget.org nur **„unlisten"**
(verstecken), **nicht löschen**. Deshalb vor dem Push immer lokal testen:
`deploy\Plugins\` in `%AppData%\MscrmTools\XrmToolBox\Plugins` kopieren und ausprobieren
(siehe [USAGE.md](USAGE.md)). Danach eine korrigierte höhere Version veröffentlichen.

## Nur intern verteilen (ohne Store)

Du willst gar nicht in den öffentlichen Store? Dann einfach das Zip aus `deploy\`
weitergeben – der Empfänger entpackt es nach
`%AppData%\MscrmTools\XrmToolBox\Plugins` und startet XrmToolBox neu. Kein nuget.org nötig.
