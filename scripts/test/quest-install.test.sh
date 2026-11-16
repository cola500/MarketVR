#!/usr/bin/env bash
# Tests for scripts/quest.sh's device handling and install (wireless ADB): a fake adb records every call and answers
# from FAKE_* variables; the APK is a real archived build (read-only, never installed anywhere).
# Run: scripts/test/quest-install.test.sh
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# Safety: this test must never reach a real headset. quest.sh has to take ADB from the environment, else stop here.
grep -q 'ADB="${ADB:-' "$ROOT/scripts/quest.sh" || { echo "[quest-install] quest.sh ignores ADB=…: refusing to run (it would call the real adb)" >&2; exit 2; }
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
pass=0; fail=0

cat > "$WORK/adb" <<'ADB'
#!/usr/bin/env bash
echo "$*" >> "$FAKE_LOG"
serial=""
if [[ "${1:-}" == "-s" ]]; then serial="$2"; shift 2; fi
case "$*" in
  devices) echo "List of devices attached"; for d in ${FAKE_DEVICES:-}; do printf '%s\tdevice\n' "$d"; done ;;
  "shell getprop ro.serialno") eval "echo \${FAKE_HW_$(echo "$serial" | tr -c 'A-Za-z0-9\n' '_')}" ;;
  "shell ip -f inet addr show wlan0") [[ -n "${FAKE_IP:-}" ]] && printf '    inet %s/24 brd 192.168.1.255 scope global wlan0\n' "$FAKE_IP" ;;
  "tcpip 5555") echo "restarting in TCP mode port: 5555" ;;
  connect*) if [[ "${FAKE_CONNECT_OK:-1}" == 1 ]]; then echo "connected to ${*#connect }"; else echo "failed to connect to ${*#connect }"; fi ;;
  disconnect*) echo "disconnected ${*#disconnect }" ;;
  get-state) [[ "${FAKE_CONNECT_OK:-1}" == 1 ]] && echo device || { echo "error: device not found" >&2; exit 1; } ;;
  "shell dumpsys activity activities") echo "  topResumedActivity=ActivityRecord{1 u0 ${FAKE_TOP:-com.oculus.vrshell/.MainActivity} t1}" ;;
  "shell dumpsys package com.lillafruktstandet.prototype") printf '    versionCode=1 minSdk=32 targetSdk=32\n    versionName=%s\n    lastUpdateTime=2026-11-01 18:30:00\n' "${FAKE_VERSION:-1.0+old}" ;;
  install*) echo "Performing Streamed Install"; echo "Success" ;;
  *) echo "fake adb: unhandled: $*" >&2 ;;
esac
ADB
chmod +x "$WORK/adb"

APK="$(ls "$ROOT"/UnityProject/Builds/archive/lilla-fruktstandet_*.apk | grep -v dirty | tail -1)"
ID="$(basename "$APK" .apk)"; ID="${ID#lilla-fruktstandet_}"

run() { # run <name> <expected exit> <env...> -- <quest.sh args...>
  local name="$1" want="$2"; shift 2
  local envs=(); while [[ "$1" != "--" ]]; do envs+=("$1"); shift; done; shift
  : > "$WORK/log"
  OUT="$(env ADB="$WORK/adb" FAKE_LOG="$WORK/log" QUEST_WIFI_FILE="$WORK/wifi" "${envs[@]}" "$ROOT/scripts/quest.sh" "$@" 2>&1)"
  CODE=$?
  if [[ "$CODE" == "$want" ]]; then return 0; fi
  echo "FAIL $name: exit $CODE (wanted $want)"; echo "$OUT" | sed 's/^/    /'; fail=$((fail + 1)); return 1
}
ok() { pass=$((pass + 1)); echo "ok   $1"; }
bad() { fail=$((fail + 1)); echo "FAIL $1"; echo "$OUT" | sed 's/^/    /'; sed 's/^/    adb /' "$WORK/log"; }
logged() { grep -qE -- "$1" "$WORK/log"; }

# 0. The script starts without any test-only variables (defaults resolve).
: > "$WORK/log"
OUT="$(env ADB="$WORK/adb" FAKE_LOG="$WORK/log" FAKE_DEVICES= "$ROOT/scripts/quest.sh" devices 2>&1)"; CODE=$?
if [[ $CODE == 0 ]] && grep -q "none" <<<"$OUT"; then ok "starts with the default settings"; else bad "default settings"; fi

# 1. No device: a clear message, nothing else.
run "no device" 4 FAKE_DEVICES= -- installed && { grep -q "USB" <<<"$OUT" && grep -q "quest.sh connect" <<<"$OUT" && ok "no device: says USB or connect" || bad "no device message"; }

