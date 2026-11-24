---
name: Dino-Dev-Jira_MCP
description: Guides an engineer through connecting Claude Code to the Jira MCP. Use when setting up or fixing Jira MCP.
---

# Onboarding an engineer onto the Jira MCP

You are guiding **one human** through a setup they have never done. Each engineer authorizes
with **their own** Broadcom SSO account and their own Claude Code login — nothing is shared
between people, and no credential file is ever copied from one machine to another.

**This document is self-contained.** It assumes the engineer has no access to any particular
repository: every URL, every rule, and the refresh script itself are included below.

## How to run this session

**Go one stage at a time.** Present a stage, wait for the engineer to report what they saw,
confirm the checkpoint passed, then move on. Do not paste all six stages at once — this
document exists because the failure modes are silent and easy to walk past.

- Every stage has a **Checkpoint**. If it does not pass, work the *If it fails* rows before
  advancing; a later stage built on a failed checkpoint produces confusing errors.
- Give them one command at a time, and ask them to paste the **output**, not a summary.
- Where a step depends on their environment (OS, browser, which Jira instance), ask rather
  than assume — use AskUserQuestion for the instance choice in Stage 2.
- If they are typing to you from a **GUI chat panel** (an IDE "Claude Code" sidebar), say so
  up front: Stage 3 cannot be done there, and they will need a real terminal window.

---

## Stage 1 — Prerequisites

Have them run each and report the output:

```bash
claude --version          # any recent build; 2.1.x is known good
claude auth status        # must show a signed-in Anthropic/Claude account
python --version          # 3.12+, needed for the helper script in Stage 5
```

