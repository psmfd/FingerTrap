#!/bin/bash
# Run INSIDE a fresh macOS ARM64 Tart base image, before cloning job VMs.
set -euo pipefail
test "$(uname -s)" = Darwin
test "$(uname -m)" = arm64
xcode-select -p >/dev/null
HOMEBREW_NO_AUTO_UPDATE=1 /opt/homebrew/bin/brew install bash
/opt/homebrew/bin/bash -c 'test "${BASH_VERSINFO[0]}" -ge 4'
test ! -e "$HOME/fingertrap-actions-runner"
mkdir "$HOME/fingertrap-actions-runner"
cd "$HOME/fingertrap-actions-runner"
archive=actions-runner-osx-arm64-2.338.0.tar.gz
curl --fail --location --retry 3 --output "$archive" \
  "https://github.com/actions/runner/releases/download/v2.338.0/$archive"
printf '%s  %s\n' \
  df4cebda25c86a886ed204e49fee63f5c2e7cec5f447b5c98440a826bbdf9df2 \
  "$archive" | shasum -a 256 -c -
tar -xzf "$archive"
rm "$archive"
# Keep the template unregistered. No repository or operator credentials enter it.
test ! -e .runner
test ! -e .credentials
./bin/Runner.Listener --version
sw_vers
sync
