# Gwent.Core DSL — versión inicial

La primera versión del lenguaje es deliberadamente pequeña y explícita. Su objetivo es validar el flujo completo fuente → compilación → catálogo → partida, antes de ampliar la gramática heredada.

## Gramática

Una declaración por línea:

```text
effect <identifier> boost <non-negative-integer>
card <identifier> "<display name>" power <non-negative-integer> row <melee|ranged|siege> [effect <effect-identifier>]
```

- Las líneas vacías y los comentarios que comienzan con `#` se ignoran.
- Los identificadores comienzan con letra o guion bajo.
- Las cartas pueden referenciar efectos declarados en el mismo archivo.
- Los números deben caber en un entero con signo de 32 bits; los valores fuera del rango producen diagnósticos.
- La compilación es atómica: ante cualquier diagnóstico no se publica ningún catálogo parcial.
- Un efecto `boost N` describe un aumento de N en la potencia de la carta que lo referencia. El compilador conserva esa descripción; la aplicación coordina su ejecución mediante la operación del motor.

## Ejecución

```sh
dotnet run --project src/Gwent.Console -- examples/starter.gwent
```

Esta gramática es una primera rebanada vertical, no una afirmación de compatibilidad con toda la sintaxis del compilador original. La expansión de selectores, predicados, scopes y operaciones de listas requiere etapas separadas de análisis semántico y ejecución.
