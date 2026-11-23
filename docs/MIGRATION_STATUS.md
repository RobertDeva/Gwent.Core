# Estado de migración

## Recuperación de fuentes

Se inspeccionó el árbol del repositorio histórico y se identificaron 117 archivos relevantes entre C#, configuración Unity y documentación. El proyecto original usa Unity 2022.3.22f1. La lógica de dominio y el compilador están mezclados con referencias a Unity y estado estático; no se copian directamente.

## Primera rebanada implementada

- Solución .NET 10 con Engine, Compiler, Console y tres proyectos de pruebas.
- Motor mínimo con instancias aisladas de partida, jugadores, cartas, movimiento al campo y puntuación.
- Compilador DSL inicial para declaraciones de cartas y efectos de aumento de potencia.
- Ejecución del efecto compilado sobre una carta que ya pertenece a una partida.
- Consola capaz de cargar un archivo DSL y mostrar puntuaciones y ganador.
- Pruebas unitarias y de integración para los contratos principales.

## Limitaciones pendientes

- La DSL v1 no implementa aún selectores, predicados, scopes, loops, post-actions ni operaciones de colecciones.
- No hay mazos, mano, cementerio, rondas ni reglas completas de Gwent en esta primera rebanada.
- No se ha ejecutado build ni tests en este entorno porque no dispone del SDK de .NET. GitHub Actions debe validar el primer build remoto.
