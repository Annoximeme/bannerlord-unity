#!/usr/bin/env python3
"""
collect_install_report.py — Bannerlord / War Sails installation audit collector.

Run this ON THE MACHINE WHERE BANNERLORD IS INSTALLED.
Requires only Python 3 (3.8+). No .NET, no Steam API, no third-party packages.

It answers the questions the Phase 0 audit could not answer without an install:
  * exact installed Bannerlord version
  * stable vs beta branch
  * whether War Sails (NavalDLC) is installed, and its build
  * the exact module set
  * the real API surface of the installed assemblies

It writes TEXT AND JSON ONLY. It never copies game assemblies, so the output is
safe to commit or paste. TaleWorlds assemblies are proprietary — do not commit
the DLLs themselves.

USAGE (Windows PowerShell / cmd):
    python tools\\apiscan\\collect_install_report.py "G:\\SteamLibrary\\steamapps\\common\\Mount & Blade II Bannerlord"

USAGE (Linux/macOS):
    python3 tools/apiscan/collect_install_report.py "/path/to/Mount & Blade II Bannerlord"

Output goes to docs/install-report/ by default (override with --out).
"""
import argparse, json, os, re, sys, platform
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

try:
    from cli_meta import Asm
except Exception as e:                                    # pragma: no cover
    print("ERROR: could not import cli_meta.py from %s (%s)" % (HERE, e))
    sys.exit(2)

import xml.etree.ElementTree as ET

BIN = os.path.join("bin", "Win64_Shipping_Client")

# Steam BetaKey values that denote the live/default branch rather than a beta
# opt-in. Bannerlord writes "public" here for the normal branch.
PUBLIC_BRANCH_KEYS = {"", "public", "none", "default"}

# Assemblies the co-op project binds to. Surface dumps are produced for these.
KEY_ASSEMBLIES = [
    "TaleWorlds.CampaignSystem.dll",
    "TaleWorlds.Core.dll",
    "TaleWorlds.Library.dll",
    "TaleWorlds.ObjectSystem.dll",
    "TaleWorlds.SaveSystem.dll",
    "TaleWorlds.MountAndBlade.dll",
    "TaleWorlds.Network.dll",
    "TaleWorlds.ModuleManager.dll",
    "TaleWorlds.Localization.dll",
    "SandBox.dll",
    "StoryMode.dll",
    "NavalDLC.dll",
]

def _val(node):
    if node is None:
        return None
    return node.get("value") or (node.text.strip() if node.text else None)

def read_submodule(xml_path):
    """Parse a Modules/<name>/SubModule.xml."""
    out = {"path": str(xml_path)}
    try:
        root = ET.parse(str(xml_path)).getroot()
    except Exception as e:
        out["error"] = "parse failed: %s" % e
        return out
    for tag in ("Id", "Name", "Version", "ModuleCategory", "ModuleType",
                "DefaultModule", "Official"):
        v = _val(root.find(tag))
        if v is not None:
            out[tag] = v
    deps = []
    for holder in ("DependedModules", "DependedModuleMetadatas"):
        parent = root.find(holder)
        if parent is None:
            continue
        for d in parent:
            deps.append({k: v for k, v in d.attrib.items()})
    if deps:
        out["dependencies"] = deps
    subs = []
    parent = root.find("SubModules")
    if parent is not None:
        for sm in parent:
            entry = {}
            for tag in ("Name", "DLLName", "SubModuleClassType"):
                v = _val(sm.find(tag))
                if v is not None:
                    entry[tag] = v
            if entry:
                subs.append(entry)
    if subs:
        out["subModules"] = subs
    return out

def find_steam_manifest(root: Path):
    """steamapps/common/<game> -> steamapps/appmanifest_261550.acf (Bannerlord appid)."""
    candidates = []
    p = root
    for _ in range(4):
        p = p.parent
        if p.name.lower() == "steamapps" or (p / "appmanifest_261550.acf").exists():
            candidates.append(p / "appmanifest_261550.acf")
    for c in candidates:
        if c.exists():
            return c
    return None

