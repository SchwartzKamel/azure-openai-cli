#!/usr/bin/env python3
"""name-screen.py -- screen candidate product names. Read-only.

Usage:  scripts/name-screen.py [--json] [--oss] <name> [<name> ...]

  --oss   open-source mode: skips the US trademark stage and instead checks the OSS world. It looks for
          GitHub repositories with the exact name and flags those in the AI/LLM/CLI space, flags PyPI, npm
          and crates packages of that name whose summary is AI/CLI-flavoured, and keeps the distribution
          blockers (a command on PATH, a Debian package, a Homebrew/Scoop/Docker name that is taken).

What it checks, cheapest first (a name that fails an early stage skips the later, rate-limited ones):
  1 local    a command on PATH, a Debian package, or a third-party mark / provider name
  2 channel  Homebrew formula and cask, Scoop main and extras, Docker library (HTTP 404 = free)
  3 uspto    live US federal marks: exact, whole-word, and edit-distance-1 neighbours, by class.
             An exact live mark whose goods mention software is a hard stop.
  4 github   exact-name repositories and their stars (uses the authenticated `gh` CLI if present)
  5 domains  .com .dev .sh via RDAP (informational)
  6 typos    local commands within one edit (a name one keystroke from a command people type)

WHAT THIS IS NOT. It is a preliminary screen, not clearance and not legal advice. It covers US federal
registrations and applications only, at the moment it runs. It does not cover state or common-law
marks, unregistered use, EU/UK/WIPO registers, translations, or sound-alikes beyond one edit. The
"software goods" test is a keyword heuristic. Use it to rule names OUT cheaply, then hand a short
list to counsel.
"""
import json, re, shutil, subprocess, sys, time, urllib.error, urllib.request

UA = {"User-Agent": "name-screen (read-only research)"}
DENY = set("""azure openai gpt chatgpt microsoft copilot claude anthropic gemini google llama meta mistral cohere
nvidia grok deepseek qwen bing cortana codex sora dalle whisper bard palm groq together cloudflare openrouter
ollama xai kimi""".split())
# zsh and fish builtins that bash does not have (bash is what `type -t` can ask here).
OTHER_SHELL_BUILTINS = set("""abbr argparse autoload bindkey builtin commandline compdef complete compgen contains count dirs
disable disown emulate enable fc float functions history integer jobs math nohup noglob not path popd print
pushd random rehash repeat sched setopt status string suspend trap ttyctl unalias unfunction unhash unlimit
unsetopt vared where whence zcompile zle zmodload zparseopts zstyle""".split())
SOFTWARE_CLASSES = {"009", "042"}
AI_SPACE = re.compile(r"\b(llm|llms|gpt|chatgpt|openai|ai|a\.i\.|artificial|prompt|chatbot|copilot|assistant|claude|gemini|ollama|agent|language model)\b", re.I)
SOFTWARE_GOODS = re.compile(
    r"software|application|\bapps?\b|platform|computer|download|saas|cloud|artificial intelligence|"
    r"machine learning|chatbot|developer|command.line", re.I)


def http(url, data=None, headers=None, timeout=15):
    req = urllib.request.Request(url, data=data, headers={**UA, **(headers or {})})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.status, r.read()
    except urllib.error.HTTPError as e:
        return e.code, b""
    except Exception:
        return 0, b""


def retry(fn, *args, tries=4):
    for i in range(tries):
        out = fn(*args)
        if out is not None:
            return out
        time.sleep(2 * (i + 1))
    return None


def norm(s):
    return re.sub(r"[^a-z0-9]", "", (s or "").lower())


def lev(a, b):
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        cur = [i]
        for j, cb in enumerate(b, 1):
            cur.append(min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (ca != cb)))
        prev = cur
    return prev[-1]


