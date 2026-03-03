import os
import re
from typing import Dict, Tuple, List, Set

# Use paths relative to this script's directory
BASE_DIR = os.path.dirname(__file__)
API_PATH = os.path.join(BASE_DIR, "API.cs")
OUT_DIR = BASE_DIR
OUT_GM = os.path.join(OUT_DIR, "GMAPI.cs")

NAMESPACE_MARK = "namespace ChillyRoom.Services.BuildingGame"

# Optional manual overrides for edge cases where static analysis is insufficient
FORCE_INCLUDE: Set[str] = {
    # Add type names to always include as GM-exclusive (e.g., enums/DTOs confirmed GM-only)
    "ClaimStatus",
}
FORCE_EXCLUDE: Set[str] = set()


def find_matching_brace(s: str, open_pos: int) -> int:
    """Given index of '{', find matching '}' index (inclusive scan)."""
    assert s[open_pos] == '{'
    depth = 0
    for i, c in enumerate(s[open_pos:], start=open_pos):
        if c == '{':
            depth += 1
        elif c == '}':
            depth -= 1
            if depth == 0:
                return i
    return -1


def find_block_by_header(s: str, header_pos: int) -> Tuple[int, int]:
    """Given pos of a type header (class/enum), return the full block [start,end)."""
    open_pos = s.find('{', header_pos)
    if open_pos == -1:
        raise RuntimeError(f"Cannot locate '{{' for block at {header_pos}")
    end_pos = find_matching_brace(s, open_pos)
    if end_pos == -1:
        raise RuntimeError(f"Unmatched brace for block at {header_pos}")

    # include contiguous attributes/XML docs/blank lines above declaration
    start_line = s.rfind('\n', 0, header_pos) + 1
    attr_start = start_line
    while True:
        prev_line_end = s.rfind('\n', 0, attr_start - 1)
        if prev_line_end == -1:
            break
        prev_line_start = s.rfind('\n', 0, prev_line_end - 1) + 1 if prev_line_end > 0 else 0
        line = s[prev_line_start:prev_line_end].strip()
        if line.startswith('[') or line.startswith('///') or line == "":
            attr_start = prev_line_start
            continue
        break

    return min(attr_start, start_line), end_pos + 1


def extract_gm_client_block(ns_inner: str) -> Tuple[int, int, str]:
    """Find GmClient/GMClient block and return (start, end, className)."""
    # Support both "GmClient" and "GMClient"
    m = re.search(r"\bpublic\s+partial\s+class\s+(GmClient|GMClient)\b", ns_inner)
    if not m:
        # Also try without 'partial' just in case
        m = re.search(r"\bpublic\s+class\s+(GmClient|GMClient)\b", ns_inner)
    if not m:
        raise RuntimeError("GmClient/GMClient not found")
    start, end = find_block_by_header(ns_inner, m.start())
    cls_name = m.group(1)
    return start, end, cls_name


def collect_type_declarations(ns_inner: str) -> Dict[str, Tuple[str, int, int]]:
    """Return map: type name -> (kind, start, end) for DTO/enum declarations (exclude *Client)."""
    decls: Dict[str, Tuple[str, int, int]] = {}

    # Classes (allow partial or not)
    class_pat = re.compile(r"\bpublic\s+(?:partial\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)\b")
    enum_pat = re.compile(r"\bpublic\s+enum\s+([A-Za-z_][A-Za-z0-9_]*)\b")

    for m in class_pat.finditer(ns_inner):
        name = m.group(1)
        if name.endswith("Client"):
            # clients handled separately; don't include as DTO
            continue
        start, end = find_block_by_header(ns_inner, m.start())
        decls[name] = ("class", start, end)

    for m in enum_pat.finditer(ns_inner):
        name = m.group(1)
        start, end = find_block_by_header(ns_inner, m.start())
        decls[name] = ("enum", start, end)

    return decls


def collect_client_blocks(ns_inner: str) -> Dict[str, Tuple[int, int, str]]:
    """Return map: ClientName -> (start, end, text) for all *Client classes."""
    clients: Dict[str, Tuple[int, int, str]] = {}
    client_pat = re.compile(r"\bpublic\s+(?:partial\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)\b")
    for m in client_pat.finditer(ns_inner):
        name = m.group(1)
        if not name.endswith("Client"):
            continue
        s, e = find_block_by_header(ns_inner, m.start())
        clients[name] = (s, e, ns_inner[s:e])
    return clients


