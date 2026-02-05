# Phase 5: VS Code Extension - Research

**Researched:** 2026-02-05
**Domain:** VS Code Extension packaging, TextMate grammars, language configuration, snippets, VSIX packaging
**Confidence:** HIGH

## Summary

Phase 5 requires completing the VS Code extension that was started in Phase 1 (plan 01-06). The existing `client/` directory already has a working LSP client (`extension.ts`) with `vscode-languageclient`, a basic `language-configuration.json`, compiled JavaScript output, and a `package.json`. What is missing: a TextMate grammar for syntax highlighting (EXT-01), enriched language configuration (EXT-02), code snippets (EXT-03), VSIX packaging capability (EXT-04), integration tests (TEST-10), and the packaging tutorial (TUT-12).

The FunLang language has been fully analyzed from the lexer (`Lexer.fsl`) and parser (`Parser.fsy`). It has 10 keywords (`let`, `in`, `if`, `then`, `else`, `match`, `with`, `fun`, `rec`, `true`, `false`), 4 type keywords (`int`, `bool`, `string`, `list`), two comment forms (`//` line comments and `(* *)` nestable block comments), string literals with escape sequences, integer literals, ML-style operators, and ML-style constructs (lambda, pattern matching, cons, tuples, lists, type annotations).

**Primary recommendation:** Create a TextMate grammar JSON for FunLang syntax highlighting, enrich language-configuration.json with block comment support, add snippets for common FunLang constructs, install `@vscode/vsce` for VSIX packaging, use protocol-level F# tests for LSP integration testing (not `@vscode/test-electron`), and write the tutorial in Korean following existing tutorial style.

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| @vscode/vsce | latest (>=2.10.0) | VSIX packaging CLI | Official Microsoft tool for VS Code extension packaging |
| vscode-languageclient | ^9.0.1 | LSP client | Already installed in client/; official VS Code LSP client |
| TypeScript | ^5.0.0 | Extension source | Already in use |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| @types/vscode | ^1.74.0 | VS Code type definitions | Already installed |
| @types/node | ^20.0.0 | Node.js types | Already installed |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| @vscode/vsce | ovsx | ovsx is for Open VSX Registry (Eclipse); vsce is for VS Code marketplace. Use vsce for local .vsix packaging. |
| @vscode/test-electron | F# protocol-level tests | @vscode/test-electron requires VS Code installation, headless display, and Node.js test infrastructure. F# protocol-level tests (already 116 passing) test the actual LSP logic without VS Code runtime. Use F# tests for server logic, manual testing for extension client. |
| esbuild bundling | no bundling | For a small extension like this, bundling is unnecessary. The extension.ts compiles to a single extension.js. |

**Installation:**
```bash
cd client
npm install -g @vscode/vsce
# Or as devDependency:
npm install --save-dev @vscode/vsce
```

## Architecture Patterns

### Current Extension Structure (client/)
```
client/
├── language-configuration.json  # Language config (EXISTS - needs block comments)
├── package.json                 # Extension manifest (EXISTS - needs grammars, snippets)
├── package-lock.json            # Lock file (EXISTS)
├── tsconfig.json                # TypeScript config (EXISTS)
├── .vscodeignore                # Package exclusions (EXISTS - needs update)
├── node_modules/                # Dependencies (EXISTS)
├── src/
│   └── extension.ts             # LSP client entry (EXISTS - complete)
├── out/
│   └── extension.js             # Compiled JS (EXISTS)
├── test.fun                     # Test file (EXISTS)
├── syntaxes/                    # NEW: TextMate grammar
│   └── funlang.tmLanguage.json  # NEW: Syntax highlighting rules
├── snippets/                    # NEW: Code snippets
│   └── funlang.json             # NEW: Snippet definitions
└── images/                      # NEW: Extension icon
    └── funlang-icon.png         # NEW: 128x128+ PNG icon
```

### Pattern 1: TextMate Grammar for FunLang

**What:** A JSON-based TextMate grammar that tokenizes FunLang source code into scoped tokens for VS Code theme coloring.

