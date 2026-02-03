# Architecture Patterns for LSP Servers

**Domain:** Language Server Protocol (LSP) Implementation
**Researched:** 2026-02-03
**Confidence:** HIGH

## Recommended Architecture for FunLang LSP Server

LSP servers follow a **layered client-server architecture** with strict separation between protocol handling and language analysis. Based on research of production implementations (FsAutoComplete, Deno LSP, TypeScript Language Server) and official specifications, the recommended architecture is:

```
┌─────────────────────────────────────────────────────────────┐
│                    Editor / IDE (Client)                     │
│                     (VS Code, Vim, etc.)                     │
└──────────────────────┬──────────────────────────────────────┘
                       │ JSON-RPC over stdio/IPC
                       │
┌──────────────────────┴──────────────────────────────────────┐
│               LSP Server (Separate Process)                  │
│  ┌────────────────────────────────────────────────────────┐ │
│  │         Protocol Layer (JSON-RPC Handler)              │ │
│  │  - Connection/Transport (stdio, sockets, IPC)          │ │
│  │  - Initialization/Shutdown lifecycle                   │ │
│  │  - Capability negotiation                              │ │
│  └────────────┬───────────────────────────────────────────┘ │
│               │                                              │
│  ┌────────────┴───────────────────────────────────────────┐ │
│  │          Document Manager (State Sync)                 │ │
│  │  - Text document synchronization (full/incremental)    │ │
│  │  - Workspace folder tracking                           │ │
│  │  - Document version management                         │ │
│  │  - Settings/configuration cache                        │ │
│  └────────────┬───────────────────────────────────────────┘ │
│               │                                              │
│  ┌────────────┴───────────────────────────────────────────┐ │
│  │        Language Services (Feature Providers)           │ │
│  │  ┌──────────────────────────────────────────────────┐  │ │
│  │  │ TextDocumentService                              │  │ │
│  │  │  - Diagnostics (errors/warnings)                 │  │ │
│  │  │  - Completion (autocomplete)                     │  │ │
│  │  │  - Hover (type info)                             │  │ │
│  │  │  - Definition/References                         │  │ │
│  │  │  - Code Actions (refactoring)                    │  │ │
│  │  │  - Formatting                                    │  │ │
│  │  └──────────────────────────────────────────────────┘  │ │
│  │  ┌──────────────────────────────────────────────────┐  │ │
│  │  │ WorkspaceService                                 │  │ │
│  │  │  - Workspace symbols                             │  │ │
│  │  │  - File operations (rename, delete)              │  │ │
│  │  └──────────────────────────────────────────────────┘  │ │
│  └────────────┬───────────────────────────────────────────┘ │
│               │                                              │
│  ┌────────────┴───────────────────────────────────────────┐ │
│  │     Language Analysis Engine (Compiler Services)       │ │
│  │  - Lexer/Parser (AST generation)                       │ │
│  │  - Type Checker/Inference                              │ │
│  │  - Symbol Table/Scope Analysis                         │ │
│  │  - Semantic Analysis                                   │ │
│  │  - Cache Layer (parsed ASTs, type info, symbols)      │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### For FunLang Specifically

**Existing Compiler Components (Reuse):**
- Lexer.fs → Tokenization
- Parser.fs → AST generation
- Ast.fs → AST types
- Type.fs → Type definitions
- Infer.fs → Type inference
- TypeCheck.fs → Type checking
- Unify.fs → Type unification

**New LSP Components (To Build):**
- Protocol layer (JSON-RPC transport)
- Document manager (text sync, workspace state)
- Language services (feature providers)
- Cache layer (performance optimization)
- Integration layer (compiler components → LSP)

## Component Boundaries

| Component | Responsibility | Communicates With | Interface |
|-----------|---------------|-------------------|-----------|
| **Protocol Layer** | JSON-RPC transport, LSP message routing, lifecycle management | Editor (stdin/stdout), Document Manager | `Connection.listen()`, message handlers |
| **Document Manager** | Track open documents, sync text changes (incremental), maintain document versions | Protocol Layer, Language Services | `onDidOpen`, `onDidChange`, `onDidClose` |
| **Language Services** | Implement LSP feature providers, translate LSP requests to compiler API calls | Document Manager, Analysis Engine | `TextDocumentService`, `WorkspaceService` interfaces |
| **Analysis Engine** | Parse source code, build AST, type inference/checking, symbol resolution, caching | Language Services | Direct function calls to compiler modules |
| **Cache Layer** | Store parsed ASTs, type info, symbols; invalidate on file changes | Analysis Engine, Document Manager | In-memory Map/Dictionary |

### Detailed Component Interactions

**Request Flow Example (Hover for Type Info):**

1. Editor sends `textDocument/hover` request via JSON-RPC
2. Protocol Layer deserializes message → routes to Hover handler
3. Hover handler queries Document Manager for current document state
4. Document Manager returns cached `TextDocument` or retrieves from workspace
5. Language Service calls Analysis Engine: `getTypeAtPosition(document, position)`
6. Analysis Engine:
   - Checks cache for parsed AST
   - If miss: Lexer → Parser → AST
   - Runs type inference on AST
   - Queries symbol table for type at position
   - Caches result
7. Language Service formats type info as LSP `Hover` response
8. Protocol Layer serializes response → sends to editor

**Change Notification Flow (Diagnostics):**

1. User types in editor → editor sends `textDocument/didChange` notification
2. Protocol Layer routes to Document Manager
3. Document Manager applies incremental text changes → updates document version
4. Document Manager invalidates affected cache entries
5. Document Manager triggers diagnostics provider
6. Diagnostics provider calls Analysis Engine: `getErrors(document)`
7. Analysis Engine: Lexer → Parser → Type Checker → collect errors
8. Diagnostics provider sends `textDocument/publishDiagnostics` notification
9. Protocol Layer sends diagnostics to editor
10. Editor displays red squiggles

## Data Flow

### Initialization Sequence

```
1. Editor starts LSP server process (stdio)
2. Editor → Server: initialize request
   {
     "processId": 1234,
     "rootUri": "file:///path/to/workspace",
     "capabilities": { ... }
   }
