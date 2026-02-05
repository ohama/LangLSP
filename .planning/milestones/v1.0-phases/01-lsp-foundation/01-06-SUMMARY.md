---
phase: 01-lsp-foundation
plan: 06
subsystem: client
tags: [vscode, typescript, language-client, extension, editor-integration]
requires:
  - 01-05
provides:
  - VS Code extension that activates on .fun files
  - LanguageClient connecting to F# LSP server
  - Basic language configuration for editing
affects: [01-07, 01-08]
tech-stack:
  added:
    - vscode-languageclient ^9.0.1
    - TypeScript for VS Code extension
  patterns:
    - VS Code extension activation pattern
    - LanguageClient stdio communication
key-files:
  created:
    - client/package.json
    - client/src/extension.ts
    - client/tsconfig.json
    - client/.vscodeignore
    - client/language-configuration.json
  modified: []
decisions:
  - decision: Use dotnet run for development, published executable for production
    rationale: Simplifies development workflow; allows debugging server code
    file: client/src/extension.ts
  - decision: Auto-activation via empty activationEvents (VS Code 1.74+)
    rationale: Modern VS Code auto-activates for contributed languages
    file: client/package.json
metrics:
  tasks: 3
  commits: 3
  files_created: 5
  files_modified: 0
  duration: 1min
  completed: 2026-02-04
---

# Phase 1 Plan 6: VS Code Extension Client Summary

VS Code extension with LanguageClient connecting to F# LSP server via stdio, activating automatically on .fun files with basic editing support.

## One-liner

TypeScript VS Code extension using vscode-languageclient 9.0.1 to spawn F# LSP server via dotnet run, with auto-closing pairs and comment support for .fun files.

## What Was Built

### Extension Package (package.json)

**Purpose:** VS Code extension manifest defining FunLang language support

**Key configuration:**
- name: "funlang"
- engines.vscode: "^1.74.0" (supports modern auto-activation)
- activationEvents: [] (empty array - VS Code 1.74+ auto-activates for contributed languages)
- contributes.languages: Defines funlang with .fun extension
- dependencies: vscode-languageclient ^9.0.1
- devDependencies: TypeScript, @types/vscode, @types/node

**Scripts:**
- compile: Transpile TypeScript to JavaScript
- watch: Auto-compile on file changes

### Language Configuration (language-configuration.json)

**Purpose:** Basic editing features for .fun files

**Features:**
- Line comments with //
- Bracket pairs: {}, [], ()
- Auto-closing pairs for brackets and quotes
- Surrounding pairs for selection wrapping
- Folding markers: #region / #endregion

### Extension Activation (extension.ts)

**Purpose:** Start LSP server and connect LanguageClient

**Key components:**

1. **ServerOptions:** Command to spawn F# LSP server
   - Development: `dotnet run --project ../src/LangLSP.Server/LangLSP.Server.fsproj`
   - Debug: Same as development
   - Production: Would use published executable path

2. **ClientOptions:** Document selector and file watcher
   - documentSelector: funlang files only
   - fileEvents: Watch .fun file changes

3. **LanguageClient:** Bridge between extension and server
   - Protocol: stdio (stdin/stdout)
   - Auto-starts server on extension activation
   - Auto-stops server on extension deactivation

**Path assumptions:**
- client/ and src/ are siblings
- Server project: ../src/LangLSP.Server/LangLSP.Server.fsproj

## How It Works

### Extension Activation Flow

```
User opens .fun file in VS Code
↓
VS Code detects funlang language (from package.json contributes)
↓
VS Code auto-activates extension (empty activationEvents)
↓
activate() function called
↓
LanguageClient spawns server: dotnet run --project ...
↓
Client connects to server via stdin/stdout
↓
Client sends initialize request
↓
Server responds with capabilities
↓
Client syncs document content (didOpen)
↓
Server analyzes and publishes diagnostics
↓
Red squiggles appear in editor
```

### Communication Protocol

