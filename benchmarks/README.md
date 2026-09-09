# Load Testing Commands

## Prerequisites

```bash
# Install k6
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update && sudo apt-get install k6

# Install Artillery
npm install -g artillery

# Install MQTT client (for mqtt-load-test.js)
npm install mqtt
```

## Start Infrastructure

```bash
docker-compose up -d
cd BattleTanks-Backend && dotnet run
```

## k6 Tests

### Basic API Test (register, login, rooms, maps, leaderboard)

```bash
k6 run benchmarks/k6.test.js
```

### Auth Load Test (100 concurrent users)

```bash
k6 run benchmarks/k6-auth-load.js
```

### SignalR WebSocket Test (20 VUs)

```bash
k6 run benchmarks/k6-signalr-load.js
```

### Room Operations Test

```bash
k6 run benchmarks/k6-rooms-load.js
```

### With InfluxDB Output (for Grafana visualization)

```bash
k6 run --out influxdb=http://localhost:8086/k6 benchmarks/k6.test.js
k6 run --out influxdb=http://localhost:8086/k6 benchmarks/k6-auth-load.js
```

## Artillery Tests

### HTTP Load Test

```bash
artillery run benchmarks/artillery.test.yml
```

### With JSON Report

```bash
artillery run --output results.json benchmarks/artillery.test.yml
artillery report results.json
```

## MQTT Load Test

```bash
node benchmarks/mqtt-load-test.js
```

### With Custom Parameters

```bash
NUM_CLIENTS=100 TEST_DURATION=120000 node benchmarks/mqtt-load-test.js
```

## Grafana Dashboard

After running k6 tests with InfluxDB output:

1. Open http://localhost:3000
2. Login: admin / battletanks
3. Go to Dashboards > k6 Load Testing Dashboard

## Test Results Reference

| Test | VUs | Duration | Requests | Error Rate | p95 Latency |
|------|-----|----------|----------|------------|-------------|
| k6.test.js | 50 | 2m | ~4000 | <1% | ~3ms |
| k6-auth-load.js | 100 | 30s | 200 | 0% | ~22s |
| k6-signalr-load.js | 20 | 60s | varies | varies | <30ms |
| artillery.test.yml | 10-150 | 3m | ~8500 | 0% | <5ms |