3. Server initializes internal state (workspace, document manager, cache)
4. Server → Editor: initialize response
   {
     "capabilities": {
       "textDocumentSync": 2, // Incremental
       "hoverProvider": true,
       "completionProvider": { ... },
       "definitionProvider": true,
       "diagnosticProvider": true,
       ...
     }
   }
5. Editor → Server: initialized notification
6. Server can now send dynamic capability registrations
7. Server ready for requests
```

### Text Synchronization (Incremental)

**Full Sync (Initial Open):**
```
Editor → Server: textDocument/didOpen
{
  "uri": "file:///workspace/main.fun",
  "languageId": "funlang",
  "version": 1,
  "text": "let x = 42\nlet y = x + 1"
}
→ Document Manager stores full text
→ Triggers diagnostics
```

**Incremental Sync (Each Edit):**
```
Editor → Server: textDocument/didChange
{
  "uri": "file:///workspace/main.fun",
  "version": 2,
  "contentChanges": [
    {
      "range": { "start": {0, 8}, "end": {0, 10} },
      "text": "100"
    }
  ]
}
→ Document Manager applies delta: "let x = 42" → "let x = 100"
→ Cache invalidation (only affected AST nodes)
→ Re-run diagnostics
```

### Capability Negotiation

Server declares capabilities during initialization:

| Capability | What Server Implements | When to Use |
|------------|----------------------|-------------|
| `textDocumentSync: Full (1)` | Server wants full document on every change | Simple, but inefficient for large files |
| `textDocumentSync: Incremental (2)` | Server wants only deltas | Efficient, requires careful state management |
| `hoverProvider: true` | Server handles `textDocument/hover` | Type information on hover |
| `completionProvider` | Server handles `textDocument/completion` | Autocomplete |
| `definitionProvider: true` | Server handles `textDocument/definition` | Go-to-definition |
| `diagnosticProvider: true` | Server sends `publishDiagnostics` | Error/warning reporting |

**For FunLang:** Use incremental sync for efficiency, implement diagnostics (type errors), hover (type info), completion (keywords/symbols), and definition first.

## Patterns to Follow

### Pattern 1: Layered Architecture
**What:** Strict separation between protocol, state management, language services, and analysis engine.

**When:** Always. This is the LSP reference architecture.

**Why:** Isolates concerns, enables reuse of compiler components, simplifies testing.

**Example (F#):**
```fsharp
// Protocol Layer - handles JSON-RPC
module ProtocolLayer =
    let handleRequest (request: LspRequest) =
        match request.method with
        | "textDocument/hover" -> HoverService.handle request.params
        | "textDocument/completion" -> CompletionService.handle request.params
        | _ -> NotImplemented