**When to use:** Required for syntax highlighting (EXT-01).

**FunLang Token Categories (derived from Lexer.fsl analysis):**

| Token Category | FunLang Tokens | TextMate Scope |
|----------------|----------------|----------------|
| Control keywords | `if`, `then`, `else`, `match`, `with`, `let`, `in`, `fun`, `rec` | `keyword.control.funlang` |
| Boolean literals | `true`, `false` | `constant.language.boolean.funlang` |
| Type keywords | `int`, `bool`, `string`, `list` | `support.type.funlang` |
| Numeric literals | `[0-9]+` | `constant.numeric.integer.funlang` |
| String literals | `"..."` with escapes `\n`, `\t`, `\\`, `\"` | `string.quoted.double.funlang` |
| Escape sequences | `\n`, `\t`, `\\`, `\"` | `constant.character.escape.funlang` |
| Line comments | `// ...` | `comment.line.double-slash.funlang` |
| Block comments | `(* ... *)` (nestable) | `comment.block.funlang` |
| Identifiers | `[a-zA-Z_][a-zA-Z0-9_]*` | (default scope / `variable.other.funlang`) |
| Type variables | `'a`, `'b` etc. | `variable.parameter.type.funlang` |
| Operators | `+`, `-`, `*`, `/`, `=`, `<>`, `<`, `>`, `<=`, `>=`, `&&`, `||`, `::`, `->` | `keyword.operator.funlang` |
| Brackets | `(`, `)`, `[`, `]` | `punctuation.bracket.funlang` |
| Pipe | `\|` | `keyword.operator.pipe.funlang` |
| Wildcard | `_` (standalone) | `variable.language.wildcard.funlang` |
| Comma | `,` | `punctuation.separator.comma.funlang` |

**Example grammar structure:**
```json
{
  "$schema": "https://raw.githubusercontent.com/martinring/tmlanguage/master/tmlanguage.json",
  "name": "FunLang",
  "scopeName": "source.funlang",
  "patterns": [
    { "include": "#comments" },
    { "include": "#strings" },
    { "include": "#constants" },
    { "include": "#keywords" },
    { "include": "#types" },
    { "include": "#operators" },
    { "include": "#identifiers" }
  ],
  "repository": {
    "comments": {
      "patterns": [
        {
          "name": "comment.line.double-slash.funlang",
          "match": "//.*$"
        },
        {
          "name": "comment.block.funlang",
          "begin": "\\(\\*",
          "end": "\\*\\)",
          "patterns": [
            { "include": "#block-comment-nested" }
          ]
        }
      ]
    },
    "block-comment-nested": {
      "name": "comment.block.nested.funlang",
      "begin": "\\(\\*",
      "end": "\\*\\)",
      "patterns": [
        { "include": "#block-comment-nested" }
      ]
    },
    "strings": {
      "name": "string.quoted.double.funlang",
      "begin": "\"",
      "end": "\"",
      "patterns": [
        {
          "name": "constant.character.escape.funlang",
          "match": "\\\\[nrt\\\\\"']"
        }
      ]
    },
    "constants": {
      "patterns": [
        {
          "name": "constant.numeric.integer.funlang",
          "match": "\\b[0-9]+\\b"
        },
        {
          "name": "constant.language.boolean.funlang",
          "match": "\\b(true|false)\\b"
        }
      ]
    },
    "keywords": {
      "patterns": [
        {
          "name": "keyword.control.funlang",
          "match": "\\b(if|then|else|match|with|let|in|fun|rec)\\b"
        }
      ]
    },
    "types": {
      "patterns": [
        {
          "name": "support.type.funlang",
          "match": "\\b(int|bool|string|list)\\b"
        },
        {
          "name": "variable.parameter.type.funlang",
          "match": "'[a-zA-Z][a-zA-Z0-9_]*"
        }
      ]
    },
    "operators": {
      "patterns": [
        {
          "name": "keyword.operator.arrow.funlang",
          "match": "->"
        },
        {
          "name": "keyword.operator.cons.funlang",
          "match": "::"
        },
        {
          "name": "keyword.operator.comparison.funlang",
          "match": "<>|<=|>=|<|>"
        },
        {
          "name": "keyword.operator.logical.funlang",
          "match": "&&|\\|\\|"
        },
        {
          "name": "keyword.operator.arithmetic.funlang",
          "match": "[+\\-*/]"
        },
        {
          "name": "keyword.operator.assignment.funlang",
          "match": "="
        },
        {
          "name": "keyword.operator.pipe.funlang",
          "match": "\\|"
        }
      ]
    },
    "identifiers": {
      "patterns": [
        {
          "name": "variable.language.wildcard.funlang",
          "match": "\\b_\\b"
        }
      ]
    }
  }
}
```