def parse_acf(path: Path):
    """Minimal VDF scrape: buildid and any BetaKey."""
    out = {"path": str(path)}
    try:
        txt = path.read_text(encoding="utf-8", errors="replace")
    except Exception as e:
        out["error"] = str(e)
        return out
    for key in ("appid", "name", "buildid", "LastUpdated", "SizeOnDisk",
                "installdir", "StateFlags"):
        m = re.search(r'"%s"\s+"([^"]*)"' % re.escape(key), txt, re.I)
        if m:
            out[key] = m.group(1)
    betas = re.findall(r'"BetaKey"\s+"([^"]*)"', txt, re.I)
    keys = sorted({b for b in betas if b})
    out["betaKeys"] = keys
    # A BetaKey naming the live branch is NOT a beta opt-in. Steam writes the
    # default branch either as an absent/empty key or literally as "public".
    real = [b for b in keys if b.lower() not in PUBLIC_BRANCH_KEYS]
    out["optedIntoBeta"] = bool(real)
    if real:
        out["steamBranch"] = real[0]
    elif keys:
        out["steamBranch"] = "%s (default/live branch)" % keys[0]
    else:
        out["steamBranch"] = "(no BetaKey - default/live branch)"
    return out

def scan_assembly(path: Path):
    try:
        a = Asm(str(path))
        info = a.assembly_info()
        info["typeCount"] = a.nrows(0x02)
        info["file"] = path.name
        info["fullPath"] = str(path)
        try:
            info["size"] = path.stat().st_size
        except Exception:
            pass
        return info, a
    except Exception as e:
        return {"file": path.name, "fullPath": str(path), "error": str(e)}, None

def locate(root: Path, dll: str):
    """Find a dll in core bin or any module bin."""
    core = root / BIN / dll
    if core.exists():
        return core
    mods = root / "Modules"
    if mods.is_dir():
        for m in sorted(mods.iterdir()):
            c = m / BIN / dll
            if c.exists():
                return c
    return None

