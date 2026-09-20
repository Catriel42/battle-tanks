export const environment = {
  production: true,
  apiUrl: '/api',
  hubUrl: '/gamehub',
  mqtt: {
    hostname: 'djxvqc20jgip4.cloudfront.net',
    port: 443,
    path: '/mqtt',
    protocol: 'wss' as const
  }
};