**Critical ordering rule:** Multi-character operators (`->`, `::`, `<>`, `<=`, `>=`, `&&`, `||`) must appear before their single-character sub-patterns. Also, `true`/`false` must match before general identifiers (handled by `\b` word boundaries in the keyword pattern).

### Pattern 2: Language Configuration for FunLang

**What:** Enhanced `language-configuration.json` with both comment types and all bracket pairs.

**Current state (needs update):**
- Line comment: `//` -- already present
- Block comment: `(* *)` -- MISSING, must add
- Brackets: `{}`, `[]`, `()` -- present (remove `{}` since FunLang does not use curly braces)
- Auto-closing pairs: present but needs `(* *)` pair
- Folding markers: present (region-based)

**Updated language configuration:**
```json
{
  "comments": {
    "lineComment": "//",
    "blockComment": ["(*", "*)"]
  },
  "brackets": [
    ["[", "]"],
    ["(", ")"]
  ],
  "autoClosingPairs": [
    { "open": "[", "close": "]" },
    { "open": "(", "close": ")" },
    { "open": "\"", "close": "\"", "notIn": ["string"] },
    { "open": "(*", "close": "*)", "notIn": ["string"] }
  ],
  "surroundingPairs": [
    ["[", "]"],
    ["(", ")"],
    ["\"", "\""]
  ],
  "autoCloseBefore": ";:.,=}])>` \n\t",
  "indentationRules": {
    "increaseIndentPattern": "\\b(let|if|then|else|match|with|fun)\\b.*$",
    "decreaseIndentPattern": "^\\s*\\b(in|else|with)\\b"
  }
}
```

### Pattern 3: VS Code Snippets for FunLang

**What:** JSON snippet definitions for common FunLang constructs.

**Snippet file location:** `snippets/funlang.json`

**Required snippets (from success criteria + FunLang constructs):**

| Prefix | Construct | Body Template |
|--------|-----------|---------------|
| `let` | Let binding | `let ${1:name} = ${2:value} in\n${0}` |
| `letrec` | Recursive function | `let rec ${1:name} ${2:param} =\n\t${3:body}\nin\n${0}` |
| `if` | If-then-else | `if ${1:condition}\nthen ${2:consequent}\nelse ${3:alternative}` |
| `match` | Pattern match | `match ${1:expr} with\n\| ${2:pattern} -> ${3:body}\n\| ${4:_} -> ${0:default}` |
| `fun` | Lambda | `fun ${1:x} -> ${0:body}` |
| `matchlist` | List match | `match ${1:xs} with\n\| [] -> ${2:base}\n\| ${3:h} :: ${4:t} -> ${0:recursive}` |

### Pattern 4: Package.json Contributes Extension

**What:** The package.json `contributes` section must declare the grammar, snippets, and language configuration.

**Required additions to existing package.json:**
```json
{
  "contributes": {
    "languages": [{
      "id": "funlang",
      "aliases": ["FunLang", "funlang"],
      "extensions": [".fun"],
      "configuration": "./language-configuration.json",
      "icon": {
        "light": "./images/funlang-icon.png",
        "dark": "./images/funlang-icon.png"
      }
    }],
    "grammars": [{
      "language": "funlang",
      "scopeName": "source.funlang",
      "path": "./syntaxes/funlang.tmLanguage.json"
    }],
    "snippets": [{
      "language": "funlang",
      "path": "./snippets/funlang.json"
    }]
  }
}
```

### Pattern 5: VSIX Packaging

**What:** Package the extension as a .vsix file using `@vscode/vsce`.

**Required package.json fields for vsce:**
- `publisher`: Must be set (use `"funlang"`)
- `repository`: Optional but recommended (GitHub URL)
- `icon`: Path to 128x128+ PNG icon
- `engines.vscode`: Already set to `"^1.74.0"`
- `version`: Already `"0.1.0"`
- `description`: Already set

**Server bundling approach:**
The extension currently uses `dotnet run` to start the server. For VSIX packaging, the server should be published via `dotnet publish` and bundled inside the extension as a `server/` directory. The `extension.ts` already has a `serverPath` reference for published server:

```typescript
// Already in extension.ts line 14-16:
const serverPath = context.asAbsolutePath(
  path.join('server', 'LangLSP.Server')
);
```

The `ServerOptions` should be updated to distinguish between development (dotnet run) and production (bundled executable) modes.

**Build script for packaging:**
```bash
# 1. Publish the F# server as self-contained (optional) or framework-dependent
dotnet publish src/LangLSP.Server/LangLSP.Server.fsproj -c Release -o client/server