def find_references(text: str, candidates: Set[str]) -> Set[str]:
    found: Set[str] = set()
    if not candidates:
        return found
    names_sorted = sorted(candidates, key=len, reverse=True)
    chunk = 200
    for i in range(0, len(names_sorted), chunk):
        part = names_sorted[i:i + chunk]
        pat = re.compile(r"\b(" + '|'.join(map(re.escape, part)) + r")\b")
        for m in pat.finditer(text):
            found.add(m.group(1))
    return found


def _extract_type_tokens(type_text: str) -> Set[str]:
    """Extract identifier tokens from a type string (handles generics and arrays)."""
    # Remove nullable ? and array [] notations for tokenization simplicity
    cleaned = re.sub(r"\?|\[\s*\]", " ", type_text)
    return set(re.findall(r"\b([A-Za-z_][A-Za-z0-9_]*)\b", cleaned))


def _find_type_refs_in_decl(body: str, type_names: Set[str]) -> Set[str]:
    """Find referenced types inside a DTO/enum declaration body by parsing field/property signatures.
    This version captures the entire type segment (supports nested generics, arrays, namespaces) and excludes the member name.
    """
    refs: Set[str] = set()
    # Property/field pattern: [access] [modifiers...] <TYPE_SEGMENT> <MemberName> { or ; or =
    prop_pat = re.compile(
        r"\b(?:public|private|protected|internal)\s+"
        r"(?:(?:static|readonly|virtual|override|sealed|new|partial|extern|unsafe|volatile)\s+)*"
        r"(.+?)\s+"  # non-greedy type segment
        r"([A-Za-z_][A-Za-z0-9_]*)\s*(?:[;=\{])",
        re.DOTALL,
    )
    for m in prop_pat.finditer(body):
        type_str = m.group(1)
        toks = _extract_type_tokens(type_str)
        refs |= (toks & type_names)
    # Base type / interfaces: class Foo : Base, IFoo<Bar>
    base_pat = re.compile(r"\bclass\s+[A-Za-z_][A-Za-z0-9_]*\s*:\s*([^\{]+)\{")
    for m in base_pat.finditer(body):
        toks = _extract_type_tokens(m.group(1))
        refs |= (toks & type_names)
    return refs


def build_decl_ref_graph(ns_inner: str, decls: Dict[str, Tuple[str, int, int]]) -> Dict[str, Set[str]]:
    """Build a graph: type -> set(referenced types) using simple signature parsing to avoid identifier-name false positives."""
    graph: Dict[str, Set[str]] = {k: set() for k in decls.keys()}
    names = set(decls.keys())
    for name, (_, s, e) in decls.items():
        body = ns_inner[s:e]
        refs = _find_type_refs_in_decl(body, names)
        refs.discard(name)
        graph[name] = refs
    return graph


def bfs_reachable(seeds: Set[str], graph: Dict[str, Set[str]]) -> Set[str]:
    """All types reachable from seeds via graph edges (including seeds)."""
    visited: Set[str] = set()
    queue: List[str] = list(seeds)
    while queue:
        n = queue.pop()
        if n in visited:
            continue
        visited.add(n)
        for m in graph.get(n, ()):  # type: ignore
            if m not in visited:
                queue.append(m)
    return visited


def _find_matching_angle(text: str, start_lt: int) -> int:
    """Given index of '<', find matching '>' considering nested generics."""
    depth = 0
    for i in range(start_lt, len(text)):
        c = text[i]
        if c == '<':
            depth += 1
        elif c == '>':
            depth -= 1
            if depth == 0:
                return i
    return -1


def _extract_generic_args_after_markers(text: str, markers: List[str]) -> List[str]:
    """Extract type tokens inside generic angle brackets following known markers."""
    results: List[str] = []
    lower = text
    for marker in markers:
        search_pos = 0
        while True:
            idx = lower.find(marker, search_pos)
            if idx == -1:
                break
            lt = lower.find('<', idx + len(marker))
            if lt == -1:
                search_pos = idx + len(marker)
                continue
            gt = _find_matching_angle(lower, lt)
            if gt == -1:
                search_pos = lt + 1
                continue
            inside = lower[lt + 1:gt]
            # Collect identifier tokens
            for tok in re.findall(r"\b([A-Za-z_][A-Za-z0-9_]*)\b", inside):
                results.append(tok)
            search_pos = gt + 1
    return results


