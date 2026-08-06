import { HttpInterceptorFn } from '@angular/common/http';

// TODO: centralizar aqui lo que hoy resolvemos en cada componente
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req);
};
