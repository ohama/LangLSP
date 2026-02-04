# Phase 1: LSP Foundation - Research

**Researched:** 2026-02-04
**Domain:** F# Language Server Protocol (LSP) implementation
**Confidence:** HIGH

## Summary

This research investigates how to implement a Language Server Protocol (LSP) server in F# using Ionide.LanguageServerProtocol, with a VS Code extension client to provide real-time diagnostics for the FunLang language. The LSP ecosystem is mature and well-documented, with clear patterns for F# implementations proven by FsAutoComplete (Ionide's F# language server).

The standard approach uses a two-process architecture: a TypeScript VS Code extension (client) that activates when .fun files are opened, and an F# LSP server process that communicates via JSON-RPC over stdin/stdout. Document synchronization tracks file open/change/close events, and diagnostics are pushed to VS Code using `textDocument/publishDiagnostics` notifications.

FunLang's existing Diagnostic.fs module with Span and typeErrorToDiagnostic provides an excellent foundation - the main work is converting between FunLang's Span type (1-based line/column) and LSP's Position/Range types (0-based) and implementing the communication protocol.

**Primary recommendation:** Use Ionide.LanguageServerProtocol 0.7.0 with incremental document sync (TextDocumentSyncKind.Incremental) and leverage FunLang's existing Diagnostic module for error reporting. Focus on minimal viable implementation: stdio transport, textDocument/didOpen/didChange/didClose, and publishDiagnostics.

## Standard Stack

The established libraries/tools for F# LSP implementation:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Ionide.LanguageServerProtocol | 0.7.0 | LSP protocol implementation in F# | F# native, battle-tested by FsAutoComplete, lightweight, actively maintained (released 2025-03-12) |
| vscode-languageclient | Latest (10.x) | VS Code extension LSP client library | Official Microsoft library, handles JSON-RPC and LSP communication, used by all VS Code language extensions |
| Expecto | 10.2.3+ | F# testing framework | Idiomatic F#, human-friendly API, already used in FunLang project |
| Expecto.FsCheck | 10.2.3+ | Property-based testing integration | Integrates FsCheck with Expecto, essential for testing position/range calculations |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| FSharp.Compiler.Service | Latest | F# compiler integration | If building F# language server (not needed for FunLang) |
| StreamJsonRpc | 2.x | Alternative JSON-RPC | If not using Ionide.LanguageServerProtocol's built-in RPC |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Ionide.LanguageServerProtocol | OmniSharp.LanguageServerProtocol | OmniSharp is C#-focused, heavier, more .NET ecosystem coupling vs Ionide's lightweight F#-first design |
| Ionide.LanguageServerProtocol | Custom LSP implementation | Would need to implement 90 methods and 407 types from scratch; LSP spec is 285 pages |
| vscode-languageclient | Custom VS Code extension client | Would reinvent JSON-RPC, message framing, LSP protocol handling |

**Installation:**

F# Server:
```bash
dotnet add package Ionide.LanguageServerProtocol --version 0.7.0
dotnet add package Expecto --version 10.2.3
dotnet add package Expecto.FsCheck --version 10.2.3
```

VS Code Extension (client):
```bash
npm install vscode-languageclient
```

## Architecture Patterns

### Recommended Project Structure
```
LangLSP/
├── src/
│   ├── LangLSP.Server/          # F# LSP server process
│   │   ├── Server.fs            # Main LSP server initialization
│   │   ├── DocumentSync.fs      # textDocument/didOpen/didChange/didClose handlers
│   │   ├── Diagnostics.fs       # Convert FunLang Diagnostic -> LSP Diagnostic
│   │   ├── Protocol.fs          # LSP type conversions (Span <-> Position/Range)
│   │   └── Program.fs           # Entry point (stdio transport)
│   └── LangLSP.Tests/           # Expecto + FsCheck tests
│       ├── DocumentSyncTests.fs
│       ├── DiagnosticsTests.fs
│       └── ProtocolTests.fs     # Position/Range conversion property tests
├── client/                       # VS Code extension (TypeScript)
│   ├── src/extension.ts         # Client activation and LanguageClient setup
│   └── package.json             # Extension manifest
└── docs/
    └── tutorial/                # Korean LSP tutorial
        ├── 01-lsp-concepts.md
        ├── 02-library-choice.md
        ├── 03-project-setup.md
        ├── 04-document-sync.md
        └── 05-diagnostics.md
```

