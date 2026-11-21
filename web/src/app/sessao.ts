// Guardas de sessão (#120, especificacao-fluxo §4): o painel sem sessão leva a /entrar, e
// /entrar com sessão leva à agenda.
import { inject } from "@angular/core";
import { type CanActivateFn, Router } from "@angular/router";
import { catchError, map, of } from "rxjs";
import { Api, ApiError } from "./api";

/**
 * GET /api/me decide: 401 → /entrar. Qualquer outro erro (sem conexão, erro interno) deixa
 * passar, para a tela do painel mostrar o próprio estado de erro em vez de uma tela em branco.
 */
export const sessaoGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(Api)
    .eu()
    .pipe(
      map(() => true),
      catchError((e: unknown) => of(e instanceof ApiError && e.status === 401 ? router.parseUrl("/entrar") : true)),
    );
};

/**
 * /entrar é a primeira página do app. Com sessão (GET /api/me 200), não há o que entrar: vai
 * para a agenda. Qualquer erro (401, sem conexão, /api/me ainda inexistente) mostra o login.
 */
export const semSessaoGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(Api)
    .eu()
    .pipe(
      map(() => router.parseUrl("/agendamentos")),
      catchError(() => of(true)),
    );
};
