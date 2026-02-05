---
phase: 02-core-navigation
plan: 03
subsystem: lsp-core
tags: [fsharp, lsp, go-to-definition, navigation, ionide]

# Dependency graph
requires:
  - phase: 02-01
    provides: AstLookup.findNodeAtPosition for cursor-to-AST mapping
provides:
  - Go to Definition handler for variable/function navigation
  - Definition module with symbol table construction
  - Same-file definition lookup (GOTO-03 scope)
affects: [future-find-references, future-rename, multi-file-support]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "AST traversal for binding site collection"
    - "Position-based scope resolution for shadowed variables"
    - "GotoResult.Single for single-file definitions"

key-files:
  created:
    - src/LangLSP.Server/Definition.fs
    - src/LangLSP.Tests/DefinitionTests.fs
  modified:
    - src/LangLSP.Server/Server.fs
    - src/LangLSP.Server/Protocol.fs
    - src/LangLSP.Server/Hover.fs
    - src/LangLSP.Tests/ProtocolTests.fs
    - src/LangLSP.Tests/LangLSP.Tests.fsproj

key-decisions:
  - "Position-based shadowing resolution: closest preceding definition wins"
  - "Sequential test execution: testSequenced to avoid shared state interference"
  - "0-based coordinate passthrough: Protocol.fs no longer subtracts 1 (matches LexBuffer.FromString)"

patterns-established:
  - "collectDefinitions: AST traversal collecting all binding sites (Let, LetRec, Lambda, patterns)"
  - "findDefinitionForVar: filter definitions by name, select closest preceding position"
  - "handleDefinition: cursor-to-definition via findNodeAtPosition + findDefinitionForVar"

# Metrics
duration: 11min
completed: 2026-02-04
---

# Phase 02 Plan 03: Go to Definition Summary

**LSP textDocument/definition handler with symbol table construction, shadowed variable resolution, and 14 comprehensive tests**

## Performance

- **Duration:** 11 min
- **Started:** 2026-02-04T08:36:25Z
- **Completed:** 2026-02-04T08:47:02Z
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments

- Implemented Definition.fs module with collectDefinitions and findDefinitionForVar
- Registered DefinitionProvider capability in Server.fs
- Created 14 comprehensive tests covering GOTO-01/02/03 requirements
- Fixed Protocol.fs 0-based coordinate handling (discovered during testing)
- Fixed Hover.fs TypeExpr to Type conversion

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement Definition.fs module** - `b8228c9` (feat)
2. **Task 2: Register definition capability** - `eca8b6a` (feat)
3. **Task 3: Add Definition unit tests** - `3403bb8` (test)

## Deviation Commits

Bug fixes discovered during testing:

4. **Protocol.fs coordinate fix** - `c3d361f` (fix)
5. **Hover.fs TypeExpr conversion** - `9d639c1` (fix)

## Files Created/Modified

- `src/LangLSP.Server/Definition.fs` - Definition handler with symbol table
- `src/LangLSP.Server/Server.fs` - DefinitionProvider capability + handler
- `src/LangLSP.Tests/DefinitionTests.fs` - 14 unit tests
- `src/LangLSP.Server/Protocol.fs` - Fixed 0-based coordinate passthrough
- `src/LangLSP.Server/Hover.fs` - Fixed TypeExpr to Type conversion
- `src/LangLSP.Tests/ProtocolTests.fs` - Updated tests for 0-based coordinates
- `src/LangLSP.Tests/LangLSP.Tests.fsproj` - Added DefinitionTests.fs

## Decisions Made

**Position-based shadowing resolution:**
- For `let x = 1 in let x = 2 in x`, finds inner x definition
- Filter definitions by name, then select closest preceding position
- Simple heuristic that works correctly for FunLang's lexical scoping

**Sequential test execution:**
- Definition tests use `testSequenced` to avoid shared DocumentSync state
- Parallel tests were calling `clearAll()` which interfered with each other

**0-based coordinate passthrough (CRITICAL FIX):**
- Protocol.fs was incorrectly subtracting 1 from span coordinates
- LexBuffer.FromString produces 0-based spans (matches LSP)
- No conversion needed - direct passthrough is correct

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Protocol.fs coordinate off-by-one**
- **Found during:** Task 3 (running tests)
- **Issue:** spanToLspRange subtracted 1 from coordinates, but LexBuffer.FromString already produces 0-based
- **Fix:** Removed subtraction, direct passthrough
- **Files modified:** Protocol.fs, ProtocolTests.fs
- **Commit:** c3d361f

**2. [Rule 1 - Bug] Hover.fs TypeExpr mismatch**
- **Found during:** Task 3 (build error)
- **Issue:** LambdaAnnot case returned TypeExpr instead of Type
- **Fix:** Use elaborateTypeExpr to convert
- **Files modified:** Hover.fs
- **Commit:** 9d639c1

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug)
**Impact on plan:** Critical coordinate fix was needed for correctness

## Issues Encountered

**Test flakiness from parallel execution:**
- Problem: Tests randomly failing with "Should find definition"
- Investigation: Debug output showed definition found in some runs, not others
- Discovery: Tests calling clearAll() were interfering with parallel tests
- Resolution: Wrapped tests in testSequenced
- Outcome: All 14 tests pass consistently

## Next Phase Readiness

**Ready for Phase 02 completion:**
- Go to Definition fully implemented (GOTO-01, GOTO-02, GOTO-03)
- Hover implemented (from 02-02)
- AstLookup foundation provides cursor-to-AST mapping

**No blockers or concerns.**

---
*Phase: 02-core-navigation*
*Completed: 2026-02-04*
