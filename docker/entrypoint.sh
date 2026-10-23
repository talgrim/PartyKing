#!/bin/sh
# Starts Kestrel (as the unprivileged app user) and nginx, and stops the container when either one exits.
set -eu

mkdir -p /data/wwwroot /data/keys
chown -R app:app /data

setpriv --reuid=app --regid=app --init-groups dotnet /app/PartyKing.API.dll &
api_pid=$!

nginx -g 'daemon off;' &
nginx_pid=$!

trap 'kill "$api_pid" "$nginx_pid" 2>/dev/null || true' TERM INT

# POSIX sh has no `wait -n`: poll until one of the two is gone
while kill -0 "$api_pid" 2>/dev/null && kill -0 "$nginx_pid" 2>/dev/null; do
    sleep 2
done

kill "$api_pid" "$nginx_pid" 2>/dev/null || true
exit 1
