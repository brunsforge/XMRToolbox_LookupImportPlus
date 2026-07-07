# Veröffentlichen & Aktualisieren

Wie das Plugin in den **XrmToolBox Tool Store** kommt und wie Updates funktionieren.

## Wie der Store funktioniert

Der XrmToolBox Tool Store ist kein eigener Upload-Ort. XrmToolBox **scannt nuget.org**
nach Paketen mit dem Tag `XrmToolBox Plugin` und listet sie. Du veröffentlichst also
nach **nuget.org**, und XrmToolBox holt es sich von dort.

```
dein Rechner / GitHub Actions ──push──> nuget.org ──scan/index──> Tool Store ──> Nutzer
```

Es gibt zwei Wege zum Push. **Empfohlen** von nuget.org ist inzwischen **Trusted
Publishing** (ohne gespeicherten API-Key); API-Keys funktionieren weiter, v. a. für den
lokalen CLI-Push.

---

## Weg A — Trusted Publishing (empfohlen, ohne API-Key)

Trusted Publishing veröffentlicht aus **GitHub Actions** per OIDC: der Workflow tauscht
ein kurzlebiges Token gegen einen temporären Push-Key. Es wird **kein Secret** im Repo
gespeichert. Der Workflow liegt bereits vor: `.github/workflows/release.yml`.

### Einmalige Einrichtung

1. **nuget.org-Konto** anlegen/anmelden (Microsoft- oder GitHub-Login):
   <https://www.nuget.org>.
2. **Trusted-Publishing-Policy** auf nuget.org anlegen — *Account → Trusted Publishing →
   Add*:
   - **Package owner:** `AndreasBrunsmann`
   - **Package:** `LookupImportPlus`  *(die nuget-Paket-ID, nicht der Repo-Name)*
   - **Repository owner:** `brunsforge`
   - **Repository:** `XMRToolbox_LookupImportPlus`
   - **Workflow file:** `release.yml`
   - **Environment:** leer lassen (oder eins setzen und im Workflow ergänzen)
3. **GitHub-Variable** setzen — Repo *Settings → Secrets and variables → Actions →
   Variables → New variable*:
   - `NUGET_USER` = dein nuget.org-Benutzername

> Für die **allererste** Veröffentlichung eines neuen Paket-Namens: nuget.org erlaubt das
> Anlegen über eine Policy, die auf deinen Owner zeigt. Klappt der erste Trusted-Publish
> wegen der noch nicht existierenden ID nicht, einmalig lokal per API-Key pushen
> (Weg B) — danach übernimmt Trusted Publishing alle Updates.

### Veröffentlichen / Aktualisieren

Einfach eine **Version taggen und pushen**:

```bash
git tag v0.1.1
git push origin v0.1.1
```

Der Workflow baut, packt und published automatisch. Alternativ **manuell** über
*GitHub → Actions → Release → Run workflow* mit Eingabe der Version.

---

## Weg B — API-Key (lokaler CLI-Push / nicht-unterstützte CI)

1. **API-Key** auf nuget.org erstellen — *Account → API Keys → Create*:
   - **Scope:** Push · **Glob Pattern:** `LookupImportPlus` · **Expiration** setzen.
   - Key sicher aufbewahren; für jedes Update wiederverwendbar.
2. Bauen, packen, pushen:

```powershell
build\release.ps1 0.1.1
dotnet nuget push deploy\LookupImportPlus.0.1.1.nupkg -s https://api.nuget.org/v3/index.json -k <API_KEY>
```

---

## Der Update-Mechanismus (gilt für beide Wege)

Versionen auf nuget.org sind **unveränderlich** — man überschreibt nie, sondern
veröffentlicht eine **höhere** Version. `build\release.ps1` (bzw. der Workflow) setzt die
Version an **einer** Stelle (`Directory.Build.props`); von dort erben die
**Assembly-Version** der `LookupImportPlus.dll` und die **Paket-Version**.

> **Wichtig:** XrmToolBox erkennt Updates über die **Assembly-Version** der installierten
> DLL im Vergleich zum Store. Deshalb muss die Version steigen — genau das macht der
> Release-Schritt. Nutzer mit älterer Version bekommen dann automatisch
> „Update verfügbar".

Nach nuget.org-Push dauert Indexierung wenige Minuten; im Store erscheint es beim
nächsten Refresh (bis zu ~24 h). Keine manuelle Freigabe.

## SemVer-Leitfaden

- **Patch** (`0.1.0 → 0.1.1`): Bugfix, keine Verhaltensänderung.
- **Minor** (`0.1.0 → 0.2.0`): neue, abwärtskompatible Funktion.
- **Major** (`0.1.0 → 1.0.0`): inkompatible Änderung.
- **Vorabversion:** `1.0.0-beta1`.

## Fehler passiert?

Eine veröffentlichte Version lässt sich nur **„unlisten"** (verstecken), nicht löschen.
Deshalb vor dem Push lokal testen: `deploy\Plugins\` nach
`%AppData%\MscrmTools\XrmToolBox\Plugins` kopieren und ausprobieren
(siehe [USAGE.md](USAGE.md)). Danach eine korrigierte höhere Version veröffentlichen.

## XrmToolBox-Store-Anforderungen (im Paket erfüllt)

Die Tool Library prüft mehr als nur den Tag. Das Paket (`build/pack/LookupImportPlus.nuspec`)
erfüllt bereits:

- **Tag** `XrmToolBox`,
- **`iconUrl`** (externes Logo) — Pflicht, sonst wird das Tool abgelehnt,
- **`<dependency id="XrmToolBox" …>`** — Marker für die Mindest-Host-Version,
- DLLs unter **`lib\net48\Plugins`**, plus `projectUrl`, `releaseNotes`, Lizenz.

> **Wichtig – Repo muss öffentlich sein:** `iconUrl` und `projectUrl` zeigen auf dein
> GitHub-Repo (`raw.githubusercontent.com/.../build/pack/icon.png`). Ist das Repo privat,
> kann XrmToolBox das Logo nicht laden → Ablehnung. Repo öffentlich schalten
> (GitHub → *Settings → General → Danger Zone → Change visibility → Make public*), oder
> das Icon auf einer anderen **öffentlichen** URL hosten und `iconUrl` in der nuspec anpassen.

Die Store-Metadaten kamen mit **0.1.1** dazu — die zuerst gepushte **0.1.0** enthält sie
nicht und würde vom Store abgelehnt. Also 0.1.1 veröffentlichen.

## Nur intern verteilen (ohne Store)

Kein öffentlicher Store gewünscht? Dann das Zip aus `deploy\` weitergeben — der Empfänger
entpackt es nach `%AppData%\MscrmTools\XrmToolBox\Plugins` und startet XrmToolBox neu.
Kein nuget.org nötig.
