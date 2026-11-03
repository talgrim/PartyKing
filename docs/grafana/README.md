# Grafana dashboards

`game-write-lock.json` charts `partyking.games.write_lock.wait`, the time a Resistance or Ringo write waits for the
session's write lock (Prometheus names: `partyking_games_write_lock_wait_milliseconds_{bucket,sum,count}`, labels
`repository`, `operation`, `acquired`).

Import it in Fly's managed Grafana (Dashboards > New > Import > Upload JSON file) and pick the Fly Prometheus data
source. Alert rules live in Grafana, not in this repository. Suggested starting points, from the panels' descriptions:

- p95 wait above 500 ms for 10 minutes;
- any sustained rate of cancelled waits (`acquired="false"`).
