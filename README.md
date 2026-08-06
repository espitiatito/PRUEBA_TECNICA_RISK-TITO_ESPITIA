# Bandeja de cobros fallidos

Herramienta interna para que Operaciones revise los cobros que fallaron en el proceso nocturno
y los reintente a mano, sin esperar a la corrida de la noche siguiente.

## Cómo se levanta

```bash
cd api/Facturacion.Api && dotnet run     # http://localhost:5080
cd web && npm ci && npm start            # http://localhost:4200
```

La base es un SQLite local: se crea y se siembra sola en el primer arranque, no hay que correr
migraciones ni nada aparte. La pasarela de pagos es una simulación con latencia y fallos, para no
depender del sandbox del proveedor.

## Pendientes

- faltan las validaciones del reintento, hoy llama a la pasarela y suma el intento
- la paginación del front hay que revisarla
- el filtro por estado a veces no trae lo que uno espera
- unificar cómo responde la API cuando algo sale mal
- los tests solo cubren el mapeo de DTOs

Quedé a medias con el reintento manual, cualquier cosa me escriben.
