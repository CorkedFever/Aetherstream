#!/usr/bin/env bash
#
# Prints the two codes for this party.
#
#   ./code.sh party.example.com:8443
#
# The host code configures the Share tab in one paste and carries the publish credentials, so it is
# yours alone. The invite code carries only where to watch, and is the one to send people.

set -euo pipefail
cd "$(dirname "$0")"

WATCH_HOST="${1:-}"
if [ -z "$WATCH_HOST" ]; then
    echo "usage: $0 <watch-host:port>"
    echo "  e.g. $0 party.example.com:8443"
    echo
    echo "The port matters: the stream is served on an HTTP/1.1-only listener."
    exit 1
fi

[ -f party.env ] || { echo "party.env not found - run ./setup.sh first."; exit 1; }
# shellcheck disable=SC1091
. ./party.env

# A fresh path every run, unless one is given. The relay accepts any path matching
# ~^party-[0-9a-f]{24}$, so a new party needs no server change at all - and every previously
# shared invite is left pointing at a stream nobody is publishing to, which is the only
# revocation there is.
#
#   ./code.sh <watch-host>            new party
#   ./code.sh <watch-host> --keep     reuse the path in party.env
if [ "${2:-}" = "--keep" ]; then
    echo "(reusing the existing path)"
else
    PARTY_PATH="party-$(openssl rand -hex 12)"
fi

# Fields are joined with a unit separator, which cannot occur in a host, path or secret, then
# base64url-encoded. This has to match PartyCode.cs exactly.
US=$(printf '\037')
payload="1${US}${WATCH_HOST}${US}${PARTY_PATH}"
join=$(printf '%s' "$payload" | base64 -w0 | tr '+/' '-_' | tr -d '=')

payload="1${US}${WATCH_HOST}${US}${PARTY_PATH}${US}${PARTY_SERVER:-$(curl -s -4 ifconfig.me 2>/dev/null)}${US}${PARTY_PUBLISH_PASS}${US}${PARTY_SRT_PASSPHRASE}"
host=$(printf '%s' "$payload" | base64 -w0 | tr '+/' '-_' | tr -d '=')

echo
echo "HOST CODE - keep this. Paste it into the plugin's Share tab."
echo "  AETHER-H-${host}"
echo
echo "INVITE CODE - send this to the room. They paste it and press Play."
echo "  AETHER-J-${join}"
echo
echo "Path for this party: ${PARTY_PATH}"
echo "Anyone holding an invite from an earlier run can no longer watch."
echo