### Pattern 1: Two-Process LSP Architecture
**What:** VS Code extension (TypeScript/JavaScript client) spawns an F# LSP server process, communicating via JSON-RPC over stdin/stdout.

**When to use:** Always for language server implementations. This is the standard LSP model.

**Example:**
```typescript
// client/src/extension.ts
// Source: https://code.visualstudio.com/api/language-extensions/language-server-extension-guide
import { LanguageClient, LanguageClientOptions, ServerOptions, TransportKind } from 'vscode-languageclient/node';

export function activate(context: ExtensionContext) {
  // Path to F# compiled LSP server
  let serverExecutable = context.asAbsolutePath(
    path.join('server', 'bin', 'LangLSP.Server')
  );

  let serverOptions: ServerOptions = {
    run: { command: serverExecutable },
    debug: { command: serverExecutable }
  };

  let clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'funlang' }]
  };

  client = new LanguageClient(
    'funlang-lsp',
    'FunLang Language Server',
    serverOptions,
    clientOptions
  );

  client.start();
}
```

### Pattern 2: Incremental Document Sync
**What:** Server advertises `TextDocumentSyncKind.Incremental` capability. Client sends full text on `didOpen`, then only delta changes on `didChange`.

**When to use:** Always. More efficient than Full sync, especially for large files. Handles multi-byte UTF-16 characters correctly.

**Example:**
```fsharp
// Server.fs
// Source: FsAutoComplete architecture pattern
open Ionide.LanguageServerProtocol.Server
open Ionide.LanguageServerProtocol.Types

let onInitialize (p: InitializeParams) : InitializeResult =
    {
        capabilities = {
            textDocumentSync = Some {
                openClose = Some true
                change = Some TextDocumentSyncKind.Incremental
                save = None
            }
            // ... other capabilities
        }
    }
```

### Pattern 3: Document State Management
**What:** Server maintains a mutable dictionary of open documents, updating on didOpen/didChange/didClose events.

**When to use:** Essential for tracking current document state without file I/O. Diagnostics should only be sent for open documents.

**Example:**
```fsharp
// DocumentSync.fs
// Source: LSP specification and FsAutoComplete pattern
module DocumentSync

open System.Collections.Concurrent
open Ionide.LanguageServerProtocol.Types

// Thread-safe document storage
let private documents = ConcurrentDictionary<string, string>()

let handleDidOpen (p: DidOpenTextDocumentParams) : unit =
    let uri = p.textDocument.uri
    let text = p.textDocument.text
    documents.[uri] <- text
    // Trigger diagnostics after storing

let handleDidChange (p: DidChangeTextDocumentParams) : unit =
    let uri = p.textDocument.uri
    match documents.TryGetValue(uri) with
    | true, currentText ->
        // Apply incremental changes
        let newText = applyContentChanges currentText p.contentChanges
        documents.[uri] <- newText
        // Trigger diagnostics after updating
    | false, _ ->
        // Document not tracked (error condition)
        ()

let handleDidClose (p: DidCloseTextDocumentParams) : unit =
    let uri = p.textDocument.uri
    documents.TryRemove(uri) |> ignore
    // Clear diagnostics for closed document

// Apply incremental text changes from LSP protocol
let private applyContentChanges (text: string) (changes: TextDocumentContentChangeEvent[]) : string =
    // For incremental sync: changes have range, rangeLength, text
    // Apply each change to the document
    // NOTE: Must handle UTF-16 code units correctly (LSP uses UTF-16 positions)
    changes |> Array.fold (fun txt change ->
        match change.range with
        | Some range ->
            // Incremental change: replace text at range
            replaceRange txt range change.text
        | None ->
            // Full document sync fallback
            change.text
    ) text
```