// Language Service - translates LSP → Compiler API
module HoverService =
    let handle (params: HoverParams) =
        let doc = DocumentManager.get params.textDocument.uri
        let pos = params.position
        let typeInfo = AnalysisEngine.getTypeAt doc pos
        { contents = typeInfo |> formatMarkdown }

// Analysis Engine - uses existing compiler
module AnalysisEngine =
    let getTypeAt (doc: TextDocument) (pos: Position) =
        let ast = parse doc.text // Reuse Parser.fs
        let types = TypeCheck.infer ast // Reuse Infer.fs
        findTypeAtPosition types pos
```

### Pattern 2: Document Manager as Source of Truth
**What:** Centralized component that owns document state, handles synchronization, and manages versions.

**When:** Required for all LSP servers.

**Why:** Prevents race conditions, ensures consistency between editor and server state, simplifies cache invalidation.

**Example (F#):**
```fsharp
type DocumentManager() =
    let documents = System.Collections.Concurrent.ConcurrentDictionary<Uri, TextDocument>()

    member this.OnDidOpen(params: DidOpenParams) =
        let doc = TextDocument.create params.textDocument.uri params.textDocument.text
        documents.[params.textDocument.uri] <- doc
        DiagnosticsService.publish doc // Trigger diagnostics

    member this.OnDidChange(params: DidChangeParams) =
        match documents.TryGetValue(params.textDocument.uri) with
        | true, doc ->
            let updatedDoc = TextDocument.applyChanges doc params.contentChanges
            documents.[params.textDocument.uri] <- updatedDoc
            Cache.invalidate params.textDocument.uri // Invalidate cache
            DiagnosticsService.publish updatedDoc
        | false, _ -> ()

    member this.Get(uri: Uri) =
        documents.TryGetValue(uri)
```

### Pattern 3: Incremental Text Synchronization
**What:** Editor sends only text deltas (ranges + replacement text) instead of full document on every change.

**When:** For any file > 100 lines. Critical for performance.

**Why:** Reduces bandwidth, enables partial re-parsing, makes cache invalidation more precise.

**Implementation Notes:**
- Declare `textDocumentSync: 2` (Incremental) in server capabilities
- Implement three handlers: `onDidOpenTextDocument`, `onDidChangeTextDocument`, `onDidCloseTextDocument`
- Apply content changes sequentially (order matters)
- Track document version to detect out-of-order messages

### Pattern 4: Lazy Computation with Caching
**What:** Cache expensive computations (parsed AST, type info, symbol table) and invalidate only affected entries on changes.

**When:** Always. LSP servers are latency-sensitive (< 100ms for hover, < 1s for completion).

**Why:** Parsing and type checking are expensive. Deno LSP reduced hover latency from 8s → <1s with caching.

**Example (F#):**
```fsharp
type Cache() =
    let astCache = System.Collections.Concurrent.ConcurrentDictionary<Uri, Ast.Expr>()
    let typeCache = System.Collections.Concurrent.ConcurrentDictionary<Uri, Type.Scheme>()

    member this.GetOrComputeAst(uri: Uri, doc: TextDocument) =
        astCache.GetOrAdd(uri, fun _ ->
            Lexer.tokenize doc.text
            |> Parser.parse
        )

    member this.InvalidateUri(uri: Uri) =
        astCache.TryRemove(uri) |> ignore
        typeCache.TryRemove(uri) |> ignore

    member this.GetOrComputeTypes(uri: Uri, ast: Ast.Expr) =
        typeCache.GetOrAdd(uri, fun _ ->
            TypeCheck.infer ast
        )