**Transport:** stdio (standard input/output)
- Client writes JSON-RPC messages to server's stdin
- Server writes JSON-RPC responses to stdout
- Server logs to file (/tmp/langlsp.log) not stdout

**Document sync:**
- didOpen: Client sends full document text on open
- didChange: Client sends incremental or full text changes
- didClose: Client notifies server on close

**Diagnostics:**
- Server publishes diagnostics to client
- Client displays as red squiggles and Problems panel entries

## Performance

- **Duration:** 1 min
- **Started:** 2026-02-04T15:41:57Z
- **Completed:** 2026-02-04T15:43:31Z
- **Tasks:** 3
- **Files created:** 5

## Accomplishments

- VS Code extension compiles successfully with TypeScript
- LanguageClient configured to spawn F# LSP server via dotnet run
- Language configuration provides basic editing support (brackets, comments)
- Extension ready for manual testing in Extension Development Host

## Task Commits

Each task was committed atomically:

1. **Task 1: Create VS Code Extension Package** - `e5d9590` (chore)
2. **Task 2: Create Language Configuration** - `27bf6f2` (feat)
3. **Task 3: Implement Extension Activation** - `b7cbd8a` (feat)

## Files Created

- `client/package.json` - VS Code extension manifest with vscode-languageclient dependency
- `client/tsconfig.json` - TypeScript configuration (ES2020, commonjs)
- `client/.vscodeignore` - Exclude source files from packaging
- `client/language-configuration.json` - Basic editing features (brackets, comments)
- `client/src/extension.ts` - Extension activation and LanguageClient setup

## Decisions Made

### Technical Decisions

1. **Use dotnet run for development, published executable for production**
   - **Rationale:** Simplifies development workflow; allows debugging server code; matches typical LSP extension patterns
   - **Alternative considered:** Always use published executable
   - **Why this way:** Developer convenience during development; can switch to published exe for production VSIX

2. **Auto-activation via empty activationEvents (VS Code 1.74+)**
   - **Rationale:** Modern VS Code automatically activates extensions for contributed languages; explicit events unnecessary
   - **Alternative considered:** activationEvents: ["onLanguage:funlang"]
   - **Why this way:** Cleaner manifest; follows VS Code best practices; requires VS Code 1.74+ minimum

3. **Server path assumes client/ and src/ are siblings**
   - **Rationale:** Matches current project structure; simplifies relative path resolution
   - **Alternative considered:** Hardcoded absolute paths or environment variables
   - **Why this way:** Works for development; extensionContext.asAbsolutePath handles relative paths correctly

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - all tasks completed without errors.

## Verification Status

**Manual verification needed:**
1. Open VS Code with extension loaded (F5 from client folder)
2. Extension Development Host should launch
3. Open .fun file in Extension Development Host
4. LSP server should start (check via ps aux | grep dotnet)
5. Type errors in .fun file should show red squiggles
6. Problems panel should show error messages

**Automated verification:**
- ✓ npm install succeeds
- ✓ npm run compile succeeds
- ✓ client/out/extension.js generated
- ✓ package.json contains required fields
- ✓ extension.ts contains LanguageClient
- ✓ extension.ts references LangLSP.Server

## Next Phase Readiness

**Ready for manual testing:**
- ✓ Extension compiles without errors
- ✓ LanguageClient configured correctly
- ✓ Server path points to correct F# project
- ✓ Language configuration provides basic editing

**Blockers:** None

**Concerns:**
- Manual testing required to verify end-to-end flow
- Server must be runnable via dotnet run for extension to work
- Log file location (/tmp/langlsp.log) should be documented

**Next steps:**
- Manual test: Open Extension Development Host and verify diagnostics
- 01-07: Implement hover provider (show type on hover)
- 01-08: Implement completion provider (variable suggestions)

## Links

- **Plan:** `.planning/phases/01-lsp-foundation/01-06-PLAN.md`
- **Commits:**
  - e5d9590: chore(01-06): create VS Code extension package
  - 27bf6f2: feat(01-06): add language configuration for FunLang
  - b7cbd8a: feat(01-06): implement extension activation and LanguageClient
