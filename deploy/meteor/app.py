"""
Aetherstream party service.

One instance, many users. Every plugin install registers itself, any user can create party groups
and share a short code, and whoever holds the code joins and watches together. Nobody self-hosts;
this is the server everyone connects to.

Shaped after how Mare Synchronos works: the client generates its own secret key, the server never
learns a password, and identity is just "whoever can present that key".

Two things it is careful about:

  * A user may only publish to a path bound to a group they own. MediaMTX asks this service before
    accepting a stream, so a shared publish password cannot be used to hijack someone else's party.
  * A group's stream path is never in the code. It is handed out only to members, and only while
    somebody is actually streaming.

Stdlib only - nothing to patch at 2am.
"""

import hashlib
import json
import os
import re
import secrets
import threading
import time
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

STATE_PATH = os.environ.get("PARTY_STATE", "/data/state.json")
WATCH_HOST = os.environ.get("PARTY_WATCH_HOST", "")
RELAY_HOST = os.environ.get("PARTY_RELAY_HOST", "")
SRT_PASSPHRASE = os.environ.get("PARTY_SRT_PASSPHRASE", "")

# Crockford base32 without I, L, O and U: nothing that gets misread when a code is spoken aloud
# or typed off a screenshot.
ALPHABET = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"
CODE_RE = re.compile(r"^[0-9A-HJKMNP-TV-Z]{6}$")
PATH_RE = re.compile(r"^party-[0-9a-f]{24}$")

# A live group stops being live on its own. Without this, a host whose game crashes leaves a group
# advertising a stream nobody is feeding, and only that machine could ever clear it.
LIVE_TTL_SECONDS = 60

MAX_GROUPS_PER_USER = 20
MAX_MEMBERS = 50

_lock = threading.RLock()
_state = {"users": {}, "groups": {}}


# -- storage ---------------------------------------------------------------------------------

def load():
    global _state
    try:
        with open(STATE_PATH, "r", encoding="utf-8") as handle:
            _state = json.load(handle)
    except (FileNotFoundError, json.JSONDecodeError):
        _state = {}
    _state.setdefault("users", {})
    _state.setdefault("groups", {})


def save():
    # Atomic replace: a half-written file would lose every group, and groups are permanent.
    tmp = STATE_PATH + ".tmp"
    os.makedirs(os.path.dirname(STATE_PATH), exist_ok=True)
    with open(tmp, "w", encoding="utf-8") as handle:
        json.dump(_state, handle)
    os.replace(tmp, STATE_PATH)


def user_id(key):
    """Identity is derived from the key, so the key itself is never stored."""
    return hashlib.sha256(("aetherstream:" + key).encode()).hexdigest()[:24]


def new_code():
    while True:
        code = "".join(secrets.choice(ALPHABET) for _ in range(6))
        if code not in _state["groups"]:
            return code


def is_live(group):
    return bool(group.get("path")) and (time.time() - group.get("live_at", 0)) < LIVE_TTL_SECONDS


def visible(group, code, uid):
    """What a member is allowed to know. The path is in here only while somebody is streaming."""
    body = {
        "code": code,
        "name": group.get("name", ""),
        "owner": group.get("owner") == uid,
        "members": len(group.get("members", [])),
        "live": is_live(group),
    }

    # The owner needs the path to push to, and is the only one who ever gets it directly.
    if group.get("owner") == uid:
        body["streamPath"] = group.get("stream_path", "")

    if is_live(group):
        body["title"] = group.get("title", "")
        body["watch"] = f"https://{WATCH_HOST}/{group['path']}/index.m3u8"
        body["screen"] = group.get("screen")

    return body


# -- http ------------------------------------------------------------------------------------

