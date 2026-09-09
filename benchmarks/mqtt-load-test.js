const mqtt = require('mqtt');

const MQTT_HOST = process.env.MQTT_HOST || 'mqtt://localhost:1883';
const NUM_CLIENTS = parseInt(process.env.NUM_CLIENTS || '50');
const TEST_DURATION_MS = parseInt(process.env.TEST_DURATION || '60000');

const topics = [
  'game/+/powerup/spawned',
  'game/+/powerup/collected', 
  'game/+/collision',
  'game/+/gameover',
];

const metrics = {
  connected: 0,
  disconnected: 0,
  messagesReceived: 0,
  errors: 0,
  latencies: [],
};

const clients = [];

async function createClient(id) {
  return new Promise((resolve, reject) => {
    const clientId = `loadtest_${id}_${Date.now()}`;
    const client = mqtt.connect(MQTT_HOST, {
      clientId,
      clean: true,
      connectTimeout: 10000,
    });

    client.on('connect', () => {
      metrics.connected++;
      
      topics.forEach(topic => {
        client.subscribe(topic, { qos: 1 }, (err) => {
          if (err) {
            metrics.errors++;
          }
        });
      });
      
      resolve(client);
    });

    client.on('message', (topic, message) => {
      metrics.messagesReceived++;
      
      try {
        const payload = JSON.parse(message.toString());
        if (payload.sent_at) {
          const latency = Date.now() - payload.sent_at;
          metrics.latencies.push(latency);
        }
      } catch {
        // Ignore parse errors
      }
    });

    client.on('error', (err) => {
      metrics.errors++;
    });

    client.on('close', () => {
      metrics.disconnected++;
    });

    setTimeout(() => {
      if (!client.connected) {
        reject(new Error(`Client ${id} failed to connect`));
      }
    }, 10000);
  });
}

function calculatePercentile(arr, p) {
  if (arr.length === 0) return 0;
  const sorted = arr.slice().sort((a, b) => a - b);
  const index = Math.ceil((p / 100) * sorted.length) - 1;
  return sorted[Math.max(0, index)];
}

async function runTest() {
  console.log(`Starting MQTT load test with ${NUM_CLIENTS} clients...`);
  console.log(`Test duration: ${TEST_DURATION_MS / 1000} seconds`);
  console.log(`MQTT broker: ${MQTT_HOST}`);
  console.log('');

  const startTime = Date.now();

  // Create all clients
  for (let i = 0; i < NUM_CLIENTS; i++) {
    try {
      const client = await createClient(i);
      clients.push(client);
      
      if ((i + 1) % 10 === 0) {
        console.log(`Created ${i + 1}/${NUM_CLIENTS} clients...`);
      }
    } catch (err) {
      console.error(`Failed to create client ${i}: ${err.message}`);
    }
  }

  console.log(`\nAll clients created. Waiting for ${TEST_DURATION_MS / 1000} seconds...`);

  // Wait for test duration
  await new Promise(resolve => setTimeout(resolve, TEST_DURATION_MS));

  // Disconnect all clients
  console.log('\nDisconnecting clients...');
  for (const client of clients) {
    client.end(true);
  }

  // Calculate metrics
  const duration = (Date.now() - startTime) / 1000;
  const avgLatency = metrics.latencies.length > 0 
    ? metrics.latencies.reduce((a, b) => a + b, 0) / metrics.latencies.length 
    : 0;
  const p95Latency = calculatePercentile(metrics.latencies, 95);
  const p99Latency = calculatePercentile(metrics.latencies, 99);

  console.log('\n========================================');
  console.log('MQTT Load Test Results');
  console.log('========================================');
  console.log(`Duration: ${duration.toFixed(2)} seconds`);
  console.log(`Clients connected: ${metrics.connected}/${NUM_CLIENTS}`);
  console.log(`Messages received: ${metrics.messagesReceived}`);
  console.log(`Errors: ${metrics.errors}`);
  console.log(`Message loss: ${metrics.messagesReceived === 0 && metrics.errors === 0 ? '0 (no messages published during test)' : 'N/A'}`);
  console.log('');
  console.log('Latency (if messages with timestamps received):');
  console.log(`  Average: ${avgLatency.toFixed(2)} ms`);
  console.log(`  p95: ${p95Latency.toFixed(2)} ms`);
  console.log(`  p99: ${p99Latency.toFixed(2)} ms`);
  console.log('========================================');

  // Output JSON for InfluxDB integration
  const result = {
    test: 'mqtt_load',
    clients: NUM_CLIENTS,
    duration_seconds: duration,
    connected: metrics.connected,
    messages_received: metrics.messagesReceived,
    errors: metrics.errors,
    latency_avg: avgLatency,
    latency_p95: p95Latency,
    latency_p99: p99Latency,
  };

  console.log('\nJSON Output:');
  console.log(JSON.stringify(result, null, 2));

  process.exit(0);
}

runTest().catch(err => {
  console.error('Test failed:', err);
  process.exit(1);
});
