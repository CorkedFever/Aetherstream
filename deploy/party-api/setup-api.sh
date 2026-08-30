#!/usr/bin/env bash
# Generates the owner token and wires the API to the relay's existing credentials.
set -euo pipefail
cd "$(dirname "$0")"

[ -f api.env ] && { echo "api.env exists - leaving it alone."; exit 0; }
[ -f ../party-stream/party.env ] || { echo "run party-stream/setup.sh first"; exit 1; }
# shellcheck disable=SC1091
. ../party-stream/party.env

WATCH_HOST="${1:?usage: $0 <watch-host:port>}"

cat > api.env <<VARS
PARTY_OWNER_TOKEN=$(openssl rand -hex 16)
PARTY_WATCH_HOST=${WATCH_HOST}
PARTY_RELAY_HOST=${PARTY_SERVER}
PARTY_PUBLISH_PASS=${PARTY_PUBLISH_PASS}
PARTY_SRT_PASSPHRASE=${PARTY_SRT_PASSPHRASE}
PARTY_STATE=/data/state.json
VARS
chmod 600 api.env
TOKEN=$(grep '^PARTY_OWNER_TOKEN=' api.env | cut -d= -f2)
API_HOST="${2:-$WATCH_HOST}"
API_HOST="${API_HOST%%:*}"
echo
echo "SETUP LINE - paste this one line into the plugin's Share tab."
echo "  ${API_HOST}/${TOKEN}"
echo