```

**Cache Invalidation Strategy:**
- **On `didChange`:** Invalidate only the changed file's cache
- **On workspace changes:** Invalidate all files (or use dependency graph if available)
- **Never:** Use stale cache for diagnostics (users expect accurate errors)

### Pattern 5: Asynchronous Request Handling
**What:** Process LSP requests asynchronously to avoid blocking the server.

**When:** For slow operations (completion with large symbol tables, workspace-wide find references).

**Why:** LSP spec allows concurrent requests. Blocking the server makes the editor unresponsive.

**Implementation:**
- Use async/await in F# (`async { ... }`)
- Run long operations in background tasks
- Support request cancellation (LSP `$/cancelRequest`)
- Return progress notifications for multi-second operations

### Pattern 6: Separate Processes for Isolation
**What:** Run language server in a separate OS process from the editor.

**When:** Always (LSP design principle).

**Why:**
1. **Language independence:** Server can be written in any language (F# for FunLang)
2. **Fault isolation:** Server crash doesn't crash editor
3. **Performance isolation:** Heavy CPU/memory usage doesn't freeze editor UI
4. **Resource limits:** OS can kill runaway server processes

**Communication:** JSON-RPC over stdio (standard input/output) or sockets.

## Anti-Patterns to Avoid

### Anti-Pattern 1: Tight Coupling Between Protocol and Compiler
**What:** Mixing JSON-RPC handling code with language analysis logic.

**Why bad:** Makes compiler components non-reusable (can't use in REPL, CLI, or other tools), hard to test, violates separation of concerns.

**Instead:** Use a layered architecture. Protocol Layer → Language Services → Analysis Engine (pure compiler logic).

**Detection Warning Sign:** `Lexer.fs` imports `Newtonsoft.Json` or references LSP types.

### Anti-Pattern 2: Full Document Synchronization for Large Files
**What:** Sending entire file content on every keystroke.

**Why bad:** For a 10,000-line file, that's ~300KB of JSON per keystroke. Causes latency, wastes bandwidth.

**Instead:** Use incremental sync (`textDocumentSync: 2`). Editor sends only changed ranges.

**Example:** User types "a" on line 100. Incremental sends: `{ range: {100,5-100,5}, text: "a" }` (50 bytes) vs full doc (300KB).

### Anti-Pattern 3: Synchronous Blocking Operations
**What:** Blocking the server thread while parsing a 50,000-line file.

**Why bad:** Server can't respond to other requests (hover, cancel) while blocked. Editor appears frozen.

**Instead:** Use async operations, run heavy work in background, support cancellation tokens.

**Detection:** Server takes >2 seconds to respond to any request.

### Anti-Pattern 4: No Caching (Re-parsing on Every Request)
**What:** Running Lexer → Parser → Type Checker for every hover request.

**Why bad:** If parsing takes 500ms, hovering becomes unusable. Deno LSP had this problem (8s hover latency).

**Instead:** Cache parsed AST and type info. Invalidate only on document changes.

**Trade-off:** Memory for speed. Acceptable for LSP servers (typically < 100 open files).

### Anti-Pattern 5: Ignoring Document Versions
**What:** Not tracking document version numbers sent by editor.

**Why bad:** Out-of-order message delivery causes state desync. Server might apply changes to wrong document version.

**Instead:** Check version on every `didChange` notification. Reject out-of-order changes or reset to known-good state.

**LSP Spec:** Each `didChange` includes `version: number`. Must be monotonically increasing.

### Anti-Pattern 6: Returning Stale Diagnostics
**What:** Using cached type errors from 5 edits ago.

**Why bad:** User fixes error, but red squiggle remains. Confusing and frustrating.

**Instead:** Always re-run diagnostics on document change (or debounce for 300ms). Never send stale errors.

**Why This Matters:** Diagnostics are the most visible LSP feature. Staleness destroys trust.

### Anti-Pattern 7: Incomplete Initialization Handshake
**What:** Accepting requests before `initialize` completes, or ignoring `shutdown`/`exit` sequence.

**Why bad:** Violates LSP spec. Server might not have workspace root URI, capabilities might not match.

**Instead:** Follow lifecycle strictly:
1. Editor → `initialize` → Server responds with capabilities
2. Editor → `initialized` notification
3. Server now ready for requests
4. Editor → `shutdown` → Server stops accepting requests
5. Editor → `exit` → Server terminates (code 0 if shutdown received, 1 otherwise)

**Detection:** Server crashes on startup or doesn't shut down cleanly.

### Anti-Pattern 8: Over-Specification (Trying to Support All LSP Features)
**What:** Implementing all 90+ LSP methods immediately.

**Why bad:** LSP has 285 pages of spec. Trying to do everything delays MVP and increases bugs.

**Instead:** Implement features incrementally based on user value:
1. **MVP:** Diagnostics, hover, text sync
2. **Phase 2:** Completion, go-to-definition
3. **Phase 3:** Formatting, code actions
4. **Later:** Workspace symbols, semantic tokens, inlay hints

**LSP Design:** Capability negotiation allows partial implementations. Editor gracefully degrades if server lacks features.

## Build Order Implications

Based on dependencies and user value, recommended implementation order:

### Phase 1: Foundation (Weeks 1-2)
**Goal:** Basic LSP server that connects to editor.

**Components:**
1. **Protocol Layer:** JSON-RPC transport (stdio), connection lifecycle (initialize/shutdown)
2. **Document Manager:** Text sync (full only, defer incremental), open/close handlers
3. **Basic Diagnostics:** Reuse existing TypeCheck.fs to report type errors

**Deliverable:** Editor shows type errors in real-time.

**Dependencies:**
- Existing: Lexer.fs, Parser.fs, TypeCheck.fs
- New: LSP protocol library (Ionide.LanguageServerProtocol or vscode-languageserver-node port)

**Why This Order:** Diagnostics are highest-value feature (immediate feedback on errors). Simplest to implement (one-way notification, no complex responses).

### Phase 2: Navigation (Weeks 3-4)
**Goal:** Users can explore code via hover and go-to-definition.

**Components:**
1. **Hover Provider:** Show type information on hover
2. **Definition Provider:** Jump to where symbol is defined
3. **Symbol Table:** Build index of defined symbols (functions, variables)

**Deliverable:** Hovering over `x` shows `int`, clicking "Go to Definition" jumps to `let x = 42`.

**Dependencies:**
- Phase 1 foundation
- Existing: Type.fs, Infer.fs (type inference results)
- New: Position → AST node mapping, symbol resolution logic

**Why This Order:** Builds on type information from diagnostics. Enables code exploration (critical DX feature).

### Phase 3: Code Completion (Weeks 5-6)
**Goal:** Autocomplete suggestions for keywords, symbols, and functions.

**Components:**
1. **Completion Provider:** Generate completion items based on context
2. **Scope Analysis:** Determine which symbols are in scope at cursor position
3. **Incremental Sync:** Switch from full to incremental text sync (performance)

**Deliverable:** Typing `x.` shows available methods/fields. Typing `le` suggests `let` keyword.

**Dependencies:**
- Phase 2 (symbol table, scope analysis)
- Existing: Ast.fs (AST structure for context analysis)
- New: Incremental text diff logic, context-aware completion

**Why This Order:** Requires robust symbol table and type info. Incremental sync important now (completion triggered frequently).

### Phase 4: Performance & Polish (Weeks 7-8)
**Goal:** Sub-100ms latency for common operations.

**Components:**
1. **Cache Layer:** Cache parsed AST, type info, symbol table
2. **Smart Invalidation:** Invalidate only affected files on changes
3. **Async Operations:** Run slow operations (workspace symbols) in background

**Deliverable:** Hover/completion feel instant even in large files.

**Dependencies:**
- Phase 1-3 (all features implemented)
- New: Caching infrastructure, performance benchmarks

**Why This Order:** Premature optimization is harmful. Build features first, then optimize based on real profiling.

### Phase 5: Advanced Features (Weeks 9+)
**Goal:** Professional-grade IDE experience.

**Components:**
1. **Code Actions:** Quick fixes, refactorings (e.g., rename variable)
2. **Formatting:** Auto-format code on save
3. **Semantic Tokens:** Syntax highlighting based on semantic info (types vs values)
4. **Workspace Symbols:** Search all symbols in project

**Deliverable:** Full-featured LSP server competitive with mature language servers.

**Dependencies:**
- Phase 1-4 (solid foundation, good performance)
- Existing: Format.fs (if available)
- New: Refactoring logic, workspace indexing

**Why Last:** Nice-to-have features. Users can be productive without them.

## Scalability Considerations

| Concern | Small Projects (<100 files) | Medium Projects (100-1K files) | Large Projects (1K-10K+ files) |
|---------|---------------------------|------------------------------|-------------------------------|
| **Text Sync** | Full sync acceptable | Incremental sync recommended | Incremental sync required |
| **Caching** | Optional (re-parse is fast) | Cache parsed AST | Cache AST + types + symbols |
| **Workspace Indexing** | Index on-demand | Index on startup | Background indexing, incremental updates |
| **Diagnostics** | Eager (on every change) | Debounced (300ms delay) | Debounced + incremental (only changed file) |
| **Memory** | ~10MB per open file | ~100MB total | GC tuning, memory limits (e.g., 2GB max) |
| **Completion** | Search all symbols | Search visible + imported | Pre-filtered index, async search |
| **Go-to-Definition** | Linear search AST | Symbol table lookup | Persistent index (file-based) |

**FunLang Scaling Strategy:**
- **MVP (Phase 1-2):** Optimize for small projects (<100 files). Simple implementation, no complex caching.
- **Phase 3-4:** Add caching and incremental sync for medium projects.
- **Phase 5+:** If FunLang gains traction, implement workspace indexing and persistent caching for large codebases.

**Do Not Prematurely Optimize For:**
- Multi-million line codebases (FunLang is new language, won't have these initially)
- Distributed file systems (local files only for MVP)
- Multi-language workspaces (FunLang only)

## F#-Specific Integration Patterns

Since FunLang already has compiler components in F#, follow FsAutoComplete's proven pattern:

### Pattern: FSharp.Compiler.Service Integration
**FsAutoComplete Architecture:**
1. **Ionide.LanguageServerProtocol:** F# library for LSP protocol (JSON-RPC, types)
2. **FSharp.Compiler.Service:** F# compiler API (parsing, type checking)
3. **Ionide.ProjInfo:** Project/solution management (`.fsproj` files)
4. **Translation Layer:** Convert LSP types ↔ FCS types

**For FunLang:**
1. Use `Ionide.LanguageServerProtocol` for LSP protocol handling (don't reinvent JSON-RPC)
2. Reuse existing compiler modules (Lexer.fs, Parser.fs, Infer.fs)
3. Build thin translation layer: LSP Position → FunLang AST node, FunLang Type → LSP Hover markdown

**Example Integration:**
```fsharp
// Use Ionide LSP library
open Ionide.LanguageServerProtocol.Server
open Ionide.LanguageServerProtocol.Types

