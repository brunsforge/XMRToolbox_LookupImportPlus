# LookupImportPlus

An **XrmToolBox plugin** for auditable Excel import into Dataverse, where
**lookups are resolved deterministically or escalated to a human — never
guessed**.

## Core rule

Fixed matching order, first hit wins:

```
1) GUID column  →  2) Business Key  →  3) Search field + conditions
```

0 hits ⇒ Not found · exactly 1 ⇒ Resolved · several ⇒ Ambiguous →
conflict strategy (escalate / skip / fail).

## Features

- Versioned job configurations; every run stores an immutable snapshot.
- Round-trip: configure → export (template/data) → edit in Excel → import
  (upload → schema-drift check → dry run → resolve conflicts → commit).
- Polymorphic lookups (e.g. Customer = Account|Contact) with search field /
  business key / conditions per target table.
- Conflict audit: every decision is logged (rule, candidates, chosen GUID,
  user, timestamp).
- Bulk writes via `ExecuteMultipleRequest`.
- Bilingual UI (English/German) — follows the Windows/UI culture, with a
  manual switch in the sidebar.

## Installation

Install from the **Tool Library** in XrmToolBox (search for "LookupImportPlus"),
or copy the DLLs into the `Plugins` folder manually.

Project & docs: <https://github.com/brunsforge/XMRToolbox_LookupImportPlus>
