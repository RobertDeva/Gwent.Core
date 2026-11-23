# Gwent.Core DSL — versión inicial

La primera versión del lenguaje es deliberadamente pequeña y explícita. Su objetivo es validar el flujo completo fuente → compilación → catálogo → partida, antes de ampliar la gramática heredada.

## Gramática

Una declaración por línea:

```text
effect <identifier> boost <non-negative-integer> [duration <instant|while-source-on-field>] [operation <add|set|multiply>] [target <source|own-row|both-players-in-row>]
card <identifier> "<display name>" power <non-negative-integer> row <melee|ranged|siege> [effect <effect-identifier>]
```

- Las líneas vacías y los comentarios que comienzan con `#` se ignoran.
- Los identificadores comienzan con letra o guion bajo.
- Las cartas pueden referenciar efectos declarados en el mismo archivo.
- Los números deben caber en un entero con signo de 32 bits; los valores fuera del rango producen diagnósticos.
- La compilación es atómica: ante cualquier diagnóstico no se publica ningún catálogo parcial.
- Un efecto `boost N` conserva compatibilidad con el comportamiento inicial: es instantáneo, suma N y afecta a la carta fuente.
- `duration while-source-on-field` mantiene el modificador activo mientras la carta fuente permanezca en el campo; `instant` es el valor predeterminado.
- `operation` define si N se suma (`add`, predeterminado), reemplaza la potencia (`set`) o la multiplica (`multiply`).
- `target source` afecta solo a la carta fuente; `own-row` afecta a las demás cartas de su fila y propietario; `both-players-in-row` afecta a las demás cartas de esa fila en ambos campos.
- Los efectos persistentes se recalculan al cambiar el campo y se retiran cuando sale su fuente o termina la ronda. El sistema de filtrado por tipo/rango de carta todavía no está implementado.

## Integración con el motor

El resultado de compilación es el catálogo autorizado para crear instancias y aplicar sus efectos:

- `CardDefinition.CreateCard()` crea una instancia física nueva; las copias comparten definición, no identidad.
- `CompilationResult.ApplyCardEffect(game, player, card)` comprueba que la instancia coincide con una definición del catálogo y aplica el efecto asociado a través de la API del motor.
- El método devuelve `false` cuando la carta no declara efecto y `true` cuando lo aplica. Las restricciones de turno, zona y duplicación siguen siendo responsabilidad del motor.

## Ejecución

```sh
dotnet run --project src/Gwent.Console -- examples/starter.gwent
```

Esta gramática es una primera rebanada vertical, no una afirmación de compatibilidad con toda la sintaxis del compilador original. La expansión de selectores, predicados, scopes y operaciones de listas requiere etapas separadas de análisis semántico y ejecución.
