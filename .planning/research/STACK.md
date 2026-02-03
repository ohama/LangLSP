# Technology Stack

**Project:** FunLang LSP Server
**Researched:** 2026-02-03
**Overall Confidence:** HIGH

## Recommended Stack

### Core LSP Framework
| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| Ionide.LanguageServerProtocol | 0.7.0 | LSP protocol implementation | F#-native, actively maintained (released March 2025), used by FsAutoComplete, supports LSP 3.17 spec, lightweight and functional | HIGH |
| .NET SDK | 10.0.x | Runtime platform | Latest LTS (supports F# 10), long-term support until Nov 2028, FunLang already implemented in F# | HIGH |
| F# | 10.0 | Implementation language | Ships with .NET 10, compatible with existing FunLang compiler components | HIGH |

### F# Compiler Integration
| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| FSharp.Compiler.Service | 43.10.102 | F# compiler API | Latest stable (Jan 2026), provides type checking/analysis patterns directly applicable to LSP features, 3.6M+ downloads, actively maintained | HIGH |

### JSON-RPC Transport
| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| StreamJsonRpc | >= 2.16.36 | JSON-RPC 2.0 over stdio | Required by Ionide.LanguageServerProtocol, Microsoft-maintained, supports stdio transport (LSP standard), handles request cancellation | HIGH |
| Newtonsoft.Json | >= 13.0.1 | JSON serialization | Required by Ionide.LanguageServerProtocol, ubiquitous in .NET ecosystem, proven compatibility with LSP types | MEDIUM |

### VS Code Extension (Client)
| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| vscode-languageclient | Latest (10.x+) | VS Code LSP client | Official Microsoft LSP client for VS Code, handles stdio communication with server, supports LSP 3.17+ | HIGH |
| TypeScript/JavaScript | - | Extension implementation | VS Code extension API requirement, industry standard for VS Code extensions | HIGH |
| Node.js | 22.13.14+ | Extension runtime | Required by vscode-languageclient 10.x, modern LTS version | HIGH |

### Testing Framework
| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| Expecto | 10.2.3+ | Unit testing | F#-native, parallel and async by default, functional API, 2.6M+ downloads, composable test values | HIGH |
| Expecto.FsCheck | 10.2.3+ | Property-based testing | Property-based testing for LSP edge cases, integrates with Expecto | MEDIUM |

### Development Tools
| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| dotnet CLI | (included with SDK) | Build orchestration | Standard .NET tooling, no need for FAKE build system for this project size | HIGH |
| NuGet | (built-in) | Dependency management | Standard .NET package manager, simpler than Paket for this use case | HIGH |

## Alternatives Considered

| Category | Recommended | Alternative | Why Not | Confidence |
|----------|-------------|-------------|---------|------------|
| LSP Framework | Ionide.LanguageServerProtocol | OmniSharp.Extensions.LanguageServer | C#-focused with DI framework overhead, heavier abstraction, less idiomatic for F# | HIGH |
| LSP Framework | Ionide.LanguageServerProtocol | Build from scratch with LSP spec | Significant effort, spec compliance risk, existing F# implementation is mature | HIGH |
| JSON Serializer | Newtonsoft.Json | System.Text.Json | Ionide.LanguageServerProtocol requires Newtonsoft.Json; System.Text.Json is faster but not compatible with current LSP stack | MEDIUM |
| LSP Language | F# | TypeScript (vscode-languageserver-node) | FunLang compiler already in F#, want to reuse existing components, tutorial focus on F# | HIGH |
| Build Tool | dotnet CLI | FAKE (F# Make) | FAKE adds complexity without benefit for single-project LSP server, dotnet CLI sufficient | HIGH |
| Package Manager | NuGet | Paket | Paket's benefits (transitive deps, git refs, .fsx support) not needed for this project, standard NuGet simpler | MEDIUM |
| Testing | Expecto | xUnit/NUnit | Expecto more idiomatic for F#, async-first design matches LSP async nature | MEDIUM |
| Transport Protocol | JSON-RPC (stdio) | MessagePack/Protobuf | LSP specification requires JSON-RPC, stdio is standard for VS Code, performance adequate for LSP | HIGH |
| .NET Version | .NET 10 LTS | .NET 9 | .NET 10 has LTS until 2028 vs .NET 9 STS until 2026, future-proof choice | HIGH |

## What NOT to Use

### Avoid: System.Text.Json (for now)
**Why:** While System.Text.Json is faster (3.7ms vs 7.6ms for 10K objects, 50% less memory), Ionide.LanguageServerProtocol v0.7.0 has a hard dependency on Newtonsoft.Json >= 13.0.1. Switching would require forking or contributing to Ionide.LanguageServerProtocol.

**Future consideration:** Monitor Ionide.LanguageServerProtocol for System.Text.Json support in future releases.

### Avoid: OmniSharp.Extensions.LanguageServer
**Why:** C#-first design with Microsoft.Extensions.DependencyInjection, MediatR pattern overhead. While it works from F#, it's not idiomatic and adds unnecessary abstraction for functional F# code. Ionide.LanguageServerProtocol is lighter and F#-native.

### Avoid: Custom LSP Implementation
**Why:** LSP 3.17 spec is complex (type hierarchy, inline values, inlay hints, notebook support). Ionide.LanguageServerProtocol already handles spec compliance, JSON-RPC transport, stdio communication. Building from scratch is high risk, low reward.

### Avoid: FAKE Build System
**Why:** FAKE (F# Make) is excellent for complex multi-project builds, but adds tooling overhead for a single LSP server project. dotnet CLI handles compilation, testing, packaging adequately. FAKE's benefits (F# DSL, orchestration) not needed here.

### Avoid: Paket Package Manager
**Why:** Paket's advantages (strict transitive dependency resolution, git repository references, .fsx script support) are not relevant for this project. Standard NuGet is simpler, more familiar to newcomers (tutorial context), and sufficient for managing LSP dependencies.

### Avoid: Binary Protocols (MessagePack, Protobuf)
**Why:** LSP specification mandates JSON-RPC 2.0. While MessagePack/Protobuf are faster (3-10x), they're incompatible with LSP. Performance is not a bottleneck for typical LSP workloads (diagnostics, hover, completion).

### Avoid: TCP Sockets for Transport
**Why:** stdio is the de facto standard for LSP servers in VS Code. It's simpler (no port management), more portable (works across platforms), and directly supported by vscode-languageclient. Sockets add complexity without benefit.

### Avoid: .NET 9 (Standard Term Support)
**Why:** .NET 9 STS ends November 2026 (9 months). .NET 10 LTS runs until November 2028 (2.5+ years). For a tutorial project, LTS provides stability and longevity. Migration path from 9 to 10 is minimal but avoids near-term upgrade pressure.

## Optional/Advanced Libraries

| Library | Version | Purpose | When to Use | Confidence |
|---------|---------|---------|-------------|------------|
| FSharp.Data.Adaptive | 1.2.18+ | Incremental computation | If implementing advanced incremental re-analysis for large files, reactive workspace model. Overkill for MVP but excellent for performance optimization later | MEDIUM |
| Fantomas | 7.0.3+ | F# code formatting | If adding "format document" LSP feature for FunLang. Not needed for diagnostics/hover/completion/go-to-def MVP | LOW |
| FSharpLint | Latest | F# linting | If adding linting/code quality diagnostics to FunLang LSP. Not in MVP scope | LOW |

## Installation

### Server (F# LSP)
```bash
# Create F# console project
dotnet new console -lang F# -n FunLangLSP
cd FunLangLSP

# Add LSP framework
dotnet add package Ionide.LanguageServerProtocol --version 0.7.0

# Add testing framework
dotnet add package Expecto --version 10.2.3
dotnet add package Expecto.FsCheck --version 10.2.3

# If integrating with existing FunLang compiler (assuming separate project)
dotnet add reference ../FunLang/FunLang.fsproj

# Build
dotnet build

# Package as dotnet tool (optional, for distribution)
dotnet pack
```

### Client (VS Code Extension)
```bash
# Initialize extension
npm init
npm install --save vscode-languageclient

# Dev dependencies
npm install --save-dev @types/node @types/vscode typescript
```

## Target Framework Configuration

**Recommended:** `<TargetFramework>net10.0</TargetFramework>`

**Reasoning:**
- .NET 10 LTS (until Nov 2028)
- Ships with F# 10
- FSharp.Compiler.Service 43.10.102 targets F# 10
- Ionide.LanguageServerProtocol 0.7.0 targets .NET Standard 2.0 (compatible with net10.0)

**Alternative:** `net9.0` if you must use Visual Studio 2022 (requires VS 2026 for net10.0), but this is STS with shorter support lifecycle.

## LSP Protocol Version

**Target:** LSP 3.17 (supported by Ionide.LanguageServerProtocol 0.7.0)

**Features available:**
- Type hierarchy
- Inline values
- Inlay hints
- Notebook document support
- Meta model (formal protocol description)

**Future:** LSP 3.18 adds inline completions, dynamic text document content, multi-range formatting. Monitor Ionide.LanguageServerProtocol for 3.18 support.

## Communication Architecture

```
VS Code Extension (TypeScript)
    |
    | vscode-languageclient
    | (stdio transport)
    v
FunLang LSP Server (F#)
    |
    | Ionide.LanguageServerProtocol
    | (StreamJsonRpc + Newtonsoft.Json)
    |
    +-- LSP Request Handlers
    |       |
    |       v
    |   FunLang Compiler (Lexer, Parser, Type Checker)
    |       |
    |       v
    |   LSP Responses (Diagnostics, Hover, Completion, etc.)
```

**Transport:** stdin/stdout (standard for VS Code LSP)
**Protocol:** JSON-RPC 2.0
**Serialization:** JSON (Newtonsoft.Json)

## Version Lock Rationale

| Package | Why This Version |
|---------|------------------|
| Ionide.LanguageServerProtocol 0.7.0 | Latest stable (March 2025), LSP 3.17 support, proven in FsAutoComplete |
| FSharp.Compiler.Service 43.10.102 | Latest stable (Jan 2026), F# 10 support, matches .NET 10 SDK |
| StreamJsonRpc >= 2.16.36 | Minimum required by Ionide.LanguageServerProtocol, stable Microsoft library |
| Newtonsoft.Json >= 13.0.1 | Minimum required by Ionide.LanguageServerProtocol, widely compatible |
| .NET 10.0.x | LTS until 2028, F# 10, stable for tutorials and production |
| Expecto 10.2.3+ | Latest stable (March 2025), async-first testing for LSP |
| Node.js 22.13.14+ | Required by vscode-languageclient 10.x, LTS version |

## Sources

**High Confidence (Official Docs / Context7):**
- [Ionide.LanguageServerProtocol GitHub](https://github.com/ionide/LanguageServerProtocol) - Library repo and documentation
- [Ionide.LanguageServerProtocol NuGet](https://www.nuget.org/packages/Ionide.LanguageServerProtocol/) - Version 0.7.0 details and dependencies
- [FSharp.Compiler.Service NuGet](https://www.nuget.org/packages/FSharp.Compiler.Service) - Version 43.10.102 stable release
- [FsAutoComplete GitHub](https://github.com/ionide/FsAutoComplete) - Reference implementation using Ionide.LanguageServerProtocol
- [LSP Specification 3.17](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/) - Official protocol specification
- [.NET 10 Support](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview) - .NET 10 LTS and F# 10 info
- [Expecto NuGet](https://www.nuget.org/packages/Expecto/) - Version 10.2.3 and features

**Medium Confidence (Community Sources / 2024-2025 Articles):**
- [VS Code Language Server Extension Guide](https://code.visualstudio.com/api/language-extensions/language-server-extension-guide) - stdio transport best practices
- [F# LSP Compiler Guide](https://fsharp.github.io/fsharp-compiler-docs/lsp.html) - F# LSP design considerations
- [System.Text.Json vs Newtonsoft.Json Benchmarks 2025](https://jkrussell.dev/blog/system-text-json-vs-newtonsoft-json-benchmark/) - Performance comparison
- [Paket vs NuGet Comparison](https://github.com/fsprojects/Paket) - When to use each tool
- [FSharp.Data.Adaptive](https://fsprojects.github.io/FSharp.Data.Adaptive/) - Incremental computation library

**Confidence Notes:**
- All core library versions verified from NuGet/npm as of Feb 2026
- .NET 10 LTS and F# 10 confirmed from official Microsoft docs
- LSP 3.17 spec support in Ionide.LanguageServerProtocol confirmed from repo README
- Alternative comparisons based on architecture analysis and community consensus
