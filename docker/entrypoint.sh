#!/bin/bash
# Starts Kestrel (as the unprivileged app user) and nginx. Stops the container when either one exits.
set -eu

# Fly's edge overwrites Fly-Client-IP and X-Forwarded-Proto, so they can be trusted there (fly.toml sets the flag).
# Anywhere else a client could send them itself, which would let it dodge the login rate limit.
if [ "${TRUST_FLY_CLIENT_IP:-0}" = "1" ]; then
    cat > /tmp/forwarded-headers.conf <<'CONF'
map $http_fly_client_ip $client_ip {
    default $http_fly_client_ip;
    ""      $remote_addr;
}
map $http_x_forwarded_proto $forwarded_proto {
    default $http_x_forwarded_proto;
    ""      $scheme;
}
CONF
else
    cat > /tmp/forwarded-headers.conf <<'CONF'
map $remote_addr $client_ip {
    default $remote_addr;
}
map $scheme $forwarded_proto {
    default $scheme;
}
CONF
fi

# Only touch ownership when it is wrong: a recursive chown on every boot gets slow with many photos
for dir in /data /data/wwwroot /data/keys; do
    mkdir -p "$dir"
    if [ "$(stat -c %U "$dir")" != "app" ]; then
        chown app:app "$dir"
    fi
done

setpriv --reuid=app --regid=app --init-groups dotnet /app/PartyKing.API.dll &
api_pid=$!

nginx -g 'daemon off;' &
nginx_pid=$!

stopping=0
trap 'stopping=1; kill -TERM "$api_pid" "$nginx_pid" 2>/dev/null || true' TERM INT

# Returns as soon as either process exits or a trapped signal arrives
wait -n || true

kill -TERM "$api_pid" "$nginx_pid" 2>/dev/null || true
wait "$api_pid" "$nginx_pid" 2>/dev/null || true

# A requested stop is a clean exit; one process dying on its own is a failure
if [ "$stopping" = "1" ]; then
    exit 0
fi
exit 1
