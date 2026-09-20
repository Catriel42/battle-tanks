export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api',
  hubUrl: 'http://localhost:5000/gamehub',
  mqtt: {
    hostname: 'localhost',
    port: 8083,
    path: '/mqtt',
    protocol: 'ws' as const
  }
};