### Pattern 4: Span to LSP Position/Range Conversion
**What:** Convert FunLang's Span type (1-based line/column) to LSP Position (0-based) and Range types.

**When to use:** When sending diagnostics to VS Code. FunLang's Diagnostic.Span must be converted to LSP Range.

**Example:**
```fsharp
// Protocol.fs
open Ionide.LanguageServerProtocol.Types
open Ast  // FunLang's Span type

// LSP positions are 0-based; FunLang Span is 1-based
let spanToLspRange (span: Span) : Range =
    {
        start = {
            line = span.StartLine - 1      // Convert to 0-based
            character = span.StartColumn - 1
        }
        ``end`` = {
            line = span.EndLine - 1
            character = span.EndColumn - 1
        }
    }

// Convert FunLang Diagnostic to LSP Diagnostic
let diagnosticToLsp (diag: Diagnostic.Diagnostic) : Diagnostic =
    {
        range = spanToLspRange diag.PrimarySpan
        severity = Some DiagnosticSeverity.Error
        code = diag.Code |> Option.map (fun c -> c :> obj)
        source = Some "funlang"
        message = diag.Message
        relatedInformation =
            diag.SecondarySpans
            |> List.map (fun (span, label) -> {
                location = {
                    uri = sprintf "file://%s" span.FileName
                    range = spanToLspRange span
                }
                message = label
            })
            |> Some
        // ... additional fields
    }
```

### Pattern 5: Publish Diagnostics on Change
**What:** After updating document state, parse and type-check the file, then send diagnostics via `textDocument/publishDiagnostics` notification.

**When to use:** On every didOpen and didChange event. This provides real-time error feedback.

**Example:**
```fsharp
// Diagnostics.fs
open Ionide.LanguageServerProtocol.Server
open Ionide.LanguageServerProtocol.Types

let publishDiagnostics (lspServer: ILspServer) (uri: string) (text: string) : unit =
    try
        // Parse and type-check FunLang code
        let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(text)
        let ast = Parser.program Lexer.token lexbuf
        let result = TypeCheck.typecheck ast

        let diagnostics =
            match result with
            | Ok _ -> []  // No errors
            | Error typeError ->
                let diag = Diagnostic.typeErrorToDiagnostic typeError
                [Protocol.diagnosticToLsp diag]

        // Send diagnostics to client
        lspServer.TextDocumentPublishDiagnostics {
            uri = uri
            diagnostics = diagnostics |> Array.ofList
        }
    with
    | ex ->
        // Send syntax error diagnostic
        let diagnostic = {
            range = { start = { line = 0; character = 0 }; ``end`` = { line = 0; character = 0 } }
            severity = Some DiagnosticSeverity.Error
            message = sprintf "Syntax error: %s" ex.Message
            source = Some "funlang"
        }
        lspServer.TextDocumentPublishDiagnostics {
            uri = uri
            diagnostics = [| diagnostic |]
        }
```

### Pattern 6: VS Code Extension Activation
**What:** Register language in package.json with file extension associations. VS Code 1.74.0+ automatically activates extension when those files are opened.

**When to use:** Always. This is how VS Code knows to start your language server.

**Example:**
```json
// client/package.json
// Source: https://code.visualstudio.com/api/references/activation-events
{
  "name": "funlang",
  "contributes": {
    "languages": [{
      "id": "funlang",
      "aliases": ["FunLang", "funlang"],
      "extensions": [".fun"],
      "configuration": "./language-configuration.json"
    }]
  },
  "activationEvents": [],  // VS Code 1.74.0+: automatic activation for contributed languages
  "main": "./out/extension.js"
}
```

