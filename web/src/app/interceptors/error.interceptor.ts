import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export interface AppError {
  status: number;
  isBusinessRule: boolean;
  errorCode?: string;
  message: string;
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      let message = 'Ocurrió un error inesperado en el servidor.';
      let isBusinessRule = false;
      let errorCode: string | undefined;

      if (error.error && typeof error.error === 'object') {
        const problem = error.error as Record<string, unknown>;
        if (typeof problem['detail'] === 'string') {
          message = problem['detail'];
        } else if (typeof problem['message'] === 'string') {
          message = problem['message'];
        }

        if (typeof problem['errorCode'] === 'string') {
          errorCode = problem['errorCode'];
        }
      }

      // 422 (Unprocessable) o 409 (Conflict) representan reglas de negocio o concurrencia
      if (error.status === 422 || error.status === 409) {
        isBusinessRule = true;
      } else if (error.status === 404) {
        message = 'El cobro solicitado no fue encontrado.';
      } else if (error.status === 0) {
        message = 'No se pudo establecer conexión con el backend (API no disponible).';
      }

      const formattedError: AppError = {
        status: error.status,
        isBusinessRule,
        errorCode,
        message,
      };

      return throwError(() => formattedError);
    })
  );
};
