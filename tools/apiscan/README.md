# apiscan — ECMA-335 metadata reader

A dependency-free Python 3 reader for .NET assembly metadata (PE → CLI header → `#~` tables → `#Strings`/`#Blob` heaps), written for the Phase 0 audit so that TaleWorlds assemblies can be inspected **without a .NET toolchain, decompiler, or game install**.

It makes every API claim in `docs/` reproducible.

## What it reads

Types (with base type and interfaces), fields, properties (with accessor visibility), events, methods (with decoded signatures), custom attributes, nested types, and assembly references.

**Crucially it surfaces `[TaleWorlds.SaveSystem.SaveableFieldAttribute]`, `[SaveablePropertyAttribute]` and `[TaleWorlds.Library.CachedDataAttribute]`** — which is how the project derives its replicate/don't-replicate rule mechanically instead of by judgement (`docs/SYNCHRONIZATION_MODEL.md` §3).

## Limits

Method **bodies** are not read (reference assemblies do not contain them). Anything about control flow, ordering, or "when" something happens is therefore outside this tool's reach and is marked `UNCONFIRMED` in the docs.

## Usage

```bash
python3 cli_meta.py asminfo <dll>...            # assembly version, runtime, type count
python3 cli_meta.py types   <dll> [regex]       # list types (kind + full name)
python3 cli_meta.py type    <dll> <FullName>... # full member dump with attributes
python3 cli_meta.py grep    <regex> <dll>...    # search members across assemblies
python3 cli_meta.py attrs   <regex> <dll>...    # find members carrying an attribute
python3 cli_meta.py refs    <dll>               # assembly references
python3 cli_meta.py surface <dll>               # normalized listing, for diffing
```

## Getting assemblies

```bash
ID=bannerlord.referenceassemblies.core; V=1.4.8.119303
curl -sSL -o pkg.nupkg "https://api.nuget.org/v3-flatcontainer/$ID/$V/$ID.$V.nupkg"
unzip -q pkg.nupkg -d pkg     # assemblies land in pkg/ref/net472/
```

Packages: `core`, `native`, `sandbox`, `storymode`, `multiplayer`, `custombattle`, **`navaldlc`** (War Sails).

## Version-drift gate (Phase 1.3)

```bash
python3 cli_meta.py surface pinned/TaleWorlds.CampaignSystem.dll | sort -u > current.txt
comm -23 baseline.txt current.txt    # anything we bind to appearing here must fail CI
```