### Anti-Patterns to Avoid
- **Sending diagnostics for closed documents:** Only send diagnostics for documents in the `documents` dictionary. Clear diagnostics on `didClose`.
- **Using Full Sync instead of Incremental:** Full sync sends entire file on every keystroke. Use Incremental for efficiency.
- **Blocking the server thread:** LSP servers should be responsive. Use async/background processing for type checking if it's slow.
- **Ignoring UTF-16 encoding:** LSP positions are UTF-16 code units, not byte offsets or Unicode code points. Emojis (🍋) count as 2 units.
- **Manual JSON-RPC implementation:** Use Ionide.LanguageServerProtocol's built-in server infrastructure instead of hand-rolling message framing.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| LSP message framing | Custom stdin/stdout parser | Ionide.LanguageServerProtocol.Server | LSP uses Content-Length headers and JSON-RPC. Protocol has edge cases (partial reads, UTF-8 encoding, CRLF vs LF). Library handles all of this. |
| Position/Range calculations | Manual line/column tracking | Ionide.LanguageServerProtocol.Types | UTF-16 code unit handling is subtle. Multi-byte characters (emojis, CJK) break naive implementations. Library types enforce correct semantics. |
| Incremental text changes | String manipulation | Follow LSP TextDocumentContentChangeEvent spec | Applying delta changes requires correct range replacement with UTF-16 awareness. Off-by-one errors are common. |
| VS Code client protocol | Custom JSON-RPC client | vscode-languageclient npm package | Microsoft's official client handles initialization, capabilities negotiation, notifications, requests, and error recovery. 10+ years of production battle-testing. |
| Document state tracking | File I/O on every change | In-memory ConcurrentDictionary | LSP sends document content; reading from disk races with unsaved changes. Clients send content explicitly to avoid this. |

**Key insight:** LSP specification is 285 pages with 90 methods and 407 types. The protocol has subtle requirements (e.g., UTF-16 positions, incremental sync semantics, capabilities negotiation). Using battle-tested libraries (Ionide.LanguageServerProtocol, vscode-languageclient) avoids reimplementing years of edge case handling.

## Common Pitfalls

### Pitfall 1: UTF-16 Position Encoding
**What goes wrong:** Using byte offsets or Unicode code points instead of UTF-16 code units causes off-by-one errors with emojis, CJK characters, and other multi-byte characters.

