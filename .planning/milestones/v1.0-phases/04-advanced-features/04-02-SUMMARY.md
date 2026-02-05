---
phase: 04-advanced-features
plan: 02
subsystem: lsp-features
tags: [rename, code-actions, quickfix, diagnostics, ionide, funlang]

# Dependency graph
requires:
  - phase: 04-01
    provides: collectReferencesForBinding and WorkspaceEdit helpers for rename implementation
provides:
  - Rename Symbol with prepareRename validation and full rename with WorkspaceEdit
  - Code Actions with quickfix for unused variables and informational type hints
  - Unused variable diagnostics with Warning severity and DiagnosticTag.Unnecessary
affects: [04-03-testing, 05-tutorials]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "PrepareRename pattern: validate renameable symbols before rename dialog"
    - "findNameInSource pattern: extract tight name-only spans from broad definition spans"
    - "Code action dispatch: route by diagnostic code to appropriate quickfix generators"

key-files:
  created:
    - src/LangLSP.Server/Rename.fs
    - src/LangLSP.Server/CodeActions.fs
  modified:
    - src/LangLSP.Server/Diagnostics.fs
    - src/LangLSP.Server/Server.fs
    - src/LangLSP.Server/LangLSP.Server.fsproj

key-decisions:
  - "findNameInSource for definition sites: AST spans for Let/LetRec/Lambda cover whole expressions, search source text for exact name location"
  - "DiagnosticTag.Unnecessary for unused vars: enables VS Code fading/dimming UI treatment"
  - "Warning severity for unused variables: yellow squiggle, not red error"
  - "Skip underscore-prefixed variables: convention for intentionally unused bindings"
  - "Informational code actions for type errors: show expected type without automatic fix"

patterns-established:
  - "Rename pattern: prepareRename validates → rename collects references via collectReferencesForBinding → WorkspaceEdit with all edits"
  - "Code action pattern: extract variable name from diagnostic message, generate quickfix with WorkspaceEdit"
  - "Unused variable detection: traverse AST, check collectReferences in binding scope, exclude _-prefixed names"

# Metrics
duration: 3min
completed: 2026-02-04
---

# Phase 04 Plan 02: Rename Symbol and Code Actions Summary

**Rename Symbol with shadowing-aware reference collection and Code Actions with unused variable quickfix and type error hints**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-04T23:04:56Z
- **Completed:** 2026-02-04T23:07:47Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments

- Rename Symbol (RENAME-01, RENAME-02, RENAME-03) with prepareRename validation and full WorkspaceEdit generation
- Code Actions with "Prefix with underscore" quickfix for unused variables (ACTION-01)
- Code Actions with informational "Expected type" hints for type errors (ACTION-02)
- Unused variable diagnostics with Warning severity and DiagnosticTag.Unnecessary for VS Code UI fading

## Task Commits

Each task was committed atomically:

1. **Task 1: Create Rename.fs with rename and prepareRename handlers** - `ee03a83` (feat)
2. **Task 2: Create CodeActions.fs, update Diagnostics.fs, complete Server.fs** - `dbd53e6` (feat)

## Files Created/Modified

- `src/LangLSP.Server/Rename.fs` - Rename Symbol implementation with handlePrepareRename (validates Var, Let, LetRec, Lambda, LambdaAnnot) and handleRename (collects scoped references + definition name span, returns WorkspaceEdit)
- `src/LangLSP.Server/CodeActions.fs` - Code Actions with createPrefixUnderscoreAction (ACTION-01 quickfix) and createTypeInfoAction (ACTION-02 informational hint)
- `src/LangLSP.Server/Diagnostics.fs` - Added findUnusedVariables function, updated analyze to return unused variable warnings with DiagnosticTag.Unnecessary
- `src/LangLSP.Server/Server.fs` - Registered RenameProvider with PrepareProvider=true and CodeActionProvider with quickfix kind, added handler functions
- `src/LangLSP.Server/LangLSP.Server.fsproj` - Added Rename.fs and CodeActions.fs to compilation order

## Decisions Made

1. **findNameInSource for tight spans:** AST spans for Let/LetRec/Lambda cover entire expressions (e.g., `let x = 5 in x + 1` span covers everything). For rename edits, need just the identifier name. Solution: search first ~15 chars of source text at span start for exact name location. This prevents rename from replacing entire expression.

2. **DiagnosticTag.Unnecessary for unused variables:** LSP spec defines DiagnosticTag.Unnecessary for unused code. VS Code renders this with faded/dimmed text. Applied to unused variable diagnostics for visual distinction from errors.

3. **Warning severity for unused variables:** Unused variables are warnings (yellow squiggle), not errors (red squiggle). Allows code to run while signaling cleanup opportunity.

4. **Skip underscore-prefixed variables:** Convention from F#/Rust/etc - variables starting with `_` are intentionally unused (e.g., `let _result = ...` when you need side effect but not value). Excluded from unused detection.

5. **Informational code actions for type errors:** Type error quickfix (ACTION-02) shows expected type as informational action with Edit = None. No automatic fix (type errors require context-dependent solutions), but surfaces type info prominently.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - implementation straightforward given existing References infrastructure (collectReferencesForBinding, createTextEdit, createWorkspaceEdit from 04-01).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Rename Symbol and Code Actions fully implemented
- Ready for testing in 04-03 (regression tests, tutorial validation)
- Note: Diagnostics.fs change (added unused variable detection) WILL affect existing tests that expect no warnings on valid programs. 04-03 will handle test updates.

---
*Phase: 04-advanced-features*
*Completed: 2026-02-04*
