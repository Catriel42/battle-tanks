import { ApplicationConfig, provideBrowserGlobalErrorListeners, importProvidersFrom } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from './interceptors/auth.interceptor';
import { routes } from './app.routes';
import { MqttModule } from 'ngx-mqtt';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    importProvidersFrom(
      MqttModule.forRoot({
        hostname: environment.mqtt.hostname,
        port: environment.mqtt.port,
        path: environment.mqtt.path,
        protocol: environment.mqtt.protocol
      } as any)
    )
  ]
};
