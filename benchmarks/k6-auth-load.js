import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

const registerDuration = new Trend('register_duration');
const errorRate = new Rate('errors');
const errors4xx = new Counter('errors_4xx');
const errors5xx = new Counter('errors_5xx');

export const options = {
  scenarios: {
    auth_load: {
      executor: 'shared-iterations',
      vus: 100,
      iterations: 100,
      maxDuration: '60s',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<30000'], // 30 segundos realista para 100 usuarios paralelos
    errors: ['rate<0.05'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export default function () {
  const uniqueId = `${Date.now()}_${__VU}_${__ITER}`;
  
  const registerPayload = JSON.stringify({
    username: `loadtest_${uniqueId}`,
    email: `loadtest_${uniqueId}@test.com`,
    password: 'Test123!',
  });

  const registerStart = Date.now();
  const registerRes = http.post(`${BASE_URL}/api/auth/register`, registerPayload, {
    headers: { 'Content-Type': 'application/json' },
  });
  registerDuration.add(Date.now() - registerStart);

  const registerSuccess = check(registerRes, {
    'register status 200': (r) => r.status === 200,
  });

  if (!registerSuccess) {
    errorRate.add(1);
    if (registerRes.status >= 400 && registerRes.status < 500) {
      errors4xx.add(1);
    } else if (registerRes.status >= 500) {
      errors5xx.add(1);
    }
  }

  sleep(0.1);

  const loginPayload = JSON.stringify({
    username: `loadtest_${uniqueId}`,
    password: 'Test123!',
  });

  const loginRes = http.post(`${BASE_URL}/api/auth/login`, loginPayload, {
    headers: { 'Content-Type': 'application/json' },
  });

  const loginSuccess = check(loginRes, {
    'login status 200': (r) => r.status === 200,
    'login has token': (r) => {
      try {
        return JSON.parse(r.body).token !== undefined;
      } catch {
        return false;
      }
    },
  });

  if (!loginSuccess) {
    errorRate.add(1);
    if (loginRes.status >= 400 && loginRes.status < 500) {
      errors4xx.add(1);
    } else if (loginRes.status >= 500) {
      errors5xx.add(1);
    }
  }
}

export function handleSummary(data) {
  return {
    stdout: JSON.stringify({
      total_requests: data.metrics.http_reqs.values.count,
      avg_duration: data.metrics.http_req_duration.values.avg,
      p95_duration: data.metrics.http_req_duration.values['p(95)'],
      errors_4xx: data.metrics.errors_4xx ? data.metrics.errors_4xx.values.count : 0,
      errors_5xx: data.metrics.errors_5xx ? data.metrics.errors_5xx.values.count : 0,
      error_rate: data.metrics.errors ? data.metrics.errors.values.rate : 0,
    }, null, 2),
  };
}
