import { ApplicationConfig, provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient, withFetch } from '@angular/common/http';

export const appConfig: ApplicationConfig = {
  providers: [
    // Zoneless: the triage screen updates from signals only, so zone.js earns nothing here.
    provideZonelessChangeDetection(),
    provideHttpClient(withFetch())
  ]
};