def main():
    ap = argparse.ArgumentParser(description="Collect a Bannerlord/War Sails install report.")
    ap.add_argument("install", nargs="?",
                    default=r"G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord",
                    help="Path to the Bannerlord installation folder")
    ap.add_argument("--out", default=None, help="Output directory (default: docs/install-report)")
    ap.add_argument("--no-surface", action="store_true",
                    help="Skip the (large) API surface dumps")
    args = ap.parse_args()

    root = Path(args.install)
    out_dir = Path(args.out) if args.out else (HERE.parent.parent / "docs" / "install-report")
    out_dir.mkdir(parents=True, exist_ok=True)

    report = {
        "collectedOn": platform.node(),
        "collectorPlatform": platform.platform(),
        "pythonVersion": sys.version.split()[0],
        "installPath": str(root),
        "installExists": root.is_dir(),
    }

    print("=" * 70)
    print("Bannerlord / War Sails install report")
    print("=" * 70)
    print("Install path : %s" % root)

    if not root.is_dir():
        print("\nERROR: that path is not a directory on this machine.")
        print("Pass the correct path as the first argument.")
        report["error"] = "install path not found"
        (out_dir / "install-report.json").write_text(json.dumps(report, indent=2))
        return 1

    # ---- 1. Steam branch (stable vs beta) -------------------------------
    man = find_steam_manifest(root)
    if man:
        report["steam"] = parse_acf(man)
        print("Steam buildid: %s" % report["steam"].get("buildid", "?"))
        print("Steam branch : %s" % report["steam"].get("steamBranch", "?"))
    else:
        report["steam"] = {"note": "appmanifest_261550.acf not found (non-Steam install?)"}
        print("Steam manifest: not found")

    # ---- 2. Modules ------------------------------------------------------
    modules, naval = [], None
    mods_dir = root / "Modules"
    if mods_dir.is_dir():
        for m in sorted(mods_dir.iterdir()):
            sm = m / "SubModule.xml"
            if sm.exists():
                info = read_submodule(sm)
                info["dir"] = m.name
                dlls = sorted(x.name for x in (m / BIN).glob("*.dll")) if (m / BIN).is_dir() else []
                info["assemblies"] = dlls
                modules.append(info)
                if m.name.lower() in ("navaldlc", "warsails") or any(
                        d.lower().startswith("navaldlc") for d in dlls):
                    naval = info
    report["modules"] = modules
    report["moduleCount"] = len(modules)
    report["warSailsInstalled"] = naval is not None
    report["warSailsModule"] = naval

    print("\nModules found: %d" % len(modules))
    for mi in modules:
        print("  %-28s %s" % (mi.get("dir", "?"), mi.get("Version", "(no version)")))

    # ---- 3. Game version from assemblies --------------------------------
    print("\nKey assemblies:")
    asms, surfaces = [], {}
    for dll in KEY_ASSEMBLIES:
        p = locate(root, dll)
        if not p:
            asms.append({"file": dll, "missing": True})
            print("  %-42s MISSING" % dll)
            continue
        info, a = scan_assembly(p)
        asms.append(info)
        fv = info.get("attrs", {}).get("AssemblyFileVersionAttribute", "?")
        iv = info.get("attrs", {}).get("AssemblyInformationalVersionAttribute", "")
        print("  %-42s file=%-16s info=%-14s types=%s"
              % (dll, fv, iv, info.get("typeCount", "?")))
        if a is not None and not args.no_surface:
            surfaces[dll] = p
    report["assemblies"] = asms

    # Best-effort overall game version
    ver_votes = {}
    for info in asms:
        fv = info.get("attrs", {}).get("AssemblyFileVersionAttribute")
        if fv and fv != "1.0.0.0":
            ver_votes[fv] = ver_votes.get(fv, 0) + 1
    report["assemblyFileVersionVotes"] = ver_votes
    native = next((m for m in modules if m.get("dir", "").lower() == "native"), None)
    report["nativeModuleVersion"] = native.get("Version") if native else None

    game_version = report["nativeModuleVersion"] or (
        max(ver_votes, key=ver_votes.get) if ver_votes else None)
    report["resolvedGameVersion"] = game_version

    # Channel: the 'e' prefix means EarlyAccess, 'v' means Release (ApplicationVersion.GetPrefix)
    channel = None
    if game_version:
        m = re.match(r'^([a-zA-Z])', game_version.strip())
        if m:
            channel = {"v": "Release (stable)", "e": "EarlyAccess",
                       "b": "Beta", "a": "Alpha", "d": "Development"}.get(m.group(1).lower())
    report["versionPrefixChannel"] = channel

    # Two INDEPENDENT signals for stable-vs-beta. They can legitimately disagree
    # (a Steam beta branch may ship a 'v'-prefixed build). Report both; never
    # silently reconcile them -- CLAUDE.md forbids silently targeting a version.
    steam_branch = report.get("steam", {}).get("steamBranch")
    on_steam_beta = bool(report.get("steam", {}).get("optedIntoBeta"))
    prefix_stable = (channel == "Release (stable)")
    if steam_branch is None:
        assessment = "UNDETERMINED (no Steam manifest)"
    elif on_steam_beta and prefix_stable:
        assessment = ("CONFLICTING SIGNALS: Steam is opted into branch '%s' but the "
                      "version prefix indicates Release. Confirm in-game (the main "
                      "menu shows the version string) before pinning."
                      % steam_branch)
    elif on_steam_beta:
        assessment = "BETA (Steam branch '%s')" % steam_branch
    elif prefix_stable:
        assessment = "STABLE (public branch, Release-prefixed version)"
    else:
        assessment = "UNDETERMINED (public branch, version prefix %r)" % (channel,)
    report["branchAssessment"] = assessment
    print("\nBranch assessment: %s" % assessment)

    # ---- 4. War Sails build ---------------------------------------------
    nav_dll = locate(root, "NavalDLC.dll")
    if nav_dll:
        info, _ = scan_assembly(nav_dll)
        report["navalDlcAssembly"] = info
        print("\nWar Sails (NavalDLC): INSTALLED")
        print("  module version : %s" % (naval.get("Version") if naval else "?"))
        print("  assembly file  : %s" % info.get("attrs", {}).get("AssemblyFileVersionAttribute", "?"))
    else:
        print("\nWar Sails (NavalDLC): NOT FOUND")

    # ---- 5. Surface dumps ------------------------------------------------
    if surfaces:
        sdir = out_dir / "surface"
        sdir.mkdir(exist_ok=True)
        print("\nWriting API surface dumps to %s ..." % sdir)
        import subprocess
        for dll, p in surfaces.items():
            try:
                a = Asm(str(p))
                lines = []
                for rid in range(1, a.nrows(0x02) + 1):
                    td = a.row(0x02, rid)
                    fn = a.typename.get(rid, "?")
                    if fn == "<Module>":
                        continue
                    lines.append("T %s" % fn)
                    (f0, f1), (m0, m1) = a.type_members(rid)
                    p0, p1 = a.type_props(rid)
                    e0, e1 = a.type_events(rid)
                    for x in range(m0, m1):
                        r = a.row(0x06, x)
                        if r:
                            ret, ps = a.methodsig(a.blob(r["Signature"]))
                            lines.append("M %s::%s(%s):%s" % (fn, r["Name"], ",".join(ps), ret))
                    for x in range(p0, p1):
                        r = a.row(0x17, x)
                        if r:
                            lines.append("P %s::%s:%s" % (fn, r["Name"], a.propsig(a.blob(r["Type"]))))
                    for x in range(e0, e1):
                        r = a.row(0x14, x)
                        if r:
                            lines.append("E %s::%s:%s" % (fn, r["Name"], a.tdr(r["EventType"])))
                    for x in range(f0, f1):
                        r = a.row(0x04, x)
                        if r:
                            lines.append("F %s::%s:%s" % (fn, r["Name"], a.fieldsig(a.blob(r["Signature"]))))
                (sdir / (dll + ".surface.txt")).write_text(
                    "\n".join(sorted(set(lines))) + "\n", encoding="utf-8")
                print("  %s (%d entries)" % (dll, len(set(lines))))
            except Exception as e:
                print("  %s FAILED: %s" % (dll, e))

    # ---- 6. Write outputs ------------------------------------------------
    (out_dir / "install-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")

    summary = []
    summary.append("# Install Report\n")
    summary.append("Collected on `%s` (%s)\n" % (report["collectedOn"], report["collectorPlatform"]))
    summary.append("\n## Version Block\n")
    summary.append("| Field | Value |")
    summary.append("|---|---|")
    summary.append("| Install path | `%s` |" % root)
    summary.append("| Resolved game version | **%s** |" % (game_version or "UNKNOWN"))
    summary.append("| Version-prefix channel | %s |" % (channel or "UNKNOWN"))
    summary.append("| Steam buildid | %s |" % report.get("steam", {}).get("buildid", "n/a"))
    summary.append("| Steam branch | %s |" % report.get("steam", {}).get("steamBranch", "n/a"))
    summary.append("| **Stable vs beta** | **%s** |" % report.get("branchAssessment", "UNDETERMINED"))
    summary.append("| War Sails installed | **%s** |" % ("YES" if nav_dll else "NO"))
    if nav_dll:
        summary.append("| War Sails module version | %s |" % (naval.get("Version") if naval else "?"))
        summary.append("| NavalDLC assembly file version | %s |"
                       % report.get("navalDlcAssembly", {}).get("attrs", {})
                             .get("AssemblyFileVersionAttribute", "?"))
    summary.append("| Module count | %d |" % len(modules))
    summary.append("\n> The authoritative in-game value is "
                   "`ApplicationVersion.ApplicationVersionType` "
                   "(`Release` = stable, `Beta` = beta). The two signals above are "
                   "filesystem-derived; confirm against the in-game version string "
                   "before pinning.\n")
    summary.append("\n## Modules\n")
    summary.append("| Module | Version | Assemblies |")
    summary.append("|---|---|---|")
    for mi in modules:
        summary.append("| `%s` | %s | %d |" % (mi.get("dir", "?"), mi.get("Version", "?"),
                                               len(mi.get("assemblies", []))))
    summary.append("\n## Key Assemblies\n")
    summary.append("| Assembly | File version | Types |")
    summary.append("|---|---|---|")
    for info in asms:
        if info.get("missing"):
            summary.append("| `%s` | — | MISSING |" % info["file"])
        else:
            summary.append("| `%s` | %s | %s |" % (
                info["file"],
                info.get("attrs", {}).get("AssemblyFileVersionAttribute", "?"),
                info.get("typeCount", "?")))
    (out_dir / "INSTALL_REPORT.md").write_text("\n".join(summary) + "\n", encoding="utf-8")

    print("\n" + "=" * 70)
    print("Wrote:")
    print("  %s" % (out_dir / "INSTALL_REPORT.md"))
    print("  %s" % (out_dir / "install-report.json"))
    if surfaces:
        print("  %s/  (API surface dumps)" % (out_dir / "surface"))
    print("\nPaste INSTALL_REPORT.md back, or commit docs/install-report/.")
    print("Do NOT commit the game DLLs themselves — they are proprietary.")
    print("=" * 70)
    return 0

if __name__ == "__main__":
    sys.exit(main())
