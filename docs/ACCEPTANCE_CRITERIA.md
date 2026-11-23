# Acceptance criteria

The application is considered functionally integrated only when the following scenario passes automatically and can also be demonstrated from the console:

1. Load a source file containing at least two card definitions and multiple effect definitions.
2. Lex and parse the complete file with stable source locations.
3. Reject malformed syntax, unknown names, invalid types, and invalid effect parameters with diagnostics and no partial catalog publication.
4. Build a match with two players and validated decks from compiled card definitions.
5. Draw and play cards through console commands.
6. Execute at least one compiled effect that selects targets and changes engine state.
7. Resolve a round and calculate scores from board state.
8. Display the winner or tie and a concise match summary.
9. Run unit, compiler, integration, and console smoke tests successfully.

No Unity dependency may be present in Engine, Compiler, or Console. The final check includes dependency inspection and a clean build/test run using the documented .NET SDK.