# 2. Compile TypeScript
cd client && npm run compile

# 3. Package VSIX
cd client && vsce package
```

### Anti-Patterns to Avoid
- **Over-complicated TextMate grammar:** Do not try to parse FunLang expressions with TextMate regex. TextMate grammars are for lexical tokenization only. Semantic highlighting is already provided by the LSP server.
- **Bundling with esbuild/webpack for this small extension:** The extension.ts compiles to a single file. Adding a bundler is unnecessary complexity.
- **Using @vscode/test-electron for LSP integration tests:** This requires a VS Code installation, X11/headless display on Linux, and significantly more setup. The existing F# Expecto tests already cover all LSP logic at the protocol level.
- **Including node_modules in VSIX:** Use `--no-dependencies` flag if bundling, or ensure devDependencies are excluded via `.vscodeignore`.
- **Curly braces in language configuration:** FunLang does not use `{` `}`. Remove them from brackets/autoClosingPairs to avoid confusing auto-closing behavior.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| VSIX packaging | Custom zip/archive scripts | `@vscode/vsce package` | Handles manifest generation, validation, compression correctly |
| TextMate grammar | Ad-hoc regex tokenizer | Standard TextMate JSON format | VS Code's tokenizer understands only TextMate grammars; standard scopes get theme support for free |
| Snippet variables | Custom snippet engine | VS Code built-in snippet format with `$1`, `${1:placeholder}` | VS Code handles tabstops, placeholders, choice lists natively |
| LSP client | Custom JSON-RPC over stdin/stdout | `vscode-languageclient` | Already in use; handles protocol negotiation, capability matching |
| Comment toggling | Custom comment toggle commands | `language-configuration.json` `comments` field | VS Code uses the language config to implement Ctrl+/ and Ctrl+Shift+A automatically |

**Key insight:** VS Code provides declarative contribution points (grammars, language-configuration, snippets) that "just work" when the JSON files are correctly structured and registered in package.json. No imperative code is needed for syntax highlighting, bracket matching, comment toggling, or snippet insertion.

## Common Pitfalls

### Pitfall 1: TextMate Grammar Operator Ordering
**What goes wrong:** Single-character operators match before multi-character operators, causing `->` to be tokenized as `-` then `>`, or `::` as `:` then `:`.
**Why it happens:** TextMate grammars match patterns in order within a patterns array. If `-` or `>` patterns come before `->`, they will match first.
**How to avoid:** Always list multi-character operator patterns before single-character ones. In the `operators` repository section, put `->`, `::`, `<>`, `<=`, `>=`, `&&`, `||` before `+`, `-`, `*`, `/`, `=`, `<`, `>`.
**Warning signs:** Arrow (`->`) displays as two differently-colored characters; cons (`::`) gets split highlighting.

### Pitfall 2: Nested Block Comments in TextMate
**What goes wrong:** `(* outer (* inner *) still comment *)` -- the first `*)` terminates the entire comment, leaving `still comment *)` unscoped.
**Why it happens:** TextMate's `begin/end` patterns don't support nesting by default.
**How to avoid:** Use a self-referencing pattern: the block comment rule's internal `patterns` array includes itself. This creates a recursive nesting structure that TextMate supports.
**Warning signs:** Code after nested comments appears unhighlighted or incorrectly colored.

### Pitfall 3: Keyword Matching Without Word Boundaries
**What goes wrong:** `letter` matches the `let` keyword pattern; `infinity` matches `in`; `functional` matches `fun`.
**Why it happens:** Keyword regex like `let|in|fun` without `\b` boundaries matches partial identifiers.
**How to avoid:** Always use `\b` word boundaries: `\b(let|in|if|then|else|match|with|fun|rec)\b`.
**Warning signs:** Parts of identifiers get keyword coloring.

### Pitfall 4: VSIX Missing Server Binary
**What goes wrong:** Extension installs but LSP features don't work (no diagnostics, no hover, no completion).
**Why it happens:** The `dotnet run` server path works in development but the `server/` directory isn't included in the VSIX package.
**How to avoid:** Run `dotnet publish` to `client/server/` before `vsce package`. Update `.vscodeignore` to NOT exclude the `server/` directory. Update `extension.ts` to detect whether running in development or from VSIX.
**Warning signs:** Extension loads but shows "Server process exited" in Output panel.

### Pitfall 5: Publisher Field Missing
**What goes wrong:** `vsce package` fails with "Missing publisher name."
**Why it happens:** The `publisher` field in package.json is required by vsce but was not needed for development.
**How to avoid:** Add `"publisher": "funlang"` to package.json before running vsce package. Any string works for local-only distribution.
**Warning signs:** vsce exits with error immediately.

### Pitfall 6: Language Configuration blockComment Not Working
**What goes wrong:** User presses Ctrl+Shift+A but nothing happens or wrong comment style is inserted.
**Why it happens:** `blockComment` must be an array of exactly 2 strings: `["(*", "*)"]`. Common mistake is using wrong format or forgetting to add it.
**How to avoid:** Add `"blockComment": ["(*", "*)"]` to the `comments` section of `language-configuration.json`.
**Warning signs:** Block comment toggle keyboard shortcut does nothing for .fun files.

### Pitfall 7: Snippet Tabstop/Placeholder Escaping
**What goes wrong:** Snippets insert literal `$1` text or break at special characters.
**Why it happens:** Dollar signs, curly braces, and backslashes need escaping in snippet bodies.
**How to avoid:** Use `\\$` for literal dollar, `\\}` for literal closing brace. Test each snippet manually in VS Code.
**Warning signs:** Inserted snippet text contains `$1` literally or tab key doesn't move between placeholders.

## Code Examples

### Example 1: TextMate Grammar Registration in package.json
```json
{
  "contributes": {
    "grammars": [{
      "language": "funlang",
      "scopeName": "source.funlang",
      "path": "./syntaxes/funlang.tmLanguage.json"
    }]
  }
}
```
Source: VS Code Syntax Highlight Guide

### Example 2: Snippet with Placeholders and Tabstops
```json
{
  "Let Binding": {
    "prefix": "let",
    "body": [
      "let ${1:name} = ${2:value} in",
      "${0}"
    ],
    "description": "Let binding expression"
  },
  "Match Expression": {
    "prefix": "match",
    "body": [
      "match ${1:expr} with",
      "| ${2:pattern} -> ${3:body}",
      "| ${4:_} -> ${0:default}"
    ],
    "description": "Pattern match expression"
  }
}
```
Source: VS Code Snippets Documentation

### Example 3: VSIX Packaging Commands
```bash
# Install vsce
npm install -g @vscode/vsce

