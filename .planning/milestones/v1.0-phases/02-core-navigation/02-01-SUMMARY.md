---
phase: 02-core-navigation
plan: 01
subsystem: lsp-core
tags: [fsharp, lsp, ast, position-lookup, ionide]

# Dependency graph
requires:
  - phase: 01-lsp-foundation
    provides: Protocol module with spanToLspRange, FunLang AST with Span and spanOf
provides:
  - AstLookup module for position-based AST traversal
  - Shared foundation for Hover and Go to Definition features
  - Position conversion handling (LSP 0-based ↔ FunLang 0-based)
affects: [02-02-hover, 02-03-go-to-definition, future navigation features]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Recursive AST traversal returning innermost matching node"
    - "Position containment checking with multi-line span support"
    - "Identifier extraction from binding sites (Let, Lambda, LetRec)"

key-files:
  created:
    - src/LangLSP.Server/AstLookup.fs
    - src/LangLSP.Tests/AstLookupTests.fs
  modified:
    - src/LangLSP.Server/LangLSP.Server.fsproj
    - src/LangLSP.Tests/LangLSP.Tests.fsproj

key-decisions:
  - "LexBuffer.FromString uses 0-based lines and columns (not FsLexYacc's documented 1-based lines)"
  - "Position containment uses inclusive EndColumn (position at column N matches span ending at N)"
  - "Recursive traversal returns innermost node by checking children first"

patterns-established:
  - "findNodeAtPosition: LSP Position → Expr option for cursor-to-AST mapping"
  - "getIdentifierAtNode: Expr → string option for extracting names from binding sites"
  - "positionInSpan: Handle single-line, multi-line, and middle-line position checks"

# Metrics
duration: 8min
completed: 2026-02-04
---

# Phase 02 Plan 01: AST Position Lookup Summary

**Position-based AST traversal with 0-based coordinate conversion, handling 30+ Expr variants for Hover and Go to Definition foundation**

## Performance

- **Duration:** 8 min
- **Started:** 2026-02-04T08:24:36Z
- **Completed:** 2026-02-04T08:32:54Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- Implemented AstLookup module converting LSP positions to AST nodes
- Discovered and fixed position coordinate system mismatch (LexBuffer.FromString uses 0-based, not 1-based as documented)
- Handled all 30+ FunLang Expr variants in recursive traversal
- Created 16 comprehensive unit tests covering variables, numbers, nested expressions, and edge cases

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement AstLookup.fs module** - `c141729` (feat)
2. **Task 2: Add AstLookup unit tests** - `c84a75a` (test)

## Files Created/Modified

- `src/LangLSP.Server/AstLookup.fs` - Position-based AST traversal with findNodeAtPosition and getIdentifierAtNode
- `src/LangLSP.Tests/AstLookupTests.fs` - 16 unit tests covering position lookup scenarios
- `src/LangLSP.Server/LangLSP.Server.fsproj` - Added AstLookup.fs after Protocol.fs
- `src/LangLSP.Tests/LangLSP.Tests.fsproj` - Added AstLookupTests.fs

## Decisions Made

**LexBuffer.FromString coordinate system:**
- FsLexYacc Position.Line is documented as 1-based, but LexBuffer.FromString actually produces 0-based lines
- This matches LSP's 0-based coordinates exactly, simplifying conversion
- Column is also 0-based in both systems
- Decision: No conversion needed for positions from LexBuffer.FromString

**Span containment logic:**
- EndColumn is inclusive (span ending at column 10 includes position at column 10)
- Handles single-line, multi-line, and middle-line positions correctly
- Early exit if line is outside span range

**Recursive traversal strategy:**
- Check children first, return innermost matching node
- Fallback to current node if no child matches
- Binary operators check left then right
- Let bindings check bind expression then body expression

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed position coordinate conversion**
- **Found during:** Task 2 (running tests)
- **Issue:** Initial implementation assumed 1-based lines (FsLexYacc documentation), but LexBuffer.FromString produces 0-based lines
- **Fix:** Removed line + 1 conversion, updated comments to reflect LexBuffer.FromString behavior
- **Files modified:** src/LangLSP.Server/AstLookup.fs
- **Verification:** All 16 tests pass after fix
- **Committed in:** c84a75a (test commit includes fix)

---

**Total deviations:** 1 auto-fixed (1 blocking - coordinate system mismatch)
**Impact on plan:** Critical fix for correctness. Without it, no positions would match. No scope creep.

## Issues Encountered

**Position coordinate system debugging:**
- Problem: Tests failing with "found nothing at position" errors
- Investigation: Added debug output to see actual span values from parser
- Discovery: Parser produces StartLine=0 (0-based), not StartLine=1 (1-based as documented)
- Resolution: Updated positionInSpan to use 0-based coordinates for both line and column
- Outcome: All 16 tests pass, position lookup works correctly

## Next Phase Readiness

**Ready for Phase 02 Plans 02-03:**
- AstLookup.findNodeAtPosition provides cursor → AST node mapping
- AstLookup.getIdentifierAtNode extracts names from binding sites
- Handles all Expr variants including nested structures
- Comprehensive test coverage validates correctness

**Foundation complete for:**
- 02-02: Hover implementation (use findNodeAtPosition + type info)
- 02-03: Go to Definition (use findNodeAtPosition + getIdentifierAtNode + scope analysis)

**No blockers or concerns.**

---
*Phase: 02-core-navigation*
*Completed: 2026-02-04*