// Reuse FunLang compiler
open FunLang.Lexer
open FunLang.Parser
open FunLang.TypeCheck

let handleHover (hoverParams: HoverParams) : AsyncLspResult<Hover option> =
    async {
        let uri = hoverParams.TextDocument.Uri
        let pos = hoverParams.Position

        // Get document from manager
        let! doc = DocumentManager.getDocument uri

        // Reuse FunLang compiler
        let tokens = Lexer.tokenize doc.Text
        let ast = Parser.parse tokens
        let typeEnv = TypeCheck.infer ast

        // Find type at position
        match findTypeAtPosition typeEnv pos with
        | Some ty ->
            let markdown = sprintf "```funlang\n%s\n```" (Type.toString ty)
            return success (Some { Contents = MarkedString markdown })
        | None ->
            return success None
    }
```

### Pattern: Type Translation Layer
LSP uses its own types (Position, Range, Location). FunLang compiler uses its own (Ast.Pos, etc.).

**Build bidirectional converters:**
```fsharp
module LspConvert =
    // LSP Position → FunLang position
    let toLspPosition (funPos: Ast.Pos) : Position =
        { Line = funPos.Line - 1; Character = funPos.Column - 1 } // LSP is 0-based

    let fromLspPosition (lspPos: Position) : Ast.Pos =
        { Line = lspPos.Line + 1; Column = lspPos.Character + 1 } // FunLang is 1-based

    // FunLang type → LSP hover markdown
    let typeToMarkdown (ty: Type.Scheme) : string =
        sprintf "```funlang\n%s\n```" (Type.toString ty)

    // FunLang error → LSP diagnostic
    let errorToDiagnostic (err: TypeCheck.Error) : Diagnostic =
        {
            Range = { Start = toLspPosition err.StartPos; End = toLspPosition err.EndPos }
            Severity = Some DiagnosticSeverity.Error
            Code = None
            Source = Some "funlang"
            Message = err.Message
        }
