---
phase: 05-vs-code-extension
plan: 03
subsystem: extension-packaging
tags: [vsce, vsix, dotnet-publish, extension-distribution]
requires: [05-01, 05-02]
provides:
  - Production/development mode detection in extension.ts
  - VSIX package (funlang-0.1.0.vsix)
  - .gitignore for build artifacts
affects: []
tech-stack:
  added: ["@vscode/vsce"]
  patterns: ["Production vs development mode detection via fs.existsSync"]
key-files:
  created:
    - .gitignore
    - client/package-lock.json
  modified:
    - client/src/extension.ts
    - client/.vscodeignore
    - client/package.json
decisions:
  - decision: "Production mode detection via fs.existsSync(serverDir)"
    rationale: "Extension must work both in development (dotnet run) and when installed from VSIX (bundled binary)"
    plan: "05-03"
  - decision: "Framework-dependent publish for server binary"
    rationale: "Requires .NET runtime on target machine, smaller VSIX size (3.6 MB vs self-contained)"
    plan: "05-03"
  - decision: "Exclude build artifacts from git (.gitignore)"
    rationale: "server/ and *.vsix are build outputs, should not be committed"
    plan: "05-03"
metrics:
  duration: 2.5min
  completed: 2026-02-05
---

# Phase 5 Plan 03: VS Code Extension Packaging Summary

**One-liner:** Production/development mode detection and VSIX packaging with bundled F# server binary

## What Was Built

### Extension Production Mode Detection

Updated `client/src/extension.ts` to detect whether running from installed VSIX (production) or development workspace:

**Production Mode (fs.existsSync(serverDir)):**
- Uses bundled server binary: `client/server/LangLSP.Server`
- Binary comes from `dotnet publish` output

**Development Mode (no server/ directory):**
- Uses `dotnet run --project ../src/LangLSP.Server/LangLSP.Server.fsproj`
- Allows debugging and iteration without repackaging

**Implementation:**
```typescript
import * as fs from 'fs';

const serverDir = context.asAbsolutePath(path.join('server'));

if (fs.existsSync(serverDir)) {
  // Production: bundled binary
  const serverPath = path.join(serverDir, 'LangLSP.Server');
  serverOptions = {
    run: { command: serverPath, options: { cwd: serverDir } },
    debug: { command: serverPath, options: { cwd: serverDir } }
  };
} else {
  // Development: dotnet run
  serverOptions = {
    run: { command: 'dotnet', args: ['run', '--project', ...] },
    debug: { command: 'dotnet', args: ['run', '--project', ...] }
  };
}
```

### VSIX Packaging Configuration

**Updated `.vscodeignore`:**
- Excludes development files: `src/**`, `*.ts`, `node_modules/**`, `tsconfig.json`, `test.fun`
- Includes extension assets: `!out/**`, `!server/**`, `!syntaxes/**`, `!snippets/**`, `!images/**`, `!language-configuration.json`

**Result:** Clean VSIX with only necessary runtime files (3.6 MB, 89 files)

### Build Process

**Step 1: Install vsce**
```bash
npm install --save-dev @vscode/vsce
```

**Step 2: Publish F# server**
```bash
dotnet publish src/LangLSP.Server/LangLSP.Server.fsproj -c Release -o client/server
```

**Step 3: Package VSIX**
```bash
npx vsce package --allow-missing-repository
```

**Output:** `client/funlang-0.1.0.vsix` (3.6 MB, ready for distribution)

### Build Artifact Management

**Created `.gitignore`:**
```gitignore
# Build artifacts
client/server/
client/*.vsix
```

Prevents committing generated files (server binary, VSIX package) to git.

## VSIX Contents

The packaged VSIX includes:
- **Extension code:** `out/extension.js` (compiled TypeScript)
- **Server binary:** `server/LangLSP.Server` + dependencies (80 files, 8.08 MB)
- **Language grammar:** `syntaxes/funlang.tmLanguage.json`
- **Code snippets:** `snippets/funlang.json`
- **Language configuration:** `language-configuration.json`
- **Icon:** `images/funlang-icon.png`

**Server dependencies:** FunLang.dll, Ionide.LanguageServerProtocol.dll, FSharp.Core.dll, Serilog.dll, etc.

