# F# 프로젝트 설정

이 문서는 F# LSP 서버 프로젝트를 처음부터 만드는 방법을 단계별로 설명합니다. 이 튜토리얼을 따라하면 FunLang LSP 서버의 전체 프로젝트 구조를 생성할 수 있습니다.

## 목차

1. [사전 요구사항](#사전-요구사항)
2. [프로젝트 구조 개요](#프로젝트-구조-개요)
3. [F# 프로젝트 생성](#f-프로젝트-생성)
4. [NuGet 패키지 설치](#nuget-패키지-설치)
5. [FunLang 서브모듈 연결](#funlang-서브모듈-연결)
6. [프로젝트 파일 구성](#프로젝트-파일-구성)
7. [테스트 프로젝트 설정](#테스트-프로젝트-설정)
8. [빌드 및 실행 확인](#빌드-및-실행-확인)

---

## 사전 요구사항

프로젝트를 시작하기 전에 다음 도구들이 설치되어 있어야 합니다.

### 1. .NET 10 SDK

```bash
# .NET 버전 확인
dotnet --version
# 출력: 10.0.x 이상
```

**설치 방법**: [.NET 공식 사이트](https://dotnet.microsoft.com/download)에서 .NET 10 SDK 다운로드

### 2. VS Code

**추천 확장 프로그램:**
- [Ionide for F#](https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp) - F# 개발 도구
- [F# (FsAutoComplete)](https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp) - F# LSP 클라이언트

### 3. Git

서브모듈 관리를 위해 Git이 필요합니다.

```bash
# Git 버전 확인
git --version
```

---

## 프로젝트 구조 개요

우리가 만들 프로젝트의 전체 구조는 다음과 같습니다.

```
LangLSP/
├── src/
│   ├── LangLSP.Server/      # F# LSP 서버 프로젝트
│   │   ├── Protocol.fs      # LSP 타입 변환 (Span ↔ Range)
│   │   ├── Server.fs        # 서버 capabilities 선언
│   │   ├── Program.fs       # 진입점 (stdin/stdout 설정)
│   │   └── LangLSP.Server.fsproj
│   └── LangLSP.Tests/       # 테스트 프로젝트 (Phase 2에서 추가)
├── client/                   # VS Code 확장 프로젝트 (Phase 3에서 추가)
├── LangTutorial/            # FunLang 서브모듈 (컴파일러 재사용)
│   └── FunLang/
│       ├── Ast.fs           # Span 타입 (소스 위치)
│       ├── Diagnostic.fs    # 에러 메시지 타입
│       ├── Lexer.fs         # 어휘 분석
│       ├── Parser.fs        # 구문 분석
│       └── TypeChecker.fs   # 타입 체커
├── docs/tutorial/           # 이 튜토리얼 문서들
└── LangLSP.sln              # 솔루션 파일
```

---

## F# 프로젝트 생성

### 1. 솔루션 생성

먼저 프로젝트 디렉토리를 만들고 솔루션을 생성합니다.

```bash
# 프로젝트 디렉토리 생성
mkdir LangLSP
cd LangLSP

# 솔루션 파일 생성
dotnet new sln -n LangLSP
```

생성된 `LangLSP.sln` 파일은 여러 F# 프로젝트를 하나로 묶어주는 역할을 합니다.

### 2. 서버 프로젝트 생성

LSP 서버를 담을 F# 콘솔 프로젝트를 생성합니다.

```bash
# 디렉토리 생성
mkdir -p src/LangLSP.Server
cd src/LangLSP.Server

# F# 콘솔 프로젝트 생성
dotnet new console -lang F# -n LangLSP.Server

# 루트로 돌아가기
cd ../..
```

**왜 콘솔 프로젝트인가?**
- LSP 서버는 stdin/stdout을 통해 에디터와 통신합니다
- 별도 프로세스로 실행되며, CLI 도구처럼 동작합니다

### 3. 솔루션에 프로젝트 추가

```bash
# 서버 프로젝트를 솔루션에 추가
dotnet sln add src/LangLSP.Server/LangLSP.Server.fsproj
```

솔루션에 추가하면 VS Code에서 전체 프로젝트를 한 번에 빌드하고 관리할 수 있습니다.

---

## NuGet 패키지 설치

LSP 서버에 필요한 NuGet 패키지들을 설치합니다.

```bash
cd src/LangLSP.Server

# Ionide.LanguageServerProtocol: LSP 프로토콜 구현
dotnet add package Ionide.LanguageServerProtocol --version 0.7.0

# Serilog: 구조화된 로깅
dotnet add package Serilog --version 4.2.0

# Serilog.Sinks.File: 파일 로깅
dotnet add package Serilog.Sinks.File --version 6.0.0

cd ../..
```

### 패키지 역할 설명

#### Ionide.LanguageServerProtocol (0.7.0)
- **역할**: LSP 프로토콜 구현 (JSON-RPC, LSP 타입)
- **장점**: F# 네이티브, FsAutoComplete에서 검증됨
- **제공 기능**:
  - `Ionide.LanguageServerProtocol.Types` - LSP 타입 (Range, Position, Diagnostic 등)
  - `Ionide.LanguageServerProtocol.Server` - 서버 구현 헬퍼

#### Serilog (4.2.0 + Sinks.File 6.0.0)
- **역할**: 디버깅용 구조화된 로깅
- **왜 파일 로깅인가?**: LSP 서버는 stdout을 프로토콜 통신에 사용하므로, 콘솔에 로그를 출력하면 프로토콜이 깨집니다
- **로그 위치**: `/tmp/funlang-lsp.log` (Linux/macOS) 또는 `%TEMP%` (Windows)

---

## FunLang 서브모듈 연결

FunLang 컴파일러 코드를 재사용하기 위해 Git 서브모듈로 추가합니다.

### 1. 서브모듈 추가

```bash
# 서브모듈로 FunLang 프로젝트 추가
git submodule add https://github.com/kodu-ai/LangTutorial.git

# 서브모듈 초기화 및 업데이트
git submodule update --init --recursive
```

### 2. FunLang 프로젝트 참조 추가

```bash
# LangLSP.Server에서 FunLang 프로젝트 참조
dotnet add src/LangLSP.Server/LangLSP.Server.fsproj reference LangTutorial/FunLang/FunLang.fsproj

# 솔루션에 FunLang 프로젝트도 추가
dotnet sln add LangTutorial/FunLang/FunLang.fsproj
```

### 왜 서브모듈인가?

FunLang 컴파일러는 이미 다음 기능들을 구현하고 있습니다.

**재사용할 모듈:**
- `Ast.fs` - **Span 타입**: 소스 코드 위치 (Line, Column)
- `Diagnostic.fs` - **진단 타입**: 에러 메시지 구조
- `Lexer.fs`, `Parser.fs` - 구문 분석
- `TypeChecker.fs` - 타입 체킹 (LSP 진단에 사용)

**Span 타입 예시:**

```fsharp
// Ast.fs에서 정의됨
type Span = {
    FileName: string
    StartLine: int    // 1-based (사람이 읽는 방식)
    StartColumn: int  // 1-based
    EndLine: int
    EndColumn: int
}
```

LSP는 0-based Position을 사용하므로, Protocol.fs에서 변환 함수를 구현합니다.

```fsharp
// Protocol.fs
let spanToLspRange (span: Span) : Range =
    {
        Start = { Line = uint32 (span.StartLine - 1); Character = uint32 (span.StartColumn - 1) }
        End = { Line = uint32 (span.EndLine - 1); Character = uint32 (span.EndColumn - 1) }
    }
```

---

## 프로젝트 파일 구성

F#은 **파일 순서에 의존하는 언어**입니다. F#에는 순방향 참조가 없으므로, 파일이 `.fsproj`에 나열된 순서대로 컴파일됩니다.

### F# 컴파일 순서의 중요성

```fsharp
// Protocol.fs (먼저 컴파일)
module Protocol
let spanToLspRange (span: Span) : Range = ...

// Server.fs (나중에 컴파일)
module Server
open Protocol  // Protocol 모듈을 사용 가능
let serverCapabilities = ...
```

만약 `Server.fs`를 `Protocol.fs`보다 먼저 나열하면, 컴파일 에러가 발생합니다.

### LangLSP.Server.fsproj 구조

프로젝트 파일의 전체 내용은 다음과 같습니다.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- F# 파일 컴파일 순서 (중요!) -->
    <Compile Include="Protocol.fs" />
    <Compile Include="Server.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Ionide.LanguageServerProtocol" Version="0.7.0" />
    <PackageReference Include="Serilog" Version="4.2.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../LangTutorial/FunLang/FunLang.fsproj" />
  </ItemGroup>

</Project>
```

### 파일별 역할

| 파일 | 역할 | 의존성 |
|------|------|-------|
| `Protocol.fs` | Span ↔ LSP Range 변환 함수 | FunLang.Ast, Ionide.Types |
| `Server.fs` | ServerCapabilities 선언 | Ionide.Types |
| `Program.fs` | 진입점, stdin/stdout 설정 | Protocol, Server, Serilog |

**컴파일 순서 규칙:**
- `Program.fs`는 항상 **마지막**에 위치 (진입점)
- `Protocol.fs`는 다른 모듈보다 **먼저** (유틸리티 함수)

---

## 테스트 프로젝트 설정

LSP 서버의 기능을 테스트하기 위한 테스트 프로젝트를 추가합니다. (Phase 2에서 본격적으로 사용)

```bash
# 테스트 프로젝트 디렉토리 생성
mkdir -p src/LangLSP.Tests
cd src/LangLSP.Tests

# F# 콘솔 프로젝트 생성
dotnet new console -lang F# -n LangLSP.Tests

# Expecto 테스트 프레임워크 설치
dotnet add package Expecto --version 10.2.3
dotnet add package Expecto.FsCheck --version 10.2.3

# LangLSP.Server 프로젝트 참조
dotnet add reference ../LangLSP.Server/LangLSP.Server.fsproj

# 루트로 돌아가기
cd ../..

# 솔루션에 추가
dotnet sln add src/LangLSP.Tests/LangLSP.Tests.fsproj
```

### 왜 Expecto인가?

- **F# 네이티브**: F#으로 작성된 테스트 프레임워크
- **표현력 높은 DSL**: `testCase`, `testList`, `testProperty` 등
- **FsCheck 통합**: Property-based testing 지원
- **FsAutoComplete 검증**: FsAutoComplete 프로젝트에서도 사용 중

### 테스트 예시 (Phase 2에서 작성 예정)

```fsharp
// LangLSP.Tests/ProtocolTests.fs
module ProtocolTests

open Expecto
open LangLSP.Server.Protocol
open Ast

[<Tests>]
let tests = testList "Protocol" [
    testCase "spanToLspRange converts 1-based to 0-based" <| fun _ ->
        let span = { FileName = "test.fun"; StartLine = 1; StartColumn = 1; EndLine = 1; EndColumn = 5 }
        let range = spanToLspRange span
        Expect.equal range.Start.Line 0u "Line should be 0-based"
        Expect.equal range.Start.Character 0u "Character should be 0-based"
]
```

---

## 빌드 및 실행 확인

모든 설정이 완료되었으면, 프로젝트가 정상적으로 빌드되는지 확인합니다.

### 1. 전체 솔루션 빌드

```bash
# 루트 디렉토리에서 실행
dotnet build

# 출력 예시:
#   Building...
#   FunLang -> /path/to/LangTutorial/FunLang/bin/Debug/net10.0/FunLang.dll
#   LangLSP.Server -> /path/to/src/LangLSP.Server/bin/Debug/net10.0/LangLSP.Server.dll
#   Build succeeded.
```

### 2. 서버 실행 (테스트)

```bash
dotnet run --project src/LangLSP.Server
```

서버가 시작되면 stdin 대기 상태가 됩니다.

**예상 동작:**
- 콘솔에는 아무것도 출력되지 않음 (stdout은 LSP 프로토콜용)
- `/tmp/funlang-lsp.log` 파일에 로그가 기록됨

**로그 확인:**

```bash
# 로그 파일 확인 (Linux/macOS)
tail -f /tmp/funlang-lsp.log

# 출력 예시:
# 2025-02-04 15:20:01.123 +00:00 [INF] FunLang LSP Server starting...
# 2025-02-04 15:20:01.456 +00:00 [INF] Setting up LSP server on stdin/stdout
# 2025-02-04 15:20:01.789 +00:00 [INF] Server capabilities configured: ...
```

**종료:**
- `Ctrl+C`로 서버 종료
- 또는 stdin을 닫으면 서버가 자동 종료

### 3. 초기 파일 구조 확인

프로젝트가 정상적으로 생성되었는지 확인합니다.

```bash
# 프로젝트 구조 확인
tree -L 3 src/

# 출력:
# src/
# ├── LangLSP.Server
# │   ├── bin
# │   ├── obj
# │   ├── LangLSP.Server.fsproj
# │   ├── Program.fs
# │   ├── Protocol.fs
# │   └── Server.fs
# └── LangLSP.Tests
#     ├── bin
#     ├── obj
#     ├── LangLSP.Tests.fsproj
#     └── Program.fs
```

---

## 다음 단계

프로젝트 설정이 완료되었습니다! 이제 다음 작업들을 진행할 수 있습니다.

**Phase 1 - 다음 튜토리얼:**
1. **04-document-sync.md**: 문서 동기화 구현 (didOpen, didChange)
2. **05-diagnostics.md**: 실시간 진단 발행 (TypeChecker 통합)

**Phase 2 - LSP 기능 확장:**
- Hover (타입 정보 표시)
- Completion (자동 완성)
- Go to Definition
- Find References

**Phase 3 - VS Code 확장:**
- `client/` 디렉토리에 VS Code 확장 프로젝트 생성
- LSP 서버와 연동

---

## 초기 파일 내용 참고

프로젝트 생성 후 각 파일의 초기 내용은 다음과 같습니다.

### Protocol.fs

```fsharp
module LangLSP.Server.Protocol

open Ionide.LanguageServerProtocol.Types
open Ast  // FunLang's Span type

/// Convert FunLang Span (1-based) to LSP Range (0-based)
let spanToLspRange (span: Span) : Range =
    {
        Start = { Line = uint32 (span.StartLine - 1); Character = uint32 (span.StartColumn - 1) }
        End = { Line = uint32 (span.EndLine - 1); Character = uint32 (span.EndColumn - 1) }
    }

/// Convert FunLang Diagnostic to LSP Diagnostic
let diagnosticToLsp (diag: Diagnostic.Diagnostic) : Diagnostic =
    {
        Range = spanToLspRange diag.PrimarySpan
        Severity = Some DiagnosticSeverity.Error
        Code = None
        Source = Some "funlang"
        Message = diag.Message
        // ... (나머지 필드)
    }
```

### Server.fs

```fsharp
module LangLSP.Server.Server

open Ionide.LanguageServerProtocol.Types

/// Server capabilities declaration
let serverCapabilities : ServerCapabilities =
    { ServerCapabilities.Default with
        TextDocumentSync =
            Some (U2.C1 {
                TextDocumentSyncOptions.Default with
                    OpenClose = Some true
                    Change = Some TextDocumentSyncKind.Incremental
            })
    }
```

### Program.fs

```fsharp
module LangLSP.Server.Program

open System
open Serilog

[<EntryPoint>]
let main argv =
    // Serilog 설정
    Log.Logger <-
        LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("/tmp/funlang-lsp.log", rollingInterval = RollingInterval.Day)
            .CreateLogger()

    Log.Information("FunLang LSP Server starting...")

    // stdin/stdout 설정
    use input = Console.OpenStandardInput()
    use output = Console.OpenStandardOutput()

    // 서버 실행 대기
    // TODO: 다음 Phase에서 LSP 메시지 루프 구현

    0
```

---

## 참고 자료

- [.NET CLI 공식 문서](https://docs.microsoft.com/dotnet/core/tools/)
- [F# 프로젝트 구조](https://fsharp.org/guides/project-structure/)
- [Ionide.LanguageServerProtocol GitHub](https://github.com/ionide/LanguageServerProtocol)
- [Expecto 문서](https://github.com/haf/expecto)
- [Git Submodules 가이드](https://git-scm.com/book/en/v2/Git-Tools-Submodules)

---

**→ 다음: [04-document-sync.md](./04-document-sync.md)** - 문서 동기화 구현
