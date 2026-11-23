# Gwent.Core DSL — versión inicial

La gramática inicial valida el flujo fuente → análisis léxico → análisis sintáctico → análisis semántico → catálogo compilado → motor.

## Gramática

Una declaración por línea:

```text
effect <identifier> boost <non-negative-integer> [duration <instant|while-source-on-field or until-round-end>] [operation <add|set|multiply>] [target <source|own-row|both-players-in-row>]
card <identifier> "<display name>" power <non-negative-integer> row <melee|ranged|siege> [effect <effect-identifier>]
```

- Las líneas vacías y los comentarios que comienzan con `#` se ignoran.
- Los identificadores comienzan con letra o guion bajo y continúan con letras, dígitos o guion bajo.
- Los nombres entre comillas admiten escapes para comillas, barra inversa, salto de línea y tabulación.
- Las cartas pueden referenciar efectos declarados antes o después.
- Los números deben caber en un entero con signo de 32 bits; los valores fuera del rango producen diagnósticos.
- La compilación es atómica: ante cualquier diagnóstico no se publica ningún catálogo parcial.
- Un efecto `boost N` es instantáneo, suma N y afecta a la carta fuente de forma predeterminada.
- `duration while-source-on-field` mantiene el modificador activo mientras la carta fuente permanezca en el campo.
- `operation` define si N se suma (`add`), reemplaza la potencia (`set`) o la multiplica (`multiply`).
- `target source` afecta solo a la fuente; `own-row` afecta a las demás cartas de su fila y propietario; `both-players-in-row` afecta a las demás cartas de esa fila en ambos campos.

## Arquitectura del compilador

1. **Lexer:** produce tokens con línea y columna, reconoce comentarios, identificadores, enteros, cadenas y finales de línea.
2. **Parser:** convierte tokens en declaraciones tipadas del AST y recupera errores al final de cada línea.
3. **AST:** conserva declaraciones de efectos y cartas sin ejecutar reglas del motor.
4. **Análisis semántico:** valida enteros, identificadores duplicados, filas y referencias a efectos; permite referencias adelantadas.
5. **Catálogo compilado:** publica definiciones y efectos solo si no hay diagnósticos.
6. **Integración runtime:** `CompilationResult.ApplyCardEffect` valida la definición y delega la mutación a `Game`.

La estructura queda preparada para incorporar expresiones, selectores, predicados, parámetros y acciones sin acoplarlos al lexer ni a la API del motor. Esas construcciones no forman todavía parte de la gramática implementada.

## Integración con el motor

- `CardDefinition.CreateCard()` crea una instancia física nueva; las copias comparten definición, no identidad.
- `CompilationResult.ApplyCardEffect(game, player, card)` aplica el efecto asociado.
- El método devuelve `false` cuando la carta no declara efecto y `true` cuando lo aplica. Las restricciones de turno, zona y duplicación siguen siendo responsabilidad del motor.

## Ejecución

```sh
dotnet run --project src/Gwent.Console -- examples/starter.gwent
```