# Publish F# server
dotnet publish src/LangLSP.Server/LangLSP.Server.fsproj \
  -c Release -o client/server

# Compile TypeScript
cd client && npm run compile

# Package VSIX
cd client && vsce package --allow-missing-repository
# Output: funlang-0.1.0.vsix

# Install locally
code --install-extension funlang-0.1.0.vsix
```
Source: VS Code Publishing Extensions guide

### Example 4: Extension.ts Server Mode Detection
```typescript
// Detect whether running from VSIX (production) or development
const serverDir = context.asAbsolutePath(path.join('server'));
const fs = require('fs');

let serverOptions: ServerOptions;

if (fs.existsSync(serverDir)) {
  // Production: use published server binary
  const serverPath = path.join(serverDir, 'LangLSP.Server');
  serverOptions = {
    run: { command: serverPath, options: { cwd: serverDir } },
    debug: { command: serverPath, options: { cwd: serverDir } }
  };
} else {
  // Development: use dotnet run
  serverOptions = {
    run: {
      command: 'dotnet',
      args: ['run', '--project', context.asAbsolutePath(
        path.join('..', 'src', 'LangLSP.Server', 'LangLSP.Server.fsproj')
      )],
      options: { cwd: context.asAbsolutePath('..') }
    },
    debug: {
      command: 'dotnet',
      args: ['run', '--project', context.asAbsolutePath(
        path.join('..', 'src', 'LangLSP.Server', 'LangLSP.Server.fsproj')
      )],
      options: { cwd: context.asAbsolutePath('..') }
    }
  };
}
```

### Example 5: .vscodeignore for Clean VSIX
```
.vscode/**
src/**
node_modules/**
tsconfig.json
test.fun
*.ts
!out/**
!server/**
!syntaxes/**
!snippets/**
!images/**
!language-configuration.json
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `vsce` (standalone) | `@vscode/vsce` (scoped package) | 2022 | Use `@vscode/vsce` for npm install |
| Custom test runner scripts | `@vscode/test-cli` + `@vscode/test-electron` | 2023 | Simplified e2e testing setup |
| tmLanguage (plist/XML) | tmLanguage.json (JSON) | Long ago | JSON is now standard for VS Code grammars |
| Manual VSIX construction | `vsce package` | Stable | Always use vsce, never manual zip |

**Deprecated/outdated:**
- `vscode` npm package (replaced by `@types/vscode` + `@vscode/test-electron`)
- XML plist tmLanguage format (JSON is preferred for VS Code)
- `vsce` unscoped package (use `@vscode/vsce`)

## Integration Testing Strategy (TEST-10)

### Recommended Approach: F# Protocol-Level Tests

The project already has 116 passing Expecto tests that test all LSP features at the protocol/module level. For Phase 5, extend this approach:

1. **Grammar tests:** Manual verification by opening .fun files in VS Code and using "Developer: Inspect Editor Tokens and Scopes" command. TextMate grammars are declarative JSON -- their correctness is verified by visual inspection and the VS Code built-in tokenizer.

2. **Language configuration tests:** Manual verification that comment toggling, bracket matching, and auto-closing work correctly.

3. **Snippet tests:** Manual verification that each snippet triggers correctly and placeholders navigate properly.

4. **LSP integration tests (TEST-10):** Add F# tests that verify the full request/response flow:
   - Parse a FunLang source
   - Simulate initialize -> didOpen -> completion/hover/definition -> didClose sequence
   - Verify correct LSP response structures
   - These tests already exist for individual features; TEST-10 adds an end-to-end sequence test

5. **VSIX installation test:** Manual verification that `code --install-extension funlang.vsix` succeeds and extension appears in Extensions panel.

### Why Not @vscode/test-electron

- Requires downloading VS Code binary (~200MB)
- Requires display server (X11 or Xvfb) on Linux/WSL
- Requires Node.js test infrastructure (Mocha) separate from F# Expecto
- Adds significant CI complexity for marginal benefit
- The LSP server logic is already thoroughly tested at the protocol level

## Tutorial (TUT-12) Patterns

### Existing Tutorial Style (from documentation/tutorial/)

All 11 existing tutorials follow this pattern:
- Written in Korean
- Markdown with headers, code blocks, tables
- Conceptual explanation first, then implementation
- Code examples with inline comments
- "Test" section showing how to verify
- Cross-references to LSP specification

### Tutorial 12 Structure (VS Code Extension Packaging)

Follow the same style. Key sections:
1. VS Code Extension 구조 (Architecture)
2. TextMate Grammar 개념 (Concepts)
3. Language Configuration 설정 (Language Config)
4. 코드 스니펫 작성 (Snippets)
5. package.json Contributes 설정 (Manifest)
6. VSIX 패키징 (Packaging)
7. 로컬 설치 및 테스트 (Installation & Testing)

## Open Questions

1. **Extension icon**
   - What we know: VSIX packaging requires a 128x128+ PNG icon
   - What's unclear: Whether to create a custom icon or use a placeholder
   - Recommendation: Create a simple text-based icon (e.g., "FL" or lambda symbol) using any image tool. A 128x128 PNG with a solid color background and text is sufficient for a tutorial project.

2. **Server binary platform**
   - What we know: `dotnet publish` can create framework-dependent or self-contained binaries
   - What's unclear: Whether to target a specific runtime or require .NET 10 SDK installed
   - Recommendation: Use framework-dependent publish (smaller size, user needs .NET runtime). The target audience already has dotnet installed since they built the project. Use `dotnet publish -c Release` without `--self-contained`.

3. **Comment continuation behavior**
   - What we know: Success criterion #2 says "User types '//' and line comment continues on new line automatically"
   - What's unclear: VS Code does not natively continue `//` comments on Enter by default. This requires either `onEnterRules` in language-configuration.json or accepting that only Ctrl+/ toggling works.
   - Recommendation: Add `onEnterRules` to language-configuration.json to continue `//` comments:
     ```json
     "onEnterRules": [
       {
         "beforeText": "^\\s*\\/\\/.*$",
         "action": { "indent": "none", "appendText": "// " }
       }
     ]
     ```

## Sources

### Primary (HIGH confidence)
- FunLang Lexer.fsl -- Complete token definitions, all keywords, operators, comment syntax
- FunLang Parser.fsy -- Complete grammar rules, all language constructs
- FunLang Ast.fs -- All AST node types
- client/package.json, extension.ts, language-configuration.json -- Current extension state
- VS Code Syntax Highlight Guide: https://code.visualstudio.com/api/language-extensions/syntax-highlight-guide
- VS Code Publishing Extensions: https://code.visualstudio.com/api/working-with-extensions/publishing-extension
- VS Code Snippets Documentation: https://code.visualstudio.com/docs/editing/userdefinedsnippets

### Secondary (MEDIUM confidence)
- TextMate Manual (Naming Conventions): https://macromates.com/manual/en/language_grammars
- tmLanguage JSON Schema: https://github.com/martinring/tmlanguage
- VS Code Extension Testing: https://code.visualstudio.com/api/working-with-extensions/testing-extension

### Tertiary (LOW confidence)
- vsce Deep Wiki: https://deepwiki.com/microsoft/vscode-vsce/2-getting-started

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - well-established VS Code extension toolchain, verified against official docs
- Architecture: HIGH - TextMate grammar format well-documented, FunLang token set fully analyzed from lexer source
- Pitfalls: HIGH - common issues documented by multiple sources and derived from lexer analysis
- Integration testing: MEDIUM - recommended F# protocol-level approach is pragmatic but not the "official" VS Code testing path
- Tutorial: HIGH - 11 existing tutorials provide clear style to follow

**Research date:** 2026-02-05
**Valid until:** 2026-03-07 (30 days -- VS Code extension API is stable)
