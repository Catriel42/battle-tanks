import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

const joinDuration = new Trend('join_duration');
const errorRate = new Rate('errors');
const roomsJoined = new Counter('rooms_joined');
const roomsLeft = new Counter('rooms_left');

export const options = {
  scenarios: {
    room_join_load: {
      executor: 'constant-vus',
      vus: 20,
      duration: '60s',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<5000'],
    errors: ['rate<0.1'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

function registerAndLogin() {
  const uniqueId = `${Date.now()}_${__VU}_${Math.random()}`;

  const registerPayload = JSON.stringify({
    username: `roomtest_${uniqueId}`,
    email: `roomtest_${uniqueId}@test.com`,
    password: 'Test123!',
  });

  http.post(`${BASE_URL}/api/auth/register`, registerPayload, {
    headers: { 'Content-Type': 'application/json' },
  });

  const loginPayload = JSON.stringify({
    username: `roomtest_${uniqueId}`,
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
    return;
  }

  const headers = {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`,
  };

  // Get available rooms
  const roomsRes = http.get(`${BASE_URL}/api/room`, { headers });

  let rooms = [];
  if (roomsRes.status === 200) {
    try {
      rooms = JSON.parse(roomsRes.body);
    } catch {
      errorRate.add(1);
    }
  }

  if (rooms.length === 0) {
    // Create a room if none exist
    const createRoomPayload = JSON.stringify({
      name: `Room_${__VU}_${Date.now()}`,
      mapId: '00000000-0000-0000-0000-000000000001', // Default map
      maxPlayers: 4,
    });

    const createRes = http.post(`${BASE_URL}/api/room`, createRoomPayload, {
      headers,
    });

    if (createRes.status === 201) {
      try {
        const newRoom = JSON.parse(createRes.body);
        rooms.push(newRoom);
      } catch {
        errorRate.add(1);
      }
    }
  }

  // Join a room
  if (rooms.length > 0) {
    const roomToJoin = rooms[0];

    const joinStart = Date.now();
    const joinRes = http.put(
      `${BASE_URL}/api/room/${roomToJoin.id}/join`,
      null,
      { headers }
    );
    joinDuration.add(Date.now() - joinStart);

    const joinSuccess = check(joinRes, {
      'join room status 200': (r) => r.status === 200,
    });

    if (joinSuccess) {
      roomsJoined.add(1);
      sleep(5); // Stay in room for 5 seconds
    } else {
      errorRate.add(1);
    }

    // Leave room
    const leaveRes = http.post(`${BASE_URL}/api/room/leave`, null, { headers });
    if (leaveRes.status === 200) {
      roomsLeft.add(1);
    }
  }

  sleep(1);
}

export function handleSummary(data) {
  return {
    stdout: JSON.stringify(
      {
        vus: 20,
        duration: '60s',
        rooms_joined: data.metrics.rooms_joined
          ? data.metrics.rooms_joined.values.count
          : 0,
        rooms_left: data.metrics.rooms_left
          ? data.metrics.rooms_left.values.count
          : 0,
        join_duration_avg: data.metrics.join_duration
          ? data.metrics.join_duration.values.avg
          : 0,
        join_duration_p95: data.metrics.join_duration
          ? data.metrics.join_duration.values['p(95)']
          : 0,
        error_rate: data.metrics.errors ? data.metrics.errors.values.rate : 0,
      },
      null,
      2
    ),
  };
}