def local(n):
    if n in DENY or any(d in n for d in DENY if len(d) > 3):
        return ["third-party mark or provider name"]
    notes = []
    if shutil.which(n):
        notes.append("command on PATH")
    # Shell builtins, keywords, aliases and functions are not on PATH, so `which` misses them, yet a name
    # that is one (`type`, `time`, `test`, `where`) is shadowed in the shell before the binary is found.
    if re.fullmatch(r"[a-z0-9]+", n):
        t = subprocess.run(["bash", "-c", f"type -t {n}"], capture_output=True, text=True).stdout.strip()
        if t in ("builtin", "keyword", "alias", "function"):
            notes.append(f"shell {t}")
    if n in OTHER_SHELL_BUILTINS:
        notes.append("builtin in zsh or fish")
    p = subprocess.run(["apt-cache", "policy", n], capture_output=True, text=True)
    if re.search(r"Candidate: [^(]", p.stdout):
        notes.append("apt package")
    return notes


def channel(n):
    urls = {
        "brew": f"https://formulae.brew.sh/api/formula/{n}.json",
        "cask": f"https://formulae.brew.sh/api/cask/{n}.json",
        "scoop_main": f"https://raw.githubusercontent.com/ScoopInstaller/Main/master/bucket/{n}.json",
        "scoop_extras": f"https://raw.githubusercontent.com/ScoopInstaller/Extras/master/bucket/{n}.json",
        "docker": f"https://hub.docker.com/v2/repositories/library/{n}/",
    }
    return [k for k, u in urls.items() if http(u)[0] == 200]


def uspto_query(n, fuzziness=None):
    clause = {"match": {"wordmark": {"query": n, "fuzziness": fuzziness} if fuzziness else n}}
    body = json.dumps({"query": {"bool": {"must": [clause]}}, "size": 100}).encode()
    hdr = {"Content-Type": "application/json", "Accept": "application/json",
           "Origin": "https://tmsearch.uspto.gov", "Referer": "https://tmsearch.uspto.gov/search/search-information",
           "User-Agent": "Mozilla/5.0 (research)"}
    code, raw = http("https://tmsearch.uspto.gov/prod-stage-v1-0-0/tmsearch", data=body, headers=hdr, timeout=30)
    if code != 200:
        return None
    return [h["source"] for h in json.loads(raw)["hits"]["hits"] if h["source"].get("alive")]


def describe(s):
    classes = sorted({c.replace("IC ", "") for c in (s.get("internationalClass") or [])})
    goods = " ".join(s.get("goodsAndServices") or [])
    return {"mark": s.get("wordmark"), "owner": (s.get("ownerName") or ["?"])[0][:48], "classes": classes,
            "status": (s.get("statusDescription") or "")[:36], "software_class": bool(SOFTWARE_CLASSES & set(classes)),
            "software_goods": bool(SOFTWARE_CLASSES & set(classes)) and bool(SOFTWARE_GOODS.search(goods)),
            "goods": goods[:100]}


def uspto(n):
    exact_src = uspto_query(n)
    if exact_src is None:
        return None
    exact, word = [], []
    for s in exact_src:
        wm = norm(s.get("wordmark"))
        if wm == n:
            exact.append(describe(s))
        elif re.search(rf"\b{re.escape(n)}\b", (s.get("wordmark") or "").lower()):
            word.append(describe(s))
    near_src = retry(uspto_query, n, 1) or []
    near = [describe(s) for s in near_src if norm(s.get("wordmark")) not in ("", n) and lev(norm(s.get("wordmark")), n) == 1]
    return {"exact": exact, "word": word, "near": near,
            "exact_software": [m for m in exact if m["software_goods"]],
            "near_software": [m for m in near if m["software_goods"]]}


