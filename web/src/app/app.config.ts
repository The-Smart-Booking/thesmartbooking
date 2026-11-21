import { provideHttpClient, withFetch, withInterceptors } from "@angular/common/http";
import { type ApplicationConfig, provideBrowserGlobalErrorListeners } from "@angular/core";
import { provideRouter, TitleStrategy } from "@angular/router";
import { provideLucideConfig } from "@lucide/angular";
import { errosApiInterceptor } from "./api/http";
import { routes, TituloDayspot } from "./app.routes";

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    { provide: TitleStrategy, useClass: TituloDayspot },
    provideHttpClient(withFetch(), withInterceptors([errosApiInterceptor])),
    // Ícones Lucide com o traço da v3 (Design.html › Ícones); o tamanho vem da classe .ds-ic.
    provideLucideConfig({ strokeWidth: 1.75 }),
  ],
};
