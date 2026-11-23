# Estado de migración

## Recuperación de fuentes

Se inspeccionó el árbol del repositorio histórico y se identificaron 117 archivos relevantes entre C#, configuración Unity y documentación. El proyecto original usa Unity 2022.3.22f1. La lógica de dominio y el compilador están mezclados con referencias a Unity y estado estático; no se copian directamente.

## Implementado

- Solución .NET 10 con Engine, Compiler, Console y tres proyectos de pruebas.
- Definiciones reutilizables y copias físicas de cartas con identidades independientes.
- Zonas de jugador: mazo, mano, campo y cementerio.
- Operaciones del motor para añadir al mazo, robar, jugar y destruir.
- Validación de pertenencia a la partida y de zona de origen antes de mover cartas.
- Vistas de zonas no modificables desde el exterior.
- Puntuación derivada de las cartas que permanecen en el campo.
- Compilador DSL inicial para cartas y efectos de aumento de potencia.
- Consola que carga definiciones, crea un mazo, roba y juega las cartas, y muestra el resultado.
- Pruebas unitarias y de integración para el ciclo de vida básico.

## Limitaciones pendientes

- No se han implementado todavía límites de mano/mazo ni reglas completas de construcción de mazos.
- No hay filas como zonas independientes, turnos, rondas, mulligan, condiciones de victoria completas ni reglas de empate.
- La DSL v1 no implementa selectores, predicados, scopes, loops, post-actions ni operaciones de colecciones.
- Los efectos aún se coordinan desde la consola; falta el runtime de efectos con contexto explícito.
- La consola demuestra un flujo básico, no una partida completa con decisiones del usuario.
- No se ha ejecutado build local en este entorno; GitHub Actions valida los cambios.
