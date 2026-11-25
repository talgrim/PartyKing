# Grafana dashboards

`game-write-lock.json` charts `partyking.games.write_lock.wait`, the time a Resistance or Ringo write waits for the
session's write lock (Prometheus names: `partyking_games_write_lock_wait_milliseconds_{bucket,sum,count}`, labels
`repository`, `operation`, `acquired`).

`resistance-fetches.json` shows how many fetches a Resistance action causes: GET game and GET lobby against the
successful writes (`partyking_controller_action_duration_milliseconds_*`, labels `controller`, `action`,
`http_request_method`, `outcome`), the GETs per write, and how many game reads joined a query already in flight
(`partyking_games_reads_total`, labels `repository`, `shared`). Requests that fail authorization before the action
runs (401) are not in the first group. Its top row is the machine, from Fly's own metrics (not the app's): the CPU balance
(`fly_instance_cpu_balance`) and the CPU by mode (`fly_instance_cpu`, whose `steal` shows the throttling once the balance is
0). The latency panels jumping to seconds while the balance is 0 and steal is high is throttling, not load.

`identity.json` shows PartyKing's calls to the identity service (`auth.talgrim.dev`, `GET /api/session`): calls and their
latency by outcome (`partyking_identity_calls_total`, `partyking_identity_call_duration_milliseconds_*`, label `outcome`:
`account`, `anonymous`, `forbidden`, `unavailable`), the cache hit ratio (`partyking_identity_cache_hits_total`) and the
answers served from an expired entry during an outage (`partyking_identity_stale_total`). Requests without the session
cookie never call the service, so guests are not in it.

Import them in Fly's managed Grafana (Dashboards > New > Import > Upload JSON file) and pick the Fly Prometheus data
source. Alert rules live in Grafana, not in this repository. Suggested starting points, from the panels' descriptions:

- p95 wait above 500 ms for 10 minutes;
- any sustained rate of cancelled waits (`acquired="false"`).
- identity calls with outcome `unavailable` above 5% for 5 minutes, or any sustained rate of stale answers (the identity
  service is down or unreachable).
