# Legacy migration inventory

This inventory records the disposition of the previous Unity implementation. It is a migration guide, not an assertion that legacy code has already been ported or runtime-verified.

## GwentLogicEngine

| Legacy area | Decision | Reason / destination |
|---|---|---|
| `Board` | Rewrite | Mutable static zones leak state between matches; board must own all zones per match. Complete missing siege/weather wiring and make cleanup mutation-safe. |
| `FieldZone`, `UnitZone`, `WeatherZone`, `UpgradeZone` | Adapt | Preserve the domain concepts; initialize collections and enforce capacities and relationships in constructors. |
| `Player` | Rewrite | Fix draw boundary behavior, scoring accumulation, initialization, and ownership. Keep player state independent of Unity. |
| `Card`, `UnitCard`, special cards | Adapt/rewrite | Preserve meaningful card concepts; remove nullable-power hazards and empty overrides; model behavior through composition where appropriate. |
| `Deck` | Adapt | Validate size, faction, duplicates and card ownership; avoid exposing mutable internal collections. |
| `ICard` | Redesign | Replace default-interface behavior that assumes a specific lane with explicit, validated engine operations. |
| `Effects` | Rewrite | Remove global registries and uninitialized player references; use explicit effect context and deterministic operations. |
| `EffectsDictionary` | Replace | Use an instance-scoped registry with duplicate detection and defined lifecycle. |
| `MetodosUtiles` | Replace | Move shuffle and zone-transfer behavior into focused domain services or collection operations; no Unity base class. |

## GwentCompiler

| Legacy area | Decision | Reason / destination |
|---|---|---|
| Lexer / tokens / token stream | Adapt | Preserve useful token definitions and language intent; fix diagnostics, source positions, and token-stream recovery. |
| Parser | Rewrite incrementally | Keep the established grammar where useful; make parsing deterministic, initialize all AST collections, and recover from malformed input safely. |
| AST / expressions | Adapt | Preserve useful syntax concepts, but define typed evaluation contracts and eliminate nullable assumptions and side-effect ambiguity. |
| Context / Scope | Redesign | Use compilation/evaluation instances rather than global mutable state; reject unknown identifiers and model lexical scope explicitly. |
| Semantic analysis | Complete | Validate declarations, types, references, selectors, parameters, and effect operations before execution. |
| Effect execution | Rewrite | Bind compiled effects to engine APIs via explicit context; remove Unity lookups, static mutable variables, and incomplete operations. |
| `Dictionarys` / compiled registries | Replace | Return a compilation result containing an instance-scoped catalog; do not mutate engine globals during parsing. |

## Unity integration

Unity `MonoBehaviour` scripts, prefabs, scene objects, UI, `GameObject.Find`, `Instantiate`, and TextMeshPro references are not part of this repository's core implementation. A future Unity front end may consume the engine through a separate adapter project.

## Known defects to prevent from returning

- Board zones and compiler execution data stored in mutable static fields.
- Collection mutation during `foreach` iteration.
- `GetTotalPower` ignoring returned accumulator values.
- Hand-size equality edge case during draw.
- Zone transfer without membership, ownership, or capacity checks.
- Missing zone initialization and incomplete player-to-board wiring.
- Nullable card power dereferenced without a defined value.
- Empty effect overrides that silently do nothing.
- Duplicate effect registration throwing unexpectedly.
- Predicate evaluation stubbed or not applied to selector results.
- Selector/post-action assumptions causing null dereferences.
- Parser errors that desynchronize the token stream or leak across compilations.
- Unknown identifiers accepted by semantic analysis.
- Unbounded loops in DSL execution.
- Effects coupled to Unity scene lookups or implicit global state.

## Verification boundary

The legacy project was inspected statically. The old Unity project was not launched, compiled, or executed during this inventory. Every migrated behavior must be covered by new tests before it is considered complete.