## Requirements Met

✅ **EXT-04:** .vsix file successfully packaged
- extension.ts has production/development mode detection
- .vscodeignore properly filters VSIX contents
- Server binary bundled in client/server/
- Build artifacts (server/, *.vsix) not committed to git
- TypeScript compiles cleanly

## Task Breakdown

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Update extension.ts for production/development mode detection | 7867b2f | client/src/extension.ts, client/.vscodeignore |
| 2 | Publish server and package VSIX | deae14d | .gitignore, client/package.json, client/package-lock.json |

## Deviations from Plan

None - plan executed exactly as written. VSIX packaging succeeded on first attempt despite small icon file (115 bytes).

## Decisions Made

1. **Production mode detection via fs.existsSync(serverDir)**
   - Extension checks if `server/` directory exists
   - Production: uses bundled binary
   - Development: uses dotnet run
   - Enables seamless workflow from development to distribution

2. **Framework-dependent publish**
   - Used `dotnet publish` without self-contained flag
   - Requires .NET runtime on target machine
   - Smaller VSIX size (3.6 MB vs 50+ MB for self-contained)
   - Acceptable for LSP tutorial project

3. **Exclude build artifacts from git**
   - Created `.gitignore` for `client/server/` and `client/*.vsix`
   - These are build outputs, not source files
   - Keeps repository clean

## Testing Performed

**TypeScript Compilation:**
```bash
npm run compile
# SUCCESS - no errors
```

**Server Binary:**
```bash
ls client/server/LangLSP.Server*
# LangLSP.Server (executable)
# LangLSP.Server.dll
# LangLSP.Server.pdb
# LangLSP.Server.deps.json
# LangLSP.Server.runtimeconfig.json
```

**VSIX Package:**
```bash
ls client/funlang-0.1.0.vsix
# -rw-r--r-- 1 shoh shoh 3.6M
```

**VSIX Contents:**
```bash
npx vsce ls
# 89 files: package.json, out/, server/, syntaxes/, snippets/, images/
```

## Known Issues

None.

## Next Phase Readiness

**Blockers:** None

**Concerns:** None

**Status:** Phase 5 Wave 2 complete. All 3 plans in Phase 5 complete. Extension ready for distribution.

## Technical Notes

### Framework-Dependent vs Self-Contained Publish

**Framework-dependent (used here):**
- Requires .NET runtime on target machine
- Smaller VSIX size: 3.6 MB
- Command: `dotnet publish -c Release -o client/server`

**Self-contained (not used):**
- Bundles .NET runtime in VSIX
- Larger VSIX size: 50+ MB
- Command: `dotnet publish -c Release -r linux-x64 --self-contained -o client/server`

For a tutorial project, framework-dependent is acceptable and reduces download size.

### VSIX Packaging with vsce

**Key flags:**
- `--allow-missing-repository`: Suppresses warning about missing repository field in package.json
- `vscode:prepublish` script: Automatically runs `npm run compile` before packaging

**Verification:**
- `npx vsce ls`: List files included in VSIX
- `npx vsce ls --tree`: Show VSIX file tree

### Icon Size Warning

The plan anticipated potential icon validation errors (128x128 pixels minimum), but `vsce package` succeeded despite the 115-byte icon file. If icon validation becomes stricter in future vsce versions, temporarily remove the `icon` field from package.json to unblock packaging, then create a proper 128x128 PNG icon.

## Phase 5 Completion Summary

Phase 5 (VS Code Extension) is now complete with 3 plans executed:

1. **05-01:** Language grammar, config, snippets, icon (EXT-01, EXT-02, EXT-03)
2. **05-02:** LSP integration tests (119 tests passing)
3. **05-03:** Extension packaging (EXT-04) ✅

**Final deliverable:** Fully functional VS Code extension packaged as `funlang-0.1.0.vsix`, ready for installation and distribution.

**Installation:**
```bash
code --install-extension client/funlang-0.1.0.vsix
```

All LSP features from Phases 1-4 (diagnostics, hover, completion, definition, references, rename, code actions, unused variable detection) now available in VS Code via this extension.