class Handler(BaseHTTPRequestHandler):
    server_version = "aetherstream-party"

    def log_message(self, *args):
        pass  # An access log here would be a record of who watches what, and when.

    def _send(self, status, payload):
        body = json.dumps(payload).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _body(self):
        try:
            length = int(self.headers.get("Content-Length", 0))
            return json.loads(self.rfile.read(length) or b"{}")
        except (ValueError, json.JSONDecodeError):
            return {}

    def _caller(self):
        """The user behind this request, registering them on first sight."""
        key = self.headers.get("X-Party-Key", "").strip()
        if len(key) < 32:
            return None

        uid = user_id(key)
        with _lock:
            if uid not in _state["users"]:
                _state["users"][uid] = {"since": time.time(), "name": ""}
                save()
        return uid

    def _parts(self):
        return [p for p in self.path.split("?")[0].split("/") if p]

    # -- reads ---------------------------------------------------------------------------------

    def do_GET(self):
        parts = self._parts()

        if parts == ["health"]:
            return self._send(HTTPStatus.OK, {"ok": True})

        uid = self._caller()
        if uid is None:
            return self._send(HTTPStatus.UNAUTHORIZED, {"error": "no key"})

        # GET /me - everything this user's plugin needs on startup.
        if parts == ["me"]:
            with _lock:
                groups = [
                    visible(g, c, uid)
                    for c, g in _state["groups"].items()
                    if uid in g.get("members", []) or g.get("owner") == uid
                ]
                return self._send(HTTPStatus.OK, {
                    "user": uid,
                    "relay": RELAY_HOST,
                    "watchHost": WATCH_HOST,
                    "srtPassphrase": SRT_PASSPHRASE,
                    "groups": groups,
                })

        # GET /g/<code> - members only. A stranger learns nothing, not even the name.
        if len(parts) == 2 and parts[0] == "g":
            code = parts[1].upper().replace("-", "")
            if not CODE_RE.match(code):
                return self._send(HTTPStatus.BAD_REQUEST, {"error": "malformed code"})

            with _lock:
                group = _state["groups"].get(code)
                if not group:
                    return self._send(HTTPStatus.NOT_FOUND, {"error": "no such party"})

                if uid not in group.get("members", []) and group.get("owner") != uid:
                    return self._send(HTTPStatus.FORBIDDEN, {"error": "not a member"})

                return self._send(HTTPStatus.OK, visible(group, code, uid))

        return self._send(HTTPStatus.NOT_FOUND, {"error": "no"})

    # -- writes --------------------------------------------------------------------------------

    def do_POST(self):
        parts = self._parts()

        # MediaMTX asks before it accepts a stream. Not a user route - no key involved.
        if parts == ["auth"]:
            return self._authorise(self._body())

        uid = self._caller()
        if uid is None:
            return self._send(HTTPStatus.UNAUTHORIZED, {"error": "no key"})

        body = self._body()

        # POST /g - create a party group. Whoever creates it owns it.
        if parts == ["g"]:
            with _lock:
                owned = sum(1 for g in _state["groups"].values() if g.get("owner") == uid)
                if owned >= MAX_GROUPS_PER_USER:
                    return self._send(HTTPStatus.TOO_MANY_REQUESTS, {"error": "too many groups"})

                code = new_code()
                _state["groups"][code] = {
                    "name": str(body.get("name", ""))[:60],
                    "owner": uid,
                    "members": [uid],
                    # Bound now and never reused: the path is what MediaMTX authorises against.
                    "path": None,
                    "stream_path": "party-" + secrets.token_hex(12),
                    "live_at": 0,
                }
                save()
                return self._send(HTTPStatus.OK, visible(_state["groups"][code], code, uid))

        if len(parts) >= 2 and parts[0] == "g":
            code = parts[1].upper().replace("-", "")
            if not CODE_RE.match(code):
                return self._send(HTTPStatus.BAD_REQUEST, {"error": "malformed code"})

            with _lock:
                group = _state["groups"].get(code)
                if not group:
                    return self._send(HTTPStatus.NOT_FOUND, {"error": "no such party"})

                # POST /g/<code>/join
                if parts[2:] == ["join"]:
                    if len(group["members"]) >= MAX_MEMBERS:
                        return self._send(HTTPStatus.TOO_MANY_REQUESTS, {"error": "party is full"})

                    if uid not in group["members"]:
                        group["members"].append(uid)
                        save()

                    return self._send(HTTPStatus.OK, visible(group, code, uid))

                if parts[2:] == ["leave"]:
                    if uid in group["members"]:
                        group["members"].remove(uid)
                        save()
                    return self._send(HTTPStatus.OK, {"ok": True})

                # POST /g/<code>/live - owner only, and the heartbeat.
                if parts[2:] == ["live"]:
                    if group.get("owner") != uid:
                        return self._send(HTTPStatus.FORBIDDEN, {"error": "not the owner"})

                    if body.get("live"):
                        group["title"] = str(body.get("title", ""))[:120]
                        group["screen"] = body.get("screen")
                        group["path"] = group["stream_path"]
                        group["live_at"] = time.time()
                    else:
                        group["path"] = None
                        group["live_at"] = 0

                    save()
                    return self._send(HTTPStatus.OK, visible(group, code, uid))

                # POST /g/<code>/rotate - new stream path, and everyone re-reads it when live.
                if parts[2:] == ["rotate"]:
                    if group.get("owner") != uid:
                        return self._send(HTTPStatus.FORBIDDEN, {"error": "not the owner"})

                    group["stream_path"] = "party-" + secrets.token_hex(12)
                    group["path"] = None
                    group["live_at"] = 0
                    save()
                    return self._send(HTTPStatus.OK, {"ok": True})

        return self._send(HTTPStatus.NOT_FOUND, {"error": "no"})

    def do_DELETE(self):
        uid = self._caller()
        if uid is None:
            return self._send(HTTPStatus.UNAUTHORIZED, {"error": "no key"})

        parts = self._parts()
        if len(parts) == 2 and parts[0] == "g":
            code = parts[1].upper().replace("-", "")
            with _lock:
                group = _state["groups"].get(code)
                if not group:
                    return self._send(HTTPStatus.NOT_FOUND, {"error": "no such party"})
                if group.get("owner") != uid:
                    return self._send(HTTPStatus.FORBIDDEN, {"error": "not the owner"})

                del _state["groups"][code]
                save()
            return self._send(HTTPStatus.OK, {"ok": True})

        return self._send(HTTPStatus.NOT_FOUND, {"error": "no"})

    # -- the relay asking permission -----------------------------------------------------------

    def _authorise(self, body):
        """
        MediaMTX calls this before every publish and every read.

        Publishing is the one that matters. With a single shared password any user could push to
        another user's path and take over their movie night, so a publish is allowed only when the
        caller owns the group that path belongs to.
        """
        action = body.get("action", "")
        path = body.get("path", "")

        if action == "read":
            # Reads stay open: the path is 96 random bits and is only ever given to members, and
            # requiring credentials here would mean teaching libvlc to authenticate mid-playlist.
            return self._send(HTTPStatus.OK, {})

        if action != "publish" or not PATH_RE.match(path):
            return self._send(HTTPStatus.UNAUTHORIZED, {})

        key = body.get("password", "")
        if len(key) < 32:
            return self._send(HTTPStatus.UNAUTHORIZED, {})

        uid = user_id(key)
        with _lock:
            for group in _state["groups"].values():
                if group.get("stream_path") == path and group.get("owner") == uid:
                    return self._send(HTTPStatus.OK, {})

        return self._send(HTTPStatus.UNAUTHORIZED, {})


if __name__ == "__main__":
    load()
    ThreadingHTTPServer(("0.0.0.0", 8099), Handler).serve_forever()
