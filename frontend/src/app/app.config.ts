import { provideHttpClient } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withRouterConfig } from '@angular/router';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(),
    provideRouter(
      routes,
      withRouterConfig({
        // Ugyanarra a route-ra visszatérve is újratöltjük a komponenst.
        onSameUrlNavigation: 'reload',
      }),
    ),
  ],
};