def github(n):
    q = f"search/repositories?q={n}+in:name&sort=stars&per_page=100"
    jq = (f'[.items[] | select(.name|ascii_downcase=="{n}")] | map({{repo:.full_name,stars:.stargazers_count,'
          f'description:(.description // "-")[0:90]}}) | .[0:10]')
    if shutil.which("gh"):
        p = subprocess.run(["gh", "api", q, "--jq", jq], capture_output=True, text=True)
        if p.returncode == 0:
            return json.loads(p.stdout)
        return None
    code, raw = http("https://api.github.com/" + q, headers={"Accept": "application/vnd.github+json"})
    if code != 200:
        return None
    return [{"repo": i["full_name"], "stars": i["stargazers_count"], "description": (i["description"] or "-")[:90]}
            for i in json.loads(raw)["items"] if i["name"].lower() == n][:10]


def packages(n):
    """PyPI, npm and crates.io packages of this exact name, with a one-line summary and an AI-space flag."""
    out = {}
    code, raw = http(f"https://pypi.org/pypi/{n}/json")
    if code == 200:
        try: out["pypi"] = (json.loads(raw)["info"].get("summary") or "")[:90]
        except Exception: out["pypi"] = ""
    code, raw = http(f"https://registry.npmjs.org/{n}")
    if code == 200:
        try: out["npm"] = (json.loads(raw).get("description") or "")[:90]
        except Exception: out["npm"] = ""
    code, raw = http(f"https://crates.io/api/v1/crates/{n}")
    if code == 200:
        try: out["crates"] = (json.loads(raw)["crate"].get("description") or "")[:90]
        except Exception: out["crates"] = ""
    return out


_RDAP = {}


def domains(n):
    if not _RDAP:
        code, raw = http("https://data.iana.org/rdap/dns.json")
        if code == 200:
            for svc in json.loads(raw)["services"]:
                for tld in svc[0]:
                    _RDAP[tld] = svc[1][0]
    out = {}
    for tld in ("com", "dev", "sh"):
        base = _RDAP.get(tld)
        code = http(f"{base.rstrip('/')}/domain/{n}.{tld}")[0] if base else 0
        out[tld] = {200: "taken", 404: "free"}.get(code, "unknown")
    return out


def typo_neighbours(n):
    cmds = subprocess.run(["bash", "-lc", "compgen -c"], capture_output=True, text=True).stdout.split()
    return sorted({c.lower() for c in cmds if c.lower() != n and abs(len(c) - len(n)) <= 1 and lev(c.lower(), n) <= 1})[:6]


def screen_oss(n):
    """Open-source screen: no trademark stage. Distribution blockers, then the OSS and package world."""
    r = {"name": n, "verdict": None, "mode": "oss"}
    notes = local(n)
    if notes:
        r["verdict"] = "OUT: " + ", ".join(notes); return r
    taken = channel(n)
    r["channels_taken"] = taken
    if taken:
        r["verdict"] = "OUT: taken on " + ", ".join(taken); return r
    # A failed GitHub lookup is an ERROR, never "no repositories": an empty answer from a rate-limited
    # client looks exactly like a clean name. github() returns None on failure and [] for a real "none".
    r["github"] = retry(github, n)
    if r["github"] is None:
        r["verdict"] = "ERROR: GitHub lookup failed (rate limit?), so this name was not verified"
        return r
    r["packages"] = packages(n)
    r["typo_neighbours"] = typo_neighbours(n)
    r["ai_repos"] = [g for g in r["github"] if AI_SPACE.search(g["description"])]
    r["ai_packages"] = {k: v for k, v in r["packages"].items() if AI_SPACE.search(v)}
    r["top_stars"] = max([g["stars"] for g in r["github"]] or [0])
    r["ai_top_stars"] = max([g["stars"] for g in r["ai_repos"]] or [0])
    flags = []
    if r["ai_repos"]:
        flags.append(f"{len(r['ai_repos'])} GitHub repo(s) of this name in the AI space (top {r['ai_top_stars']} stars)")
    if r["ai_packages"]:
        flags.append("AI-flavoured package(s): " + ", ".join(r["ai_packages"]))
    if r["top_stars"] > 1000 and not r["ai_repos"]:
        flags.append(f"a {r['top_stars']}-star repo of this name (not AI)")
    if r["typo_neighbours"]:
        flags.append("one edit from command(s): " + ", ".join(r["typo_neighbours"]))
    r["verdict"] = "PASS" if not flags else "PASS WITH FLAGS: " + "; ".join(flags)
    return r


