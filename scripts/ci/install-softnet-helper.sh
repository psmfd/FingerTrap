#!/bin/bash
# One-time HOST administrator step. Installs only the reviewed Softnet binary.
set -euo pipefail
test "$(id -u)" = 0
source=/opt/homebrew/Cellar/softnet/0.19.0/bin/softnet
expected=ab333619fc8bd7277837545e49a771baa994c01c3e8c14904ae4cc4c1f37269e
destination=/Library/PrivilegedHelperTools/fingertrap-softnet
printf '%s  %s\n' "$expected" "$source" | shasum -a 256 -c -
test ! -L "$destination"
test ! -L "$destination/softnet"
/usr/bin/install -d -o root -g wheel -m 0755 "$destination"
/usr/bin/install -o root -g wheel -m 4755 "$source" "$destination/softnet"
printf '%s  %s\n' "$expected" "$destination/softnet" | shasum -a 256 -c -
stat -f '%Su %Sg %Sp %N' "$destination/softnet"
