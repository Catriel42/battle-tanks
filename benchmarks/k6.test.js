import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const errorRate = new Rate('errors');
const roomsDuration = new Trend('rooms_duration');
const mapsDuration = new Trend('maps_duration');

export const options = {
  stages: [
    { duration: '30s', target: 10 },
    { duration: '1m', target: 50 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],
    errors: ['rate<0.1'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export function setup() {
  const uniqueId = Date.now();

  const registerPayload = JSON.stringify({
    username: `testuser_${uniqueId}`,
    email: `test_${uniqueId}@test.com`,
    password: 'Test123!',
  });

  http.post(`${BASE_URL}/api/auth/register`, registerPayload, {
    headers: { 'Content-Type': 'application/json' },
  });

  const loginPayload = JSON.stringify({
    username: `testuser_${uniqueId}`,
    password: 'Test123!',
  });

  const loginRes = http.post(`${BASE_URL}/api/auth/login`, loginPayload, {
    headers: { 'Content-Type': 'application/json' },
  });

  if (loginRes.status === 200) {
    const body = JSON.parse(loginRes.body);
    return { token: body.token };
  }

  return { token: null };
}

export default function (data) {
  const headers = {
    'Content-Type': 'application/json',
    Authorization: data.token ? `Bearer ${data.token}` : '',
  };

  const roomsRes = http.get(`${BASE_URL}/api/room`, { headers });
  roomsDuration.add(roomsRes.timings.duration);
  check(roomsRes, {
    'GET /api/room status 200': (r) => r.status === 200,
  }) || errorRate.add(1);

  sleep(0.5);

  const mapsRes = http.get(`${BASE_URL}/api/maps`, { headers });
  mapsDuration.add(mapsRes.timings.duration);
  check(mapsRes, {
    'GET /api/maps status 200': (r) => r.status === 200,
  }) || errorRate.add(1);

  sleep(0.5);

  const leaderboardRes = http.get(`${BASE_URL}/api/leaderboard`, { headers });
  check(leaderboardRes, {
    'GET /api/leaderboard status 200': (r) => r.status === 200,
  }) || errorRate.add(1);

  sleep(1);
}

export function teardown(data) {
  console.log('Test completed');
}