**Why it happens:** LSP specification mandates UTF-16 encoding for positions (inherited from VS Code's internal representation). Most developers expect byte offsets or Unicode code points.

**How to avoid:**
- LSP Position.character is a UTF-16 code unit offset, not byte offset
- Emoji 🍋 at column 0 occupies code units 0-1; next character is at position 2
- Since LSP 3.17, can negotiate UTF-8 encoding via `general.positionEncodings` capability
- For FunLang: FsLexYacc's Position.Column is already 0-based, but verify it's counting correctly for multi-byte chars

**Warning signs:**
- Diagnostics appear at wrong column when file contains emojis or CJK text
- Range end position is off by 1 for multi-byte characters

### Pitfall 2: 0-Based vs 1-Based Indexing
**What goes wrong:** Mixing LSP's 0-based line/character with FunLang's 1-based Span causes diagnostics to point to wrong locations.

**Why it happens:** LSP uses 0-based (influenced by C/JavaScript arrays). FunLang/FsLexYacc uses 1-based (human-readable line numbers).

**How to avoid:**
- Always convert at the boundary: `lspLine = span.StartLine - 1`
- Write property-based tests with FsCheck to verify round-trip conversion
- Document clearly which functions expect 0-based vs 1-based

**Warning signs:**
- Red squiggles appear one line/column off from actual error
- First line of file (line 1 in editor) shows errors at line 0

### Pitfall 3: Incremental Sync Range Replacement
**What goes wrong:** Incorrectly applying incremental text changes corrupts document state, causing all subsequent diagnostics to be wrong.

**Why it happens:** Incremental changes specify a Range to replace. Computing string offsets from line/column positions is error-prone, especially with CRLF vs LF line endings.

**How to avoid:**
- Use Ionide.LanguageServerProtocol's types which handle this
- Or: build a rope/piece table data structure for efficient range replacement
- Test with files containing CRLF, LF, mixed line endings
- Test with multi-byte characters spanning change boundaries

**Warning signs:**
- Document state becomes corrupted after a few edits
- Diagnostics stop updating or show nonsensical errors
- Server works on fresh file open but breaks after editing

### Pitfall 4: Not Clearing Diagnostics on Close
**What goes wrong:** Closing a file in VS Code doesn't clear the red squiggles in the Problems panel.

**Why it happens:** Forgetting to send empty diagnostics array on `didClose`.

**How to avoid:**
```fsharp
let handleDidClose (p: DidCloseTextDocumentParams) : unit =
    lspServer.TextDocumentPublishDiagnostics {
        uri = p.textDocument.uri
        diagnostics = [||]  // Empty array clears diagnostics
    }
```

**Warning signs:**
- Problems panel shows errors for files that are no longer open
- Closing and reopening file duplicates errors

### Pitfall 5: Synchronous Type Checking Blocking Server
**What goes wrong:** Slow type checking (e.g., large files) blocks LSP server, making VS Code unresponsive.

**Why it happens:** Running type checker synchronously on every keystroke in the main LSP server thread.

**How to avoid:**
- Debounce diagnostics (wait 300ms after last edit before type checking)
- Run type checking in background thread/async task
- Send partial diagnostics (syntax errors immediately, type errors after delay)
- For Phase 1: FunLang type checking is fast enough for synchronous approach; optimize later if needed

**Warning signs:**
- VS Code freezes briefly on every keystroke
- Diagnostics lag behind typing
- Server becomes unresponsive

### Pitfall 6: File URI vs File Path Confusion
**What goes wrong:** Using file paths like `/home/user/file.fun` instead of URIs like `file:///home/user/file.fun` breaks LSP protocol.

**Why it happens:** LSP specification requires URIs, but file system APIs use paths.

**How to avoid:**
- Always use URIs in LSP protocol messages
- Convert to file path only when accessing file system
- Use Ionide.LanguageServerProtocol's URI helper functions

**Warning signs:**
- Diagnostics don't appear in VS Code
- "File not found" errors in LSP server logs
- Related information links don't work

## Code Examples

Verified patterns from official sources and FsAutoComplete:

### Minimal LSP Server Main Entry Point
```fsharp
// Program.fs
// Source: Ionide.LanguageServerProtocol pattern
module LangLSP.Server.Program

open System
open Ionide.LanguageServerProtocol.Server

[<EntryPoint>]
let main argv =
    // Create LSP server that reads from stdin and writes to stdout
    use input = Console.OpenStandardInput()
    use output = Console.OpenStandardOutput()

    // Initialize server with handlers
    let server = Server.createServerFromStreams input output

    server.onInitialize <- Some (fun p -> Server.onInitialize p)
    server.onTextDocumentDidOpen <- Some DocumentSync.handleDidOpen
    server.onTextDocumentDidChange <- Some DocumentSync.handleDidChange
    server.onTextDocumentDidClose <- Some DocumentSync.handleDidClose

    // Start server event loop
    server.WaitForShutdown()
```

### Property-Based Test for Position Conversion
```fsharp
// ProtocolTests.fs
// Source: Expecto + FsCheck pattern from FunLang project
module LangLSP.Tests.ProtocolTests

open Expecto
open Expecto.FsCheck
open FsCheck
open Ast
open Protocol

let config = { FsCheckConfig.defaultConfig with maxTest = 1000 }

[<Tests>]
let tests =
    testList "Protocol Position/Range Conversion" [
        testPropertyWithConfig config "Span to LSP Range preserves line order" <| fun () ->
            // Generate valid Span (1-based, StartLine <= EndLine)
            let genSpan = gen {
                let! startLine = Gen.choose(1, 100)
                let! startCol = Gen.choose(1, 80)
                let! endLine = Gen.choose(startLine, startLine + 10)
                let! endCol = Gen.choose(1, 80)
                return {
                    FileName = "test.fun"
                    StartLine = startLine
                    StartColumn = startCol
                    EndLine = endLine
                    EndColumn = endCol
                }
            }

            Prop.forAll (Arb.fromGen genSpan) (fun span ->
                let range = spanToLspRange span
                // LSP Range should maintain order and be 0-based
                range.start.line <= range.``end``.line
                && range.start.line = span.StartLine - 1
                && range.``end``.line = span.EndLine - 1
            )
    ]
```

### VS Code Extension Activation with LanguageClient
```typescript
// client/src/extension.ts
// Source: https://code.visualstudio.com/api/language-extensions/language-server-extension-guide
import * as path from 'path';
import { workspace, ExtensionContext } from 'vscode';
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions
} from 'vscode-languageclient/node';

let client: LanguageClient;

export function activate(context: ExtensionContext) {
  // Server executable path (platform-specific)
  const serverCommand = context.asAbsolutePath(
    path.join('server', 'bin', 'LangLSP.Server')
  );

  const serverOptions: ServerOptions = {
    command: serverCommand,
    args: []
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'funlang' }],
    synchronize: {
      fileEvents: workspace.createFileSystemWatcher('**/.fun')
    }
  };

  client = new LanguageClient(
    'funlangServer',
    'FunLang Language Server',
    serverOptions,
    clientOptions
  );

  client.start();
}

export function deactivate(): Thenable<void> | undefined {
  if (!client) {
    return undefined;
  }
  return client.stop();
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Full sync (TextDocumentSyncKind.Full) | Incremental sync (TextDocumentSyncKind.Incremental) | LSP 3.0 (2016) | Reduced bandwidth and CPU. Essential for large files and fast typing. |
| UTF-16 positions (mandatory) | Negotiable encoding (UTF-8, UTF-16, UTF-32) | LSP 3.17 (2021) | Allows servers to use native encoding. Opt-in via `general.positionEncodings` capability. UTF-16 still default for compatibility. |
| Manual activationEvents for onLanguage | Automatic activation for contributed languages | VS Code 1.74.0 (Nov 2022) | Simplified extension manifests. Empty activationEvents array for language servers. |
| fsprojects/fsharp-language-server | ionide/FsAutoComplete | 2020+ | FsAutoComplete became the standard. fsprojects/fsharp-language-server no longer maintained past .NET 6. |
| Ionide.LanguageServerProtocol 0.4.x | Ionide.LanguageServerProtocol 0.7.0 | March 2025 | Generate Client and Server interfaces from LSP spec. Breaking change requiring code updates. |
| Expecto.FsCheck (FsCheck 2) | Expecto.FsCheck (FsCheck 3) | 2023+ | FsCheck 2 no longer supported. Use FsCheck 3 for property-based tests. |

**Deprecated/outdated:**
- **fsprojects/fsharp-language-server:** Not maintained past .NET 6. Use Ionide.LanguageServerProtocol directly or reference FsAutoComplete.
- **OmniSharp.LanguageServerProtocol for F#:** C#-focused, heavier. Ionide.LanguageServerProtocol is preferred for F# projects.
- **Explicit onLanguage activation events:** VS Code 1.74.0+ automatically activates for contributed languages. Empty activationEvents array is modern practice.

## Open Questions

Things that couldn't be fully resolved:

1. **FsharpLspExample Repository Location**
   - What we know: Ionide.LanguageServerProtocol README mentions "FsharpLspExample (GitHub mirror)" as tutorial/example
   - What's unclear: Exact GitHub URL not found in search results. May be part of Ionide.LanguageServerProtocol repository or separate repo.
   - Recommendation: Check Ionide.LanguageServerProtocol repository's /examples or /samples folder. Alternatively, use FsAutoComplete source code as reference implementation (confirmed to use Ionide.LanguageServerProtocol).

2. **FsLexYacc Position UTF-16 Handling**
   - What we know: FsLexYacc's Position type has Line and Column fields. FunLang uses FsLexYacc for parsing.
   - What's unclear: Whether FsLexYacc's Column counts bytes, Unicode code points, or UTF-16 code units. LSP requires UTF-16.
   - Recommendation: Write a test with emoji/CJK characters to verify. If FsLexYacc uses byte/codepoint counting, implement UTF-16 conversion layer. Property-based tests (FsCheck) should catch discrepancies.

3. **Ionide.LanguageServerProtocol 0.7.0 Breaking Changes**
   - What we know: Version 0.7.0 (March 2025) has breaking change "Generate Client and Server interfaces from LSP spec"
   - What's unclear: Specific API changes from 0.4.x to 0.7.0. Migration guide not found in search results.
   - Recommendation: Start with 0.7.0 (latest). Reference FsAutoComplete for current usage patterns. Avoid 0.4.x examples unless confirmed compatible.

4. **Tutorial Scope: Korean vs English**
   - What we know: Requirements specify Korean tutorial (TUT-01 to TUT-05). Korean LSP tutorials are rare.
   - What's unclear: Should tutorial include English translation for broader reach? What level of F# knowledge to assume?
   - Recommendation: Focus on Korean as specified. Assume reader knows F# basics (from FunLang tutorial) but not LSP. Provide code examples that work standalone.

## Sources

### Primary (HIGH confidence)
- [Language Server Extension Guide | VS Code API](https://code.visualstudio.com/api/language-extensions/language-server-extension-guide) - Official VS Code LSP guide with TypeScript client examples
- [Activation Events | VS Code API](https://code.visualstudio.com/api/references/activation-events) - VS Code 1.74.0+ automatic activation behavior
- [Ionide.LanguageServerProtocol GitHub](https://github.com/ionide/LanguageServerProtocol) - Official F# LSP library repository, version 0.7.0
- [FsAutoComplete GitHub](https://github.com/ionide/FsAutoComplete) - Production F# language server using Ionide.LanguageServerProtocol, architecture reference
- [LSP Specification 3.17](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/) - Official LSP protocol specification
- FunLang codebase - Diagnostic.fs, Ast.fs (Span type, typeErrorToDiagnostic function)

### Secondary (MEDIUM confidence)
- [LSP: the good, the bad, and the ugly](https://www.michaelpj.com/blog/2024/09/03/lsp-good-bad-ugly.html) - Detailed analysis of LSP pitfalls and design issues (Sept 2024)
- [Expecto GitHub](https://github.com/haf/expecto) - F# testing framework documentation, FsCheck integration
- [vscode-languageclient npm](https://www.npmjs.com/package/vscode-languageclient) - Official VS Code LSP client library for TypeScript
- [Ionide vscode-fsharp package.json](https://github.com/ionide/ionide-vscode-fsharp/blob/main/release/package.json) - Real-world VS Code extension example

### Tertiary (LOW confidence)
- [F# LSP | F# Compiler Guide](https://fsharp.github.io/fsharp-compiler-docs/lsp.html) - Official F# compiler LSP documentation (general context)
- WebSearch results for UTF-16 encoding issues - Multiple GitHub issues confirming UTF-16 as common pitfall
- WebSearch results for incremental sync - LSP 3.14+ documentation on TextDocumentSyncKind

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Ionide.LanguageServerProtocol 0.7.0 confirmed from NuGet, FsAutoComplete proves battle-tested usage
- Architecture: HIGH - Official VS Code guide and FsAutoComplete provide concrete patterns
- Pitfalls: HIGH - LSP spec, Michael PJ's blog post, and multiple GitHub issues document UTF-16, position encoding, and sync issues

**Research date:** 2026-02-04
**Valid until:** 2026-04-04 (60 days - LSP spec is stable, library versions may update but patterns remain)