```

## Sources

### Official Documentation (HIGH confidence)
- [Language Server Protocol Specification 3.17](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/)
- [Language Server Protocol Official Site](https://microsoft.github.io/language-server-protocol/)
- [VS Code Language Server Extension Guide](https://code.visualstudio.com/api/language-extensions/language-server-extension-guide)
- [Microsoft Learn - Language Server Protocol Overview](https://learn.microsoft.com/en-us/visualstudio/extensibility/language-server-protocol?view=vs-2022)

### Production Implementations (HIGH confidence)
- [FsAutoComplete - F# LSP Server](https://github.com/ionide/FsAutoComplete)
- [Ionide LanguageServerProtocol Library](https://github.com/ionide/LanguageServerProtocol)
- [AWS Language Servers Architecture](https://github.com/aws/language-servers/blob/main/ARCHITECTURE.md)
- [Deno LSP Optimization](https://deno.com/blog/optimizing-our-lsp)

### Implementation Guides (MEDIUM-HIGH confidence)
- [Ballerina Practical Guide for LSP](https://medium.com/ballerina-techblog/practical-guide-for-the-language-server-protocol-3091a122b750)
- [Toptal LSP Tutorial](https://www.toptal.com/javascript/language-server-protocol-tutorial)
- [Building a Language Server with Go](https://tamerlan.dev/how-to-build-a-language-server-with-go/)
- [LSP: The Good, The Bad, and The Ugly](https://www.michaelpj.com/blog/2024/09/03/lsp-good-bad-ugly.html)

### Academic Research (MEDIUM confidence)
- [Implementation Practices in Language Server Protocols (2022 Paper)](https://peldszus.com/wp-content/uploads/2022/08/2022-models-lspstudy.pdf)

### Community Resources (MEDIUM confidence)
- [F# Compiler LSP Documentation](https://fsharp.github.io/fsharp-compiler-docs/lsp.html)
- [Understanding LSP by Malintha Ranasinghe](https://medium.com/@malintha1996/understanding-the-language-server-protocol-5c0ba3ac83d2)
