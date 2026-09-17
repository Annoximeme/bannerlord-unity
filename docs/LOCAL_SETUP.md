# Local Dev Environment Setup (Windows)

**Why this matters for this project.** Early exploratory work happened in an isolated cloud
sandbox with **no access to local drives** — `G:\` was unreachable from it. Several audit
items (RISK-02, and investigations B6/B7/B8) need the real installation and, in some cases, a
running game. A local session can read `G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`
directly.

---

## 1. Prerequisites

| Requirement | Notes |
|---|---|
| Windows 10 1809+ or Windows Server 2019+ | 4 GB+ RAM, x64 or ARM64 |
| A coding-assistant CLI with filesystem and shell access | This project's docs and workflow assume one is available locally |
| **Git for Windows** | *Optional but recommended* — see §3 |
| Python 3.8+ | Only needed for `tools/apiscan/` |
| .NET SDK | Needed to build `src/` and for `ilspycmd` (§8) |

## 2. Native Windows vs WSL — use Native Windows

| Option | Verdict for this project |
|---|---|
| **Native Windows** | ✅ **Use this.** The game and the .NET toolchain are Windows-native, and `G:\` is directly readable. |
| WSL 2 | ❌ Adds a translation layer over `/mnt/g` and complicates running the Windows .NET build tools. |

## 3. Install Git for Windows first (recommended)

https://git-scm.com/downloads/win

It installs Git Credential Manager, which is what makes pushing to GitHub painless (§6), and
gives a Bash shell alongside PowerShell — the project's tooling and docs are written against
bash-style commands in places.

## 4. Get the project

```powershell
cd C:\dev                         # or wherever you keep projects
git clone https://github.com/Annoximeme/bannerlord-unity.git
cd bannerlord-unity
git checkout main
```

## 5. Pushing to GitHub — yes, this is easy

You own `Annoximeme/bannerlord-unity`, so you already have write access.

**First push:** Git Credential Manager (bundled with Git for Windows) opens a browser window,
you authorise once, and the credential is cached in Windows Credential Manager. Every later
push is silent.

```powershell
git push -u origin main
```

**Alternatives:** the GitHub CLI (`winget install GitHub.cli`, then `gh auth login`), or an SSH
key if you prefer.

> ⚠ **Never commit the game assemblies.** TaleWorlds DLLs are proprietary. `.gitignore`
> already excludes `*.dll`; leave that rule in place.

## 6. First thing to run locally

```powershell
python tools\apiscan\collect_install_report.py "G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
```

This fills in the version block that early exploratory work couldn't determine (see
`docs/VERSION_SUPPORT.md` §5a). Output lands in `docs/install-report/` as text and JSON — safe
to commit.

## 7. What a local session unlocks

| Blocked item | Why remote-sandbox work couldn't do it | Local |
|---|---|---|
| B2 — version pin | No access to `G:\` | ✅ Filesystem read |
| B6 — which mutations bump `Ship.VersionNo` | Reference assemblies have no method bodies | ✅ Real DLLs carry IL |
| B7 — `MapEvent` → naval mission launch path | Same | ✅ |
| B8 — siege stage transitions | Same | ✅ |
| B3 — `GameNetwork`-in-campaign probe (RISK-02) | Needs a running game | ✅ Requires launching Bannerlord |
| B9 — save compat with/without War Sails | Needs a running game | ✅ |

For IL-level work (B7, B8 — B6 is done), a decompiler helps: [ILSpy](https://github.com/icsharpcode/ILSpy) or `ilspycmd`. `tools/apiscan/cli_meta.py` reads type and member metadata but deliberately does not decode method bodies.

`dotnet tool install -g ilspycmd` pulls the latest release, which as of 2026-09-17 fails to install (`DotnetToolSettings.xml was not found in the package`). Pin an older release instead: `dotnet tool install -g ilspycmd --version 8.2.0.7535`. Point it at the real installed assemblies (not the reference-assembly NuGet packages, which have no method bodies), e.g.:

```powershell
ilspycmd -t "TaleWorlds.CampaignSystem.Naval.Ship" -r "G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client" "G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll"
```

**Never commit decompiled output.** It reproduces TaleWorlds' own source with real method bodies — legally a stricter case than the metadata-only surface dumps in `docs/evidence/`. Write findings from it into the docs in our own words instead; `.gitignore` blocks `*.decompiled.cs` and `/decompiled/` as a backstop.

## 8. Notes

- **Sandboxing is unavailable on native Windows**, so a local session asks permission for commands more often than a cloud sandbox would. Expected, not a fault.
- Keep to the `main` branch so history stays linear.
- If you work across multiple machines, `git pull` before starting to avoid divergence.