def screen(n):
    r = {"name": n, "verdict": None}
    notes = local(n)
    if notes:
        r["verdict"] = "OUT: " + ", ".join(notes); return r
    taken = channel(n)
    r["channels_taken"] = taken
    if taken:
        r["verdict"] = "OUT: taken on " + ", ".join(taken); return r
    u = retry(uspto, n)
    if u is None:
        r["verdict"] = "ERROR: USPTO lookup failed"; return r
    r["uspto"] = u
    if u["exact_software"]:
        m = u["exact_software"][0]
        r["verdict"] = f"OUT: live US mark {m['mark']} ({m['owner']}) with software goods, classes {','.join(m['classes'])}"
        return r
    r["github"] = github(n)
    r["domains"] = domains(n)
    r["typo_neighbours"] = typo_neighbours(n)
    flags = []
    if u["exact"]: flags.append(f"{len(u['exact'])} live exact mark(s) in non-software goods")
    if u["near_software"]: flags.append(f"{len(u['near_software'])} near-miss live software mark(s): " + ", ".join(m["mark"] for m in u["near_software"][:4]))
    if r["typo_neighbours"]: flags.append("one edit from command(s): " + ", ".join(r["typo_neighbours"]))
    if r["github"] and r["github"][0]["stars"] > 300: flags.append(f"GitHub exact-name repo with {r['github'][0]['stars']} stars")
    r["verdict"] = "PASS" if not flags else "PASS WITH FLAGS: " + "; ".join(flags)
    return r


def render(r):
    lines = [f"{r['name']}: {r['verdict']}"]
    u = r.get("uspto")
    if u:
        lines.append(f"  USPTO live marks: exact {len(u['exact'])}, whole-word {len(u['word'])}, near-miss {len(u['near'])} "
                     f"({len(u['near_software'])} with software goods)")
        for m in u["exact"][:3]:
            lines.append(f"    exact: {m['mark']} | {m['owner']} | classes {','.join(m['classes'])} | {m['goods'][:60]}")
    if r.get("github") is not None:
        lines.append("  GitHub exact-name repos: " + ("; ".join(f"{g['repo']} ({g['stars']}): {g['description']}" for g in r["github"]) or "none"))
    if r.get("domains"):
        lines.append("  Domains: " + ", ".join(f".{t} {s}" for t, s in r["domains"].items()))
    lines.append("  (preliminary screen of US federal marks only; not clearance, not legal advice)")
    return "\n".join(lines)


def render_oss(r):
    lines = [f"{r['name']}: {r['verdict']}"]
    if r.get("github") is not None and r.get("mode") == "oss":
        lines.append("  GitHub exact-name repos: " + ("; ".join(f"{g['repo']} ({g['stars']}): {g['description'][:50]}" for g in r["github"][:4]) or "none"))
        lines.append("  Packages of this name: " + ("; ".join(f"{k}: {v[:48] or '-'}" for k, v in r["packages"].items()) or "none"))
    lines.append("  (open-source screen: no trademark stage; a flag is information, not a verdict)")
    return "\n".join(lines)


def main(argv):
    as_json = "--json" in argv
    oss = "--oss" in argv
    names = [a.lower() for a in argv if not a.startswith("--")]
    if not names:
        print(__doc__); return 2
    results = [(screen_oss if oss else screen)(n) for n in names]
    if as_json:
        print(json.dumps(results, indent=2))
    else:
        print("\n\n".join(render_oss(r) if oss else render(r) for r in results))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
