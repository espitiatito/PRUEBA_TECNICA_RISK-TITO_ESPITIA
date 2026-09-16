# Bandeja de cobros fallidos

Herramienta interna para que Operaciones revise los cobros que fallaron en el proceso nocturno y los reintente a mano, sin esperar a la corrida de la noche siguiente.

## Cómo se levanta

No cambié nada del arranque original, se levanta tal cual venía preparado:

```bash
# API (.NET 9) — http://localhost:5080
cd api/Facturacion.Api && dotnet run

# Front (Angular 21) — http://localhost:4200
cd web && npm ci && npm start
```

La base es un SQLite local que se crea y se siembra sola en el primer arranque (~120 cobros). La pasarela está simulada con latencia y fallos para probar los casos difíciles sin depender de terceros.

---

## Decisiones técnicas que tomé

Para solucionar los problemas que encontré y dejar la herramienta lista, tomé estas decisiones:

* **Control de doble cobro (Idempotencia):** Era el punto más delicado. Para asegurarme de que no se cobre dos veces a un cliente, apliqué dos bloqueos:
  1. Un candado rápido en memoria (`SemaphoreSlim`) que rechaza de inmediato si una persona hace doble clic en el mismo milisegundo.
  2. Marco el cobro como "En proceso" en la base de datos antes de hablar con la pasarela, para que otra pestaña u otro usuario tampoco lo pueda enviar.
* **Lógica y validaciones de negocio separadas:** Creé `ChargeRetryService` para evaluar las 5 reglas (tarjetas vencidas, cobros ya pagados, tope de 3 intentos, etc.) antes de tocar la pasarela. Además, validé en la entrada del API que el monto no sea menor o igual a cero con una respuesta `400 Bad Request` útil.
* **Base de datos sana:** Cambié el `DbContext` a ciclo de vida `Scoped` para que cada petición tenga su conexión y no se pisen las consultas.
* **Frontend práctico y sin desorden:** Usé Signals para los 4 estados de la vista (cargando, vacío, error y con datos), cancelé las peticiones en vuelo previas para que respuestas lentas de red no sobreescriban la pantalla en clics rápidos, y dejé el diseño en HTML estándar con el CSS mínimo indispensable.

---

## Qué dejé sin hacer y qué haría con 3 horas más

* **Reintento en lote (RF-5):** Lo dejé fuera a propósito para enfocarme en que el reintento individual fuera 100% seguro y no duplicara cobros. Con 3 horas más, implementaría este reintento masivo usando una cola en segundo plano (`Channel` o servicio en background) para procesar varios cobros sin congelar la pantalla.
* **Proceso de reconciliación:** Crearía un worker automático que revise si algún cobro quedó atascado en "En proceso" (por ejemplo, si el servidor se apaga justo cuando la pasarela cobró pero antes de guardar en SQLite) y verifique el estado real en la pasarela.
* **Más pruebas de integración:** Aunque dejé 13 pruebas unitarias cubriendo las reglas del reintento, montos inválidos y casos de timeout, agregaría pruebas integrales levantando la API con `WebApplicationFactory`.
