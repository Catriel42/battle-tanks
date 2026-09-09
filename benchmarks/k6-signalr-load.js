import ws from 'k6/ws';
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

const wsConnectDuration = new Trend('ws_connect_duration');
const wsMessageLatency = new Trend('ws_message_latency');
const wsErrors = new Counter('ws_errors');
const messagesSent = new Counter('messages_sent');
const messagesReceived = new Counter('messages_received');
const errorRate = new Rate('errors');

export const options = {
  scenarios: {
    signalr_load: {
      executor: 'constant-vus',
      vus: 20,
      duration: '60s',
    },
  },
  thresholds: {
    ws_connect_duration: ['p(95)<2000'],
    errors: ['rate<0.1'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const WS_URL = __ENV.WS_URL || 'ws://localhost:5000';

function registerAndLogin() {
  const uniqueId = `${Date.now()}_${__VU}_${Math.random()}`;

  const registerPayload = JSON.stringify({
    username: `signalr_${uniqueId}`,
    email: `signalr_${uniqueId}@test.com`,
    password: 'Test123!',
  });

  http.post(`${BASE_URL}/api/auth/register`, registerPayload, {
    headers: { 'Content-Type': 'application/json' },
  });

  const loginPayload = JSON.stringify({
    username: `signalr_${uniqueId}`,
    password: 'Test123!',
  });

  const loginRes = http.post(`${BASE_URL}/api/auth/login`, loginPayload, {
    headers: { 'Content-Type': 'application/json' },
  });

  if (loginRes.status === 200) {
    try {
      return JSON.parse(loginRes.body).token;
    } catch {
      return null;
    }
  }
  return null;
}

export default function () {
  const token = registerAndLogin();
  if (!token) {
    errorRate.add(1);
    wsErrors.add(1);
    return;
  }

  // Direct WebSocket connection to SignalR hub
  const wsUrl = `${WS_URL}/gamehub?access_token=${token}`;

  const connectStart = Date.now();

  const res = ws.connect(wsUrl, {}, function (socket) {
    wsConnectDuration.add(Date.now() - connectStart);

    let messageCount = 0;
    const maxMessages = 10;

    socket.on('open', function () {
      // Send SignalR handshake
      socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + '\x1e');
    });

    socket.on('message', function (data) {
      const messages = data.split('\x1e').filter((m) => m.length > 0);

      for (const msg of messages) {
        if (!msg) continue;

        try {
          const parsed = JSON.parse(msg);
          messagesReceived.add(1);

          // If we receive completion message, start sending inputs
          if (parsed.type === 3 || messageCount === 0) {
            sendMovementInputs(socket);
          }
        } catch (e) {
          // Ignore parse errors for partial messages
        }
      }
    });

    socket.on('error', function (e) {
      wsErrors.add(1);
      errorRate.add(1);
    });

    function sendMovementInputs(socket) {
      if (messageCount >= maxMessages) {
        socket.close();
        return;
      }

      const directions = ['up', 'down', 'left', 'right'];
      const direction = directions[Math.floor(Math.random() * directions.length)];

      const moveStartMsg =
        JSON.stringify({
          type: 1,
          target: 'SendInput',
          arguments: [
            {
              type: 'move_start',
              direction: direction,
              sequenceNumber: messageCount,
            },
          ],
        }) + '\x1e';

      try {
        socket.send(moveStartMsg);
        messagesSent.add(1);
      } catch (e) {
        wsErrors.add(1);
      }

      sleep(0.5);

      const moveStopMsg =
        JSON.stringify({
          type: 1,
          target: 'SendInput',
          arguments: [
            {
              type: 'move_stop',
              sequenceNumber: messageCount + 1,
            },
          ],
        }) + '\x1e';

      try {
        socket.send(moveStopMsg);
        messagesSent.add(1);
      } catch (e) {
        wsErrors.add(1);
      }

      messageCount++;

      if (messageCount < maxMessages) {
        sleep(1);
        sendMovementInputs(socket);
      } else {
        socket.close();
      }
    }

    socket.setTimeout(function () {
      socket.close();
    }, 15000);
  });

  check(res, {
    'WebSocket connected': (r) => r && r.status === 101,
  }) || errorRate.add(1);
}

export function handleSummary(data) {
  return {
    stdout: JSON.stringify(
      {
        vus: 20,
        duration: '60s',
        ws_connect_p95: data.metrics.ws_connect_duration
          ? data.metrics.ws_connect_duration.values['p(95)']
          : 0,
        ws_latency_avg: data.metrics.ws_message_latency
          ? data.metrics.ws_message_latency.values.avg
          : 0,
        ws_latency_p95: data.metrics.ws_message_latency
          ? data.metrics.ws_message_latency.values['p(95)']
          : 0,
        messages_sent: data.metrics.messages_sent
          ? data.metrics.messages_sent.values.count
          : 0,
        messages_received: data.metrics.messages_received
          ? data.metrics.messages_received.values.count
          : 0,
        errors: data.metrics.ws_errors ? data.metrics.ws_errors.values.count : 0,
        error_rate: data.metrics.errors ? data.metrics.errors.values.rate : 0,
      },
      null,
      2
    ),
  };
}
