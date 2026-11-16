#!/usr/bin/env bash
#
# scripts/kund.sh — the phone customer relay (EPIC-004) on this MacBook. Local network only, nothing is saved.
#
# Usage:
#   scripts/kund.sh start     # start the relay: QR code + session code in the terminal, host page in the browser
#   scripts/kund.sh test      # relay tests (node --test)
#   scripts/kund.sh spike [beacon]  # Editor chain: relay + Unity batchmode PlayMode tests RelayChain_*
#                                   #   (beacon: the Unity side finds the relay via the UDP beacon, as on the Quest)
#
# Needs Node 22+ (no npm packages). The first time, macOS may ask whether "node" may accept incoming network
# connections: answer Tillåt (Allow). This script never changes firewall settings itself.

set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NODE="${NODE:-$(command -v node || true)}"
UNITY_VERSION="${UNITY_VERSION:-6000.4.7f1}"
UNITY="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
PORT="${PORT:-8787}"

need_node() {
    [[ -n "$NODE" ]] || { echo "[kund] Node.js saknas. Installera: brew install node" >&2; exit 2; }
    local major
    major="$("$NODE" -p 'process.versions.node.split(".")[0]')"
    (( major >= 22 )) || { echo "[kund] Node $major är för gammal (behöver 22 eller senare)" >&2; exit 2; }
}

start() {
    need_node
    ( sleep 1; open "http://localhost:${PORT}/host" >/dev/null 2>&1 || true ) &
    exec "$NODE" "${ROOT}/relay/server.mjs" --port "$PORT" "$@"
}

spike() {
    need_node
    local code=4821 log="${ROOT}/UnityProject/Logs/relay-spike.log"
    local discover=0 beacon=--no-beacon
    [[ "${1:-}" == beacon ]] && { discover=1; beacon=; }
    "$NODE" "${ROOT}/relay/server.mjs" --port "$PORT" --code "$code" $beacon > "$log" 2>&1 &
    RELAY_PID=$!
    trap 'kill "$RELAY_PID" 2>/dev/null || true' EXIT
    sleep 1
    kill -0 "$RELAY_PID" 2>/dev/null || { echo "[kund] relay did not start, see $log" >&2; exit 3; }
    echo "[kund] relay on 127.0.0.1:${PORT} (pid $RELAY_PID); running RelayChain_* in the Editor (batchmode)"
    local status=0
    FRUKTSTAND_RELAY="127.0.0.1:${PORT}" FRUKTSTAND_CODE="$code" FRUKTSTAND_DISCOVER="$discover" "$UNITY" -batchmode -projectPath "${ROOT}/UnityProject" \
        -runTests -testPlatform PlayMode -testFilter "RelayChain_" \
        -testResults "${ROOT}/UnityProject/Logs/relay-spike-results.xml" -logFile "${ROOT}/UnityProject/Logs/relay-spike-unity.log" || status=1
    grep -m1 -o '<test-run [^>]*' "${ROOT}/UnityProject/Logs/relay-spike-results.xml" | sed -E 's/.*total="([0-9]+)".*passed="([0-9]+)".*failed="([0-9]+)".*/[kund] Editor chain: \2 passed, \3 failed (of \1)/'
    echo "[kund] relay log:"; sed 's/^/  /' "$log"
    return $status
}

case "${1:-}" in
    start) shift; start "$@" ;;
    test) need_node; "$NODE" --test "${ROOT}/relay/test/" ;;
    spike) spike "${2:-}" ;;
    *) sed -n '2,13p' "$0"; exit 1 ;;
esac