# 2. The same headset on USB and on wifi: no 'multiple devices'; every call names one device (adb -s).
run "usb+wifi" 0 "FAKE_DEVICES=1WMHH000X 192.168.1.42:5555" FAKE_HW_1WMHH000X=Q1 FAKE_HW_192_168_1_42_5555=Q1 FAKE_VERSION="1.0+$ID" -- installed &&
  { ! grep -vE '^(devices|-s )' "$WORK/log" | grep -q . && logged '^-s 1WMHH000X shell dumpsys package' && grep -q "USB" <<<"$OUT" && ok "same headset twice: USB chosen, adb -s everywhere" || bad "usb+wifi target"; }

# 3. Two different headsets: refuse to guess.
run "two headsets" 4 "FAKE_DEVICES=AAA BBB" FAKE_HW_AAA=Q1 FAKE_HW_BBB=Q2 -- installed && { grep -q "QUEST_DEVICE" <<<"$OUT" && ok "two headsets: asks for QUEST_DEVICE" || bad "two headsets"; }
run "explicit device" 0 "FAKE_DEVICES=AAA BBB" FAKE_HW_AAA=Q1 FAKE_HW_BBB=Q2 QUEST_DEVICE=BBB -- installed && { logged '^-s BBB shell dumpsys package' && ok "QUEST_DEVICE picks it" || bad "explicit device"; }

# 4. wifi: from exactly one USB headset to its wifi address; the address is remembered.
run "wifi" 0 FAKE_DEVICES=1WMHH000X FAKE_HW_1WMHH000X=Q1 FAKE_IP=192.168.1.42 -- wifi &&
  { logged '^-s 1WMHH000X tcpip 5555' && logged '^connect 192.168.1.42:5555' && [[ "$(cat "$WORK/wifi")" == "192.168.1.42" ]] && grep -qi "omstart\|restart" <<<"$OUT" && ok "wifi: tcpip, connect, remembered, explains restart" || bad "wifi"; }
run "wifi without wlan" 4 FAKE_DEVICES=1WMHH000X FAKE_HW_1WMHH000X=Q1 FAKE_IP= -- wifi && { grep -qi "wifi" <<<"$OUT" && ok "wifi: headset not on wifi is explained" || bad "wifi no ip"; }
run "wifi needs usb" 4 FAKE_DEVICES=192.168.1.42:5555 FAKE_HW_192_168_1_42_5555=Q1 -- wifi && ok "wifi: needs the headset on USB first"

# 5. connect: the remembered address; a failure explains sleep/restart.
echo 192.168.1.42 > "$WORK/wifi"
run "connect" 0 FAKE_DEVICES= -- connect && { logged '^connect 192.168.1.42:5555' && ok "connect: the remembered address" || bad "connect"; }
run "connect fails" 4 FAKE_DEVICES= FAKE_CONNECT_OK=0 -- connect && { grep -qi "usb" <<<"$OUT" && ok "connect failure: wake it, after a restart USB + wifi again" || bad "connect fail msg"; }

# 6. install: an explicit archived build, manifest checked, never while the app is in front, verified afterwards.
run "install needs a choice" 5 FAKE_DEVICES=X FAKE_HW_X=Q1 -- install && { ! logged 'install' && ok "install without a build: lists builds, installs nothing" || bad "install no arg"; }
run "install while playing" 6 FAKE_DEVICES=X FAKE_HW_X=Q1 FAKE_TOP=com.lillafruktstandet.prototype/com.unity3d.player.UnityPlayerGameActivity -- install "$ID" &&
  { ! logged '^-s X install' && grep -qi "spelar\|playing" <<<"$OUT" && ok "install refused while the app is in front" || bad "install while playing"; }
run "install" 0 "FAKE_DEVICES=X 10.0.0.9:5555" FAKE_HW_X=Q1 FAKE_HW_10_0_0_9_5555=Q1 QUEST_DEVICE=10.0.0.9:5555 FAKE_VERSION="1.0+$ID" -- install "$ID" &&
  { logged "^-s 10.0.0.9:5555 install -r .*$ID.apk" && grep -q "verified" <<<"$OUT" && ok "install over wifi with adb -s, verified" || bad "install"; }
run "install unverified" 7 FAKE_DEVICES=X FAKE_HW_X=Q1 FAKE_VERSION=1.0+something-else -- install "$ID" && ok "install: a different build on the headset afterwards fails"
cp "$APK" "$WORK/copy.apk"
run "install outside the archive" 5 FAKE_DEVICES=X FAKE_HW_X=Q1 -- install "$WORK/copy.apk" && { ! logged 'install -r' && ok "install: only archived builds with a matching manifest" || bad "install unknown"; }

echo "[quest-install] $pass passed, $fail failed"
[[ $fail == 0 ]]