def find_client_type_seeds(ctext: str, type_names: Set[str], coarse: bool = False) -> Set[str]:
    """Find DTO/enum type references in a client class.
    - Always inspects common generic patterns and method parameter types.
    - When coarse=True, also performs a fallback word scan.
    """
    markers = [
        'ReadObjectResponseAsync',
        'ObjectResponseResult',
        'System.Threading.Tasks.Task',
        'Task',
    ]
    seeds = set(_extract_generic_args_after_markers(ctext, markers))
    # Also scan method signatures (parameter types)
    meth_pat = re.compile(r"\bpublic\s+[^{;]+?\s+[A-Za-z_][A-Za-z0-9_]*\s*\(([^)]*)\)", re.DOTALL)
    for m in meth_pat.finditer(ctext):
        param_blob = m.group(1)
        seeds |= _extract_type_tokens(param_blob)
    # Fallback: coarse word scan if requested
    if coarse:
        seeds |= find_references(ctext, type_names)
    return seeds & type_names


def split_to_gm(text: str) -> str:
    ns_pos = text.find(NAMESPACE_MARK)
    if ns_pos == -1:
        raise RuntimeError("Namespace not found")
    ns_open = text.find('{', ns_pos)
    if ns_open == -1:
        raise RuntimeError("Namespace opening brace not found")
    ns_close = find_matching_brace(text, ns_open)
    if ns_close == -1:
        raise RuntimeError("Namespace closing brace not found")

    header = text[:ns_pos].rstrip() + "\n"
    ns_header = text[ns_pos:ns_open + 1]  # include 'namespace ... {'
    ns_inner = text[ns_open + 1:ns_close]
    footer = text[ns_close + 1:]

    gm_start, gm_end, gm_name = extract_gm_client_block(ns_inner)
    gm_block = ns_inner[gm_start:gm_end]

    decls = collect_type_declarations(ns_inner)
    decl_graph = build_decl_ref_graph(ns_inner, decls)

    # Initial references from clients to DTO/enums
    clients = collect_client_blocks(ns_inner)
    all_type_names = set(decls.keys())

    # Seeds from GmClient: use precise generic scanning and parameter-type parsing (no coarse word scan)
    gm_seed = find_client_type_seeds(gm_block, all_type_names, coarse=False)
    if not gm_seed:
        gm_seed = find_references(gm_block, all_type_names)
    # Seeds from other clients: precise only to avoid false positives
    others_seed: Set[str] = set()
    for cname, (cs, ce, ctext) in clients.items():
        if cname == gm_name:
            continue
        others_seed |= find_client_type_seeds(ctext, all_type_names, coarse=False)

    # Reachability over decl graph
    gm_reachable = bfs_reachable(gm_seed, decl_graph)
    others_reachable = bfs_reachable(others_seed, decl_graph)

    # Shared types are those reachable from both
    shared_types = gm_reachable & others_reachable
    # Export only gm-exclusive reachable types
    chosen = gm_reachable - shared_types

    # Manual overrides
    if FORCE_INCLUDE:
        manual = {t for t in FORCE_INCLUDE if t in decls}
        chosen |= manual
    if FORCE_EXCLUDE:
        chosen -= {t for t in FORCE_EXCLUDE if t in decls}

    dep_names = sorted(list(chosen), key=lambda x: decls[x][1])

    # keep original order by start index
    dep_blocks = sorted(((name, *decls[name]) for name in dep_names), key=lambda x: x[2])
    dep_text = "".join(ns_inner[start:end] for (name, kind, start, end) in dep_blocks)

    gm_file_parts: List[str] = []
    gm_file_parts.append("#if UNITY_EDITOR\n")
    gm_file_parts.append(header)
    gm_file_parts.append(ns_header)
    gm_file_parts.append("\n")
    gm_file_parts.append(gm_block)
    gm_file_parts.append("\n")
    gm_file_parts.append(dep_text)
    gm_file_parts.append("\n}\n")  # close namespace
    gm_file_parts.append(footer)
    # Ensure #endif starts on its own line
    if not (footer.endswith("\n") or footer.endswith("\r\n")):
        gm_file_parts.append("\n")
    gm_file_parts.append("#endif\n")

    return "".join(gm_file_parts)


def main():
    if not os.path.isfile(API_PATH):
        raise SystemExit(f"File not found: {API_PATH}")

    with open(API_PATH, "r", encoding="utf-8") as f:
        text = f.read()

    gm_text = split_to_gm(text)

    # Write with platform default newlines (CRLF on Windows)
    with open(OUT_GM, "w", encoding="utf-8") as f:
        f.write(gm_text)

    print(f"[OK] Wrote: {OUT_GM}")
    print("[Note] Shared types are intentionally not exported. Keep API.cs in compile set.")


if __name__ == "__main__":
    main()