Then have them open their Jira instance in a browser (e.g. **https://jira.broadcom.net**) and
confirm they can see a project and are logged in through Broadcom SSO.

**Checkpoint:** version prints, `claude auth status` shows signed in, Python 3.12+, and Jira
loads in the browser.

| If it fails | Do this |
|---|---|
| `claude: command not found` | Claude Code is not installed or not on `PATH`. Install it, reopen the terminal, retry. |
| `claude auth status` shows not signed in | `claude auth login` and complete the browser flow. This is their **Claude** login — separate from Broadcom SSO. Both are needed. |
| Jira shows "access denied" or asks for a licence | Stop. The MCP gateway only proxies permissions they already have; it cannot grant Jira access. They must request Jira access first — no amount of MCP setup will work around this. |
| Python missing or older than 3.12 | They can still finish Stages 2–4 (the CLI path). Only the Stage 5 helper needs 3.12+; without it they must re-run `/mcp reconnect` by hand each hour. |

---

## Stage 2 — Register the server

Ask which Jira instance they need. Each instance has its own gateway:

| Jira instance | Gateway MCP URL |
|---|---|
| `jira.broadcom.net` | `https://gateway-jira.apps-tpcf-epic-gto.lvn.broadcom.net/jira/mcp` |
| `vmw-jira.broadcom.net` | `https://gateway-vmw-jira.apps-tpcf-epic-gto.lvn.broadcom.net/jira/mcp` |
| `ingjira.broadcom.net` (CSG) | `https://gateway-ingjira.apps-tpcf-epic-gto.lvn.broadcom.net/jira/mcp` |
| `bep-jira.broadcom.net` | `https://gateway-bep-jira.apps-tpcf-epic-gto.lvn.broadcom.net/jira/mcp` |
| `bsg-jira.broadcom.net` | `https://gateway-bsg-jira.apps-tpcf-epic-gto.lvn.broadcom.net/jira/mcp` |
| `jira-dcsg-1.gwd.broadcom.net` | `https://gateway-jira-dcsg-1-gwd.apps-tpcf-epic-gto.lvn.broadcom.net/jira/mcp` |
| `labservices.broadcom.net` | `https://gateway-labservices-jira.apps-tpcf-epic-gto.lvn.broadcom.net/jira/mcp` |
| `devops-manager.broadcom.net` | `https://gateway-devops-manager-jira.apps-tpcf-epic-gto.lvn.broadcom.net/jira/mcp` |

For semiconductor / WSD work the usual choice is `jira.broadcom.net` (projects `PENWSSD`,
`PENFBARNPI`, `PENSD`). Register it:

```bash
claude mcp add --transport http jira https://gateway-jira.apps-tpcf-epic-gto.lvn.broadcom.net/jira/mcp -s user
```

Explain what this does, because it is easy to get wrong later:

- `-s user` registers it in `~/.claude.json` for **all** their projects. Without it the server
  is local to one directory and they will wonder why it vanished elsewhere.
- The name `jira` is how they will refer to it in `/mcp`. If they need a second instance later,
  add it under a different name (`vmw-jira`, `bep-jira`, ...) — never reuse `jira`.

```bash
claude mcp list
```

**Checkpoint:** `jira: <url> (HTTP) - ! Needs authentication`. "Needs authentication" is the
**correct** state here — they have not logged in yet. That comes next.

| If it fails | Do this |
|---|---|
| `No MCP servers are configured` | Their running session started *before* `claude mcp add`. Fully restart the Claude Code session so it re-reads `~/.claude.json`. |
| Already lists `jira` from a past attempt | Fine — skip ahead. To start clean: `claude mcp remove jira -s user`, then re-add. |

---

## Stage 3 — Authorize with their SSO account (the stage that trips everyone)

**This must happen in a real terminal window** — PowerShell, Git Bash, Windows Terminal, or an
IDE's integrated `TERMINAL` tab. It **cannot** be done from a GUI chat panel, and it cannot be
done by you on their behalf: it needs a TTY to draw its picker, and their browser session to
carry their SSO cookie.

Have them, in that terminal:

1. Start an interactive session: `claude`
2. Type: `/mcp`
3. Select `jira` from the list, then choose **Authenticate** (or **Re-authenticate**).
4. A browser window opens. They sign in with **their own Broadcom SSO account** if prompted.
5. A **consent/authorize screen** appears — they must click **Authorize**. This grants their
   Claude Code client permission to act as them in Jira. It is a one-time approval per machine.
6. The browser redirects to `http://localhost:3118/callback`. The CLI catches this
   automatically and the tab can be closed.

**Checkpoint:** the `/mcp` panel shows `jira · ✓ connected · 21 tools`.

| If it fails | Do this |
|---|---|
| `MCP controls aren't available right now — the terminal is still starting up or is showing another view` | They are in a **GUI chat panel**, not a terminal. This will never succeed there, no matter how many times they retry. Move them to a real terminal window. |
| Browser opens but the callback never arrives (remote/VDI/SSH) | The redirect target is a plain `http://localhost:3118` on the **machine running `claude`**. If their browser runs elsewhere (VDI, remote SSH, WSL-to-Windows split), that loopback cannot reach the listener. They must run the browser on the same host, or forward the port (`ssh -L 3118:localhost:3118`). |
| `Address already in use` / callback never arrives | Something else holds port 3118. Find it — Windows: `netstat -ano \| findstr 3118`; macOS/Linux: `lsof -i :3118` — close it, retry. A previous half-finished OAuth attempt is the usual culprit. |
| Consent screen shows an error, or no Jira access | Back to Stage 1 — they likely lack Jira access on that instance. |
| They authorize, but it still says "needs authentication" | Stale marker — see Stage 5. Run the helper's `ensure`, or delete their server's key from `~/.claude/mcp-needs-auth-cache.json`. |

> Tell them plainly: **never** try to authenticate with `claude -p "..."`. Print mode exits as
> soon as it prints the authorize URL, so nothing is listening on the callback port and the
> code cannot be redeemed. Pasting the callback URL back afterwards does not work either.

---

## Stage 4 — First real query

Still in that interactive `claude` session, have them ask for something harmless — MCP tools
are only used in agent mode, so a plain question is the right test:

```
Use jira_get_me to show who I am authenticated as in Jira.
```

The **first** tool call raises a permission prompt naming `mcp__jira__jira_get_me`. They must
approve it. Explain that this is Claude Code's own guardrail, per tool, and is separate from the
OAuth consent in Stage 3 — approving one does not approve the others.

Then something real (substitute a project they actually work on):

```
Get Jira issue PENWSSD-6 and show its status, assignee and priority.
```

**Checkpoint:** their own name comes back from `jira_get_me`, and the issue lookup returns real
fields.

| If it fails | Do this |
|---|---|
| Hangs on "Please approve the ... permission prompt" | The prompt is waiting somewhere they cannot see, or there is no TTY to answer it. Answer it in the terminal; for scripted use pass `--allowedTools "mcp__jira__jira_get_issue"`. |
| `401` / "needs authentication" straight after a successful Stage 3 | Stale marker in `~/.claude/mcp-needs-auth-cache.json`; see Stage 5. |
| A JQL error mentioning the `~` operator | Expected — this is Jira Server/DC. Use `assignee = <username>`, never `assignee ~ "Some Name"`. |

### The 21 tools they now have

Read-only: `jira_get_me`, `jira_get_projects`, `jira_get_boards`, `jira_get_board_issues`,
`jira_get_sprints`, `jira_get_comments`, `jira_get_components`, `jira_get_create_issue_metadata`,
`jira_get_issue`, `jira_get_issue_changelog`, `jira_get_issue_transitions`,
`jira_get_project_issuetypes`, `jira_search_issues`

Writes: `jira_add_issues_to_sprint`, `jira_add_comment`, `jira_add_worklog`, `jira_assign_issue`,
`jira_create_issue`, `jira_link_issues`, `jira_transition_issue`, `jira_update_issue`

The CLI labels *every* tool "destructive", including the read-only ones. That label is unreliable
— judge by the tool name.

---

## Stage 5 — Keep it alive (why it dies every hour, and the fix)

Set the expectation explicitly, or they will think the setup broke:

> Tokens last **60 minutes**, and the standard silent refresh does **not** work on this gateway.
> Its authorization server omits `none` from `token_endpoint_auth_methods_supported`, and Claude
> Code is a public client holding no client secret — so its `refresh_token` grant is rejected
> with HTTP 401. A full re-authorization is genuinely required each hour. This is a server-side
> limitation, not something they did wrong.

There is also a trap worth warning them about: when a token expires, the CLI records it in
`~/.claude/mcp-needs-auth-cache.json` and **never clears that entry** once a new token arrives.
Symptom: they re-authenticate successfully, and the very next command asks them to authenticate
again — while the stored token is provably valid.

### Option A (recommended) — the helper script

The `authorization_code` + PKCE grant *does* accept a public client, so re-authorization can be
automated. Have them save the script from the **Appendix at the end of this document** as
`jira_mcp.py`, anywhere convenient (e.g. their home directory). It is **stdlib-only** — nothing
to install.

```powershell
python "$HOME\jira_mcp.py" status     # how long the token has left
python "$HOME\jira_mcp.py" ensure     # re-authorize if under 5 minutes remain
python "$HOME\jira_mcp.py" issue PENWSSD-6
python "$HOME\jira_mcp.py" search "project = PENWSSD AND resolution = Unresolved"
```

```bash
python ~/jira_mcp.py status     # how long the token has left
python ~/jira_mcp.py ensure     # re-authorize if under 5 minutes remain
python ~/jira_mcp.py issue PENWSSD-6
python ~/jira_mcp.py search "project = PENWSSD AND resolution = Unresolved"
```

Use whichever matches their shell — **not interchangeably**. `~` is bash/zsh syntax: those shells
expand it to the home directory before `python` ever sees it. PowerShell does not do this for a
bare argument to an external command (it only resolves `~` for its own cmdlets like `cd ~`), so
`python ~/jira_mcp.py` in PowerShell fails with "file not found" — pass `$HOME` instead, as above.
On plain `cmd.exe` (not PowerShell), `$HOME` also doesn't exist — use `%USERPROFILE%\jira_mcp.py`,
or just open PowerShell instead, which this project treats as the default Windows shell.

`ensure` tries `prompt=none` first, which the gateway answers from their existing SSO session
**without** showing the consent screen — so after the one-time approval in Stage 3, hourly
re-authorization needs no clicks. It writes the same credentials file the CLI reads, so a
successful run also un-sticks `/mcp` and any IDE panel, and it clears the stale marker above.

`issue` and `search` call `ensure` first, so queries are self-healing.

### Option B — manual

Without Python 3.12+, they re-run `/mcp reconnect` in a real terminal whenever a call starts
failing. Whether that shows a consent screen to click through every time, or goes through
silently like the helper script's `prompt=none` attempt, depends on the CLI's own OAuth
implementation — not something confirmed here either way. Assume it may ask for a click; if it
doesn't, that's a pleasant surprise, not something to plan around.

**Checkpoint:** `python "$HOME\jira_mcp.py" status` (PowerShell) or `python ~/jira_mcp.py status`
(bash) prints a positive minutes-remaining figure (or, for Option B, `/mcp` shows connected).

---

## Stage 6 — Working habits

Hand over these rules. Each has already cost someone real time:

- **Constrain JQL by project.** The gateway sees the entire Broadcom estate (~2.7M issues); an
  unfiltered search is slow and meaningless.
- **`assignee ~ "..."` is invalid.** This is Jira Server/DC, not Cloud — it only supports `=`
  and `in` on `assignee`. The error reads
  `The operator '~' is not supported by the 'assignee' field.`
- **Never guess a username from a display name.** "Kuok Liang Koh" is `kuokliak`; most staff are
  `<initials><digits>` (e.g. `ay895797`). There is **no user-search tool** among the 21 — find a
  username by searching issues and reading the `assignee` object, e.g. `jira_search_issues` with
  `assignee is not EMPTY`, then match on display name.
- **Never quote a priority that came from a search.** `jira_search_issues` returns a reduced
  field set and nulls `priority` — issues that are actually High/Medium/Low come back empty.
  Re-fetch with `jira_get_issue` before reporting priority or description.
- **Writes are real.** `jira_add_comment`, `jira_assign_issue`, `jira_transition_issue`,
  `jira_update_issue` and friends change tickets other people are watching. Confirm intent
  before calling one.
- **Respect the 250 requests/minute** gateway limit; avoid tight loops over many issues.
- **Never share or copy `~/.claude/.credentials.json`.** It holds live bearer tokens for their
  account. Each engineer runs Stage 3 on their own machine.

## Closing the session

Confirm all six checkpoints passed and summarize what they now have: a user-scoped `jira`
server, their own authorized token, a working query, and a way to keep it fresh. If any
checkpoint is still failing, say which one and what is blocking it — do not sign off a partially
working setup as done.

---

## Appendix: `jira_mcp.py`

Save this as `jira_mcp.py` (stdlib-only, Python 3.12+). It reads and updates
`~/.claude/.credentials.json`, backing that file up before every write.

```python
"""Keep the Jira MCP OAuth token fresh, and query Jira without the Claude Code CLI.

Why this exists: the GTO Jira auth adapter does not advertise ``none`` in
``token_endpoint_auth_methods_supported``, so Claude Code (a public client holding no
client secret) cannot use the ``refresh_token`` grant -- it gets HTTP 401. That is the
root cause of the documented "token expires ~1h and you must /mcp reconnect" issue.

The ``authorization_code`` + PKCE grant *does* accept a public client, so this module
re-runs that flow: it starts the loopback listener on the registered redirect port, opens
the browser, captures the code, exchanges it, and writes the new token back into
``~/.claude/.credentials.json`` where the Claude Code CLI also reads it.

The first attempt sends ``prompt=none``, which the adapter answers from the existing SSO
session without rendering its consent screen -- so the usual case needs **no clicks**. Only
when that is refused (SSO session gone, or consent never granted on this machine) does it
fall back to a visible consent page that must be approved by hand.

stdout carries query results (this is a CLI, so stdout is the product); diagnostics go to
stderr so the two can be piped apart.

    python tools/jira_mcp.py status
    python tools/jira_mcp.py ensure
    python tools/jira_mcp.py issue PENWSSD-6
    python tools/jira_mcp.py search "assignee = kuokliak AND resolution = Unresolved"
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
import secrets
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path
from typing import Any, ClassVar

CREDENTIALS_PATH = Path.home() / ".claude" / ".credentials.json"
NEEDS_AUTH_CACHE_PATH = Path.home() / ".claude" / "mcp-needs-auth-cache.json"

# Re-authenticate when less than this much of the token lifetime remains.
REFRESH_MARGIN_SECONDS = 300
# How long to wait for the user to finish the browser round trip.
CALLBACK_TIMEOUT_SECONDS = 180
# The prompt=none attempt involves no human, so it should fail fast.
SILENT_CALLBACK_TIMEOUT_SECONDS = 45
HTTP_TIMEOUT_SECONDS = 60

CHROME_CANDIDATES = (
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
)


def log(message: str) -> None:
    """Diagnostics -> stderr, so they can be piped away from results."""
    sys.stderr.write(message + "\n")


def emit(message: str = "") -> None:
    """Results -> stdout; this is a CLI, so stdout is the deliverable."""
    sys.stdout.write(message + "\n")


# --------------------------------------------------------------------------- credentials


def load_credentials() -> dict[str, Any]:
    return json.loads(CREDENTIALS_PATH.read_text(encoding="utf-8"))


def find_server_entry(credentials: dict[str, Any], server_name: str) -> tuple[str, dict[str, Any]]:
    """Return the ``(storage_key, entry)`` the CLI uses for ``server_name``.

    The storage key embeds a hash the CLI computes, so it must be preserved verbatim on
    write or the CLI will no longer find the token we just refreshed.
    """
    for key, entry in (credentials.get("mcpOAuth") or {}).items():
        if entry.get("serverName") == server_name:
            return key, entry
    raise SystemExit(
        f"No OAuth entry for MCP server {server_name!r} in {CREDENTIALS_PATH}. "
        f"Authenticate once via `/mcp` in an interactive terminal first."
    )


def save_server_entry(server_name: str, entry: dict[str, Any]) -> None:
    """Write ``entry`` back, re-reading first so we never clobber a concurrent CLI write."""
    credentials = load_credentials()
    key, _ = find_server_entry(credentials, server_name)
    credentials["mcpOAuth"][key] = entry

    backup = CREDENTIALS_PATH.with_suffix(f".json.bak.{time.strftime('%Y%m%d-%H%M%S')}")
    shutil.copy2(CREDENTIALS_PATH, backup)

    tmp = CREDENTIALS_PATH.with_suffix(".json.tmp")
    tmp.write_text(json.dumps(credentials, indent=2), encoding="utf-8")
    os.replace(tmp, CREDENTIALS_PATH)
    log(f"  credentials updated (backup: {backup.name})")


def clear_needs_auth_cache(server_name: str) -> None:
    """Drop a stale "needs auth" marker.

    The CLI writes this file when a token expires, but does not clear it when the token is
    replaced. A leftover entry makes every new CLI process skip a perfectly valid token and
    jump straight into a fresh browser flow.
    """
    if not NEEDS_AUTH_CACHE_PATH.exists():
        return
    try:
        cache = json.loads(NEEDS_AUTH_CACHE_PATH.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return
    if cache.pop(server_name, None) is not None:
        NEEDS_AUTH_CACHE_PATH.write_text(json.dumps(cache), encoding="utf-8")
        log(f"  cleared stale needs-auth cache entry for {server_name!r}")


def seconds_remaining(entry: dict[str, Any]) -> float:
    return entry.get("expiresAt", 0) / 1000 - time.time()


# ------------------------------------------------------------------------------ oauth


def discover_metadata(authorization_server_url: str) -> dict[str, Any]:
    """Fetch RFC 8414 metadata, trying both the path-inserted and path-suffixed layouts."""
    parts = urllib.parse.urlsplit(authorization_server_url)
    path = parts.path.rstrip("/")
    candidates = [
        urllib.parse.urlunsplit((parts.scheme, parts.netloc, f"/.well-known/oauth-authorization-server{path}", "", "")),
        urllib.parse.urlunsplit((parts.scheme, parts.netloc, f"{path}/.well-known/oauth-authorization-server", "", "")),
    ]
    for url in candidates:
        try:
            with urllib.request.urlopen(url, timeout=HTTP_TIMEOUT_SECONDS) as response:
                if response.status == 200:
                    return json.loads(response.read().decode())
        except (urllib.error.URLError, json.JSONDecodeError):
            continue
    raise SystemExit(f"Could not discover OAuth metadata from {authorization_server_url}")


class _CallbackHandler(BaseHTTPRequestHandler):
    """Single-shot loopback receiver for the authorization code."""

    captured: ClassVar[dict[str, str]] = {}

    def do_GET(self) -> None:  # name fixed by BaseHTTPRequestHandler
        query = urllib.parse.parse_qs(urllib.parse.urlsplit(self.path).query)
        _CallbackHandler.captured = {k: v[0] for k, v in query.items()}
        ok = "code" in _CallbackHandler.captured
        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.end_headers()
        body = (
            "<h2>Jira MCP authorized.</h2><p>You can close this tab.</p>"
            if ok
            else f"<h2>Authorization failed.</h2><pre>{_CallbackHandler.captured}</pre>"
        )
        self.wfile.write(body.encode())

    def log_message(self, *_args: Any) -> None:
        """Silence the default stderr access log."""


def open_browser(url: str) -> None:
    for chrome in CHROME_CANDIDATES:
        if Path(chrome).is_file():
            subprocess.Popen([chrome, url], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            log("  opened Chrome for the SSO round trip")
            return
    import webbrowser

    webbrowser.open(url)
    log("  opened default browser for the SSO round trip")


def _authorization_round(entry: dict[str, Any], metadata: dict[str, Any], silent: bool) -> tuple[dict[str, str], str]:
    """Drive one browser round trip. Returns ``(callback_params, code_verifier)``.

    ``silent=True`` adds ``prompt=none``, which asks the authorization server to skip the
    consent screen and answer immediately from the existing SSO session. That is the normal
    path once consent has been granted once; it needs no clicks. If the server cannot honour
    it (session gone, consent revoked) it redirects straight back with an ``error`` instead
    of rendering UI, so this costs one fast round trip rather than a hang.
    """
    redirect_uri = entry["redirectUri"]
    port = urllib.parse.urlsplit(redirect_uri).port or 80

    verifier = base64.urlsafe_b64encode(secrets.token_bytes(32)).decode().rstrip("=")
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip("=")
    state = base64.urlsafe_b64encode(secrets.token_bytes(24)).decode().rstrip("=")

    # `resource` is the RFC 8707 indicator; the adapter issues an audience-bound token and
    # rejects the exchange without it.
    resource = entry["serverUrl"].removesuffix("/mcp")
    params = {
        "response_type": "code",
        "client_id": entry["clientId"],
        "code_challenge": challenge,
        "code_challenge_method": "S256",
        "redirect_uri": redirect_uri,
        "state": state,
        "scope": entry.get("scope", "WRITE"),
        "resource": resource,
    }
    if silent:
        params["prompt"] = "none"
    authorize_url = metadata["authorization_endpoint"] + "?" + urllib.parse.urlencode(params)

    timeout = SILENT_CALLBACK_TIMEOUT_SECONDS if silent else CALLBACK_TIMEOUT_SECONDS
    server = HTTPServer(("127.0.0.1", port), _CallbackHandler)
    server.timeout = timeout
    _CallbackHandler.captured = {}
    try:
        open_browser(authorize_url)
        if silent:
            log(f"  attempting silent re-auth (prompt=none), up to {timeout}s ...")
        else:
            log(f"  consent needed -- approve in the browser (waiting up to {timeout}s) ...")
        server.handle_request()
    finally:
        server.server_close()

    captured = _CallbackHandler.captured
    if captured and captured.get("state") != state:
        raise SystemExit("State mismatch on the callback -- refusing the response.")
    return captured, verifier


def authorize(entry: dict[str, Any]) -> dict[str, Any]:
    """Re-authorize via authorization_code + PKCE, silently when the server allows it."""
    metadata = discover_metadata(entry["discoveryState"]["authorizationServerUrl"])

    for silent in (True, False):
        captured, verifier = _authorization_round(entry, metadata, silent)
        if "code" in captured:
            if silent:
                log("  silent re-auth succeeded (no interaction needed)")
            break
        if not silent:
            raise SystemExit(f"No authorization code received (got {captured or 'nothing'}).")
        reason = captured.get("error") or "no response"
        log(f"  silent re-auth unavailable ({reason}) -- falling back to interactive consent")
    else:  # pragma: no cover - the loop always breaks or raises
        raise SystemExit("Authorization failed.")

    body = urllib.parse.urlencode(
        {
            "grant_type": "authorization_code",
            "code": captured["code"],
            "redirect_uri": entry["redirectUri"],
            "client_id": entry["clientId"],
            "code_verifier": verifier,
            "resource": entry["serverUrl"].removesuffix("/mcp"),
        }
    ).encode()
    request = urllib.request.Request(
        metadata["token_endpoint"],
        data=body,
        headers={"Content-Type": "application/x-www-form-urlencoded", "Accept": "application/json"},
    )
    try:
        with urllib.request.urlopen(request, timeout=HTTP_TIMEOUT_SECONDS) as response:
            token = json.loads(response.read().decode())
    except urllib.error.HTTPError as error:
        raise SystemExit(f"Token exchange failed: HTTP {error.code} {error.read().decode()[:400]}") from error

    entry["accessToken"] = token["access_token"]
    if token.get("refresh_token"):
        entry["refreshToken"] = token["refresh_token"]
    entry["expiresAt"] = int((time.time() + int(token.get("expires_in", 3600))) * 1000)
    return entry


def ensure_authenticated(server_name: str, force: bool = False) -> dict[str, Any]:
    """Return an entry whose access token is good for at least the refresh margin."""
    _, entry = find_server_entry(load_credentials(), server_name)
    remaining = seconds_remaining(entry)
    if not force and remaining > REFRESH_MARGIN_SECONDS:
        log(f"  token valid for {remaining / 60:.0f} more minutes -- no re-auth needed")
        clear_needs_auth_cache(server_name)
        return entry

    reason = "forced" if force else f"expires in {remaining / 60:.0f} min"
    log(f"  re-authenticating ({reason}) ...")
    entry = authorize(entry)
    save_server_entry(server_name, entry)
    clear_needs_auth_cache(server_name)
    log(f"  new token valid for {seconds_remaining(entry) / 60:.0f} minutes")
    return entry


# -------------------------------------------------------------------------------- mcp


def call_tool(entry: dict[str, Any], tool: str, arguments: dict[str, Any]) -> Any:
    payload = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "tools/call",
        "params": {"name": tool, "arguments": arguments},
    }
    request = urllib.request.Request(
        entry["serverUrl"],
        data=json.dumps(payload).encode(),
        headers={
            "Authorization": "Bearer " + entry["accessToken"],
            "Content-Type": "application/json",
            "Accept": "application/json, text/event-stream",
        },
    )
    with urllib.request.urlopen(request, timeout=HTTP_TIMEOUT_SECONDS) as response:
        raw = response.read().decode()

    # The gateway may answer as SSE; the JSON-RPC envelope is the last `data:` frame.
    for line in raw.splitlines():
        if line.startswith("data: "):
            raw = line[6:]
    envelope = json.loads(raw)
    if "error" in envelope:
        raise SystemExit(f"MCP error: {envelope['error']}")

    text = "".join(block.get("text", "") for block in envelope["result"].get("content", []))
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        return text


def format_issue(issue: dict[str, Any]) -> str:
    """Render one issue.

    ``priority`` is omitted rather than shown as "-" when absent: ``jira_search_issues``
    returns a reduced field set that nulls it, and printing "-" there reads as "no priority
    set" when the issue may well be High.
    """
    fields = issue.get("fields") or {}

    def named(field: str) -> str:
        value = fields.get(field)
        return (value or {}).get("name", "-") if isinstance(value, dict) else str(value or "-")

    def person(field: str) -> str:
        value = fields.get(field) or {}
        return value.get("displayName", "-")

    priority = f"  priority={named('priority')}" if fields.get("priority") else ""
    return "\n".join(
        [
            f"{issue['key']}  {fields.get('summary', '').strip()}",
            f"  type={named('issuetype')}  status={named('status')}{priority}",
            f"  assignee={person('assignee')}  reporter={person('reporter')}",
            f"  created={str(fields.get('created'))[:10]}  updated={str(fields.get('updated'))[:10]}"
            f"  due={fields.get('duedate') or '-'}",
            f"  labels={fields.get('labels') or []}",
        ]
    )


# -------------------------------------------------------------------------------- cli


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--server", default="jira", help="MCP server name in ~/.claude.json (default: jira)")
    subparsers = parser.add_subparsers(dest="command", required=True)

    subparsers.add_parser("status", help="show token expiry without touching it")
    auth_parser = subparsers.add_parser("ensure", help="re-authenticate if the token is near expiry")
    auth_parser.add_argument("--force", action="store_true", help="re-authenticate even if the token is fresh")

    issue_parser = subparsers.add_parser("issue", help="fetch one issue by key")
    issue_parser.add_argument("key")

    search_parser = subparsers.add_parser("search", help="run a JQL search")
    search_parser.add_argument("jql")
    search_parser.add_argument("--max-results", type=int, default=50)
    search_parser.add_argument("--json", action="store_true", help="emit raw JSON instead of a summary")

    args = parser.parse_args()

    if args.command == "status":
        _, entry = find_server_entry(load_credentials(), args.server)
        remaining = seconds_remaining(entry)
        expiry = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(entry["expiresAt"] / 1000))
        state = f"valid, {remaining / 60:.0f} min left" if remaining > 0 else f"EXPIRED {-remaining / 60:.0f} min ago"
        emit(f"{args.server}: {state} (expires {expiry})")
        return

    if args.command == "ensure":
        ensure_authenticated(args.server, force=args.force)
        emit(f"{args.server}: authenticated")
        return

    entry = ensure_authenticated(args.server)

    if args.command == "issue":
        issue = call_tool(entry, "jira_get_issue", {"issueIdOrKey": args.key})
        emit(format_issue(issue))
        return

    result = call_tool(
        entry,
        "jira_search_issues",
        {"jql": args.jql, "maxResults": args.max_results},
    )
    if args.json:
        emit(json.dumps(result, indent=2))
        return
    issues = result.get("issues", [])
    emit(f"{result.get('total', len(issues))} match(es); showing {len(issues)}")
    emit("(search omits priority and description -- run `issue <KEY>` for the full record)\n")
    for issue in issues:
        emit(format_issue(issue))
        emit()


if __name__ == "__main__":
    main()
```
