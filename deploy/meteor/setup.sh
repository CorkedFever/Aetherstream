#!/usr/bin/env bash
#
# One-time setup: generates fresh secrets and writes the real config files from the templates.
# Refuses to overwrite, so re-running cannot silently invalidate codes already shared.
#
#   ./setup.sh aetherstream.example.com

set -euo pipefail
cd "$(dirname "$0")"

DOMAIN="${1:?usage: $0 <domain>}"

for f in mediamtx.yml api.env; do
    [ -f "$f" ] && { echo "$f already exists — leaving everything alone."; exit 0; }
done

command -v openssl >/dev/null || { echo "openssl is required"; exit 1; }

SRT_PASSPHRASE="$(openssl rand -hex 16)"

sed -e "s|__SRT_PASSPHRASE__|${SRT_PASSPHRASE}|" mediamtx.template.yml > mediamtx.yml
chmod 600 mediamtx.yml

cat > api.env <<VARS
PARTY_WATCH_HOST=${DOMAIN}:8443
PARTY_RELAY_HOST=${DOMAIN}
PARTY_SRT_PASSPHRASE=${SRT_PASSPHRASE}
PARTY_STATE=/data/state.json
VARS
chmod 600 api.env

echo "Wrote mediamtx.yml and api.env for ${DOMAIN}."
echo "TLS and routing for ${DOMAIN} are in corkedfever-website's deploy/meteor Caddyfile; start that stack first."
echo "Start this one with: docker compose up -d"
echo "Plugin side: everyone connects the Share tab to ${DOMAIN} — that is the whole setup."
