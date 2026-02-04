# F# LSP 라이브러리 선택

이 문서는 F#으로 LSP 서버를 구현할 때 사용할 수 있는 라이브러리들을 비교하고, 왜 **Ionide.LanguageServerProtocol**을 선택했는지 설명합니다.

## 목차

1. [F# LSP 라이브러리 옵션](#f-lsp-라이브러리-옵션)
2. [Ionide.LanguageServerProtocol 소개](#ionidelanguageserverprotocol-소개)
3. [OmniSharp 대비 장점](#omnisharp-대비-장점)
4. [직접 구현을 피해야 하는 이유](#직접-구현을-피해야-하는-이유)
5. [결론: Ionide 선택](#결론-ionide-선택)

---

## F# LSP 라이브러리 옵션

F#으로 LSP 서버를 만들 때 선택할 수 있는 옵션은 크게 3가지입니다.

### 1. Ionide.LanguageServerProtocol

- **NuGet**: [`Ionide.LanguageServerProtocol`](https://www.nuget.org/packages/Ionide.LanguageServerProtocol/)
- **언어**: F#
- **프로젝트**: [Ionide](https://ionide.io/) 생태계의 일부
- **실제 사용 사례**: [FsAutoComplete](https://github.com/fsharp/FsAutoComplete) (F# LSP 서버)

### 2. OmniSharp.LanguageServerProtocol

- **NuGet**: [`OmniSharp.Extensions.LanguageServer`](https://www.nuget.org/packages/OmniSharp.Extensions.LanguageServer/)
- **언어**: C#
- **프로젝트**: [OmniSharp](https://www.omnisharp.net/) 생태계
- **실제 사용 사례**: OmniSharp C# Language Server

### 3. 직접 구현 (Custom Implementation)

- JSON-RPC 2.0 메시지 파싱/직렬화 직접 작성
- LSP 타입 정의 모두 직접 작성
- stdin/stdout 파이프 처리 직접 구현

---

## Ionide.LanguageServerProtocol 소개

**Ionide.LanguageServerProtocol**은 F# 네이티브 LSP 라이브러리입니다.

### 핵심 특징

- **F#으로 작성됨**: F# 타입 시스템과 자연스럽게 통합
- **Ionide 프로젝트 일부**: F# 커뮤니티에서 검증됨
- **경량 설계**: 최소한의 의존성 (Microsoft.Extensions.DI 불필요)
- **LSP 3.17 지원**: 최신 LSP 스펙 완전 지원
- **프로덕션 검증**: FsAutoComplete가 수년간 사용 중

### 버전 정보

- **현재 버전**: 0.7.0 (2025년 3월 12일 릴리스)
- **호환성**: .NET 6.0+
- **NuGet 다운로드**: ~50만 회 이상

### 기본 사용 예시

```fsharp
open Ionide.LanguageServerProtocol
open Ionide.LanguageServerProtocol.Server
open Ionide.LanguageServerProtocol.Types

// 서버 초기화
let server = Server()

// initialize 핸들러 등록
server.On<InitializeParams, InitializeResult>("initialize", fun p ->
    async {
        return {
            Capabilities = {
                HoverProvider = Some true
                CompletionProvider = Some { TriggerCharacters = Some [|"."|] }
                TextDocumentSync = Some (TextDocumentSyncKind.Incremental)
                // ... 기타 capabilities
            }
            ServerInfo = Some {
                Name = "FunLang LSP"
                Version = Some "1.0.0"
            }
        }
    }
)

// hover 핸들러 등록
server.On<HoverParams, Hover option>("textDocument/hover", fun p ->
    async {
        // 호버 로직 구현
        return Some {
            Contents = MarkupContent {
                Kind = MarkupKind.Markdown
                Value = "**함수**: `add`\n\n타입: `(Int, Int) -> Int`"
            }
            Range = None
        }
    }
)

// 서버 실행
server.Start()
```

---

## OmniSharp 대비 장점

Ionide와 OmniSharp를 비교하면 다음과 같습니다.

### 비교표

| 비교 항목 | Ionide.LanguageServerProtocol | OmniSharp.Extensions.LanguageServer |
|-----------|-------------------------------|-------------------------------------|
| **언어** | F# (네이티브) | C# |
| **DI 의존성** | 없음 (순수 F#) | Microsoft.Extensions.DependencyInjection 필수 |
| **설계 철학** | 함수형, immutable types | OOP, mutable state |
| **학습 곡선** | 낮음 (F# 개발자에게) | 높음 (복잡한 DI 설정) |
| **커뮤니티** | F# 중심 (Ionide, FSAC) | C# 중심 (OmniSharp) |
| **의존성 크기** | 작음 (~10개 패키지) | 큼 (~30개 패키지) |
| **타입 안전성** | Discriminated Union 활용 | Class hierarchy |
| **LSP 스펙 지원** | LSP 3.17 | LSP 3.17 |
| **실제 사용 사례** | FsAutoComplete | OmniSharp C# Server |

### 왜 Ionide를 선택했는가?

#### 1. F# 네이티브 구현

FunLang 컴파일러가 F#으로 작성되어 있으므로, F# 라이브러리를 사용하는 것이 자연스럽습니다.

```fsharp
// FunLang AST (F# record)
type Expr =
    | Lit of int
    | Add of Expr * Expr

// Ionide LSP types (F# record)
type Position = { Line: int; Character: int }
type Range = { Start: Position; End: Position }
```

두 라이브러리 모두 F#의 record, discriminated union을 사용하므로 타입이 잘 맞습니다.

#### 2. Dependency Injection 불필요

OmniSharp는 C# 스타일의 DI 컨테이너를 강제합니다.

**OmniSharp 스타일 (C# DI 필수):**

```csharp
// OmniSharp는 이런 DI 설정이 필요
services.AddLanguageServer(options => {
    options
        .WithHandler<TextDocumentHandler>()
        .WithHandler<HoverHandler>()
        .OnInitialize((server, request, token) => { ... });
});
```

**Ionide 스타일 (순수 F#):**

```fsharp
// Ionide는 간단한 함수 등록
server.On<HoverParams, Hover option>("textDocument/hover", hoverHandler)
```

F# 개발자에게는 Ionide의 함수형 스타일이 더 자연스럽습니다.

#### 3. FsAutoComplete의 검증

FsAutoComplete는 F# LSP 서버로, VS Code에서 수백만 명의 개발자가 사용합니다.

```
VS Code F# Extension
    ↓
FsAutoComplete (F# LSP Server)
    ↓
Ionide.LanguageServerProtocol
```

이미 프로덕션에서 수년간 검증된 라이브러리입니다.

#### 4. 경량 설계

**Ionide 의존성:**
- Ionide.LanguageServerProtocol
- Newtonsoft.Json (JSON 직렬화)
- StreamJsonRpc (JSON-RPC 처리)

**OmniSharp 의존성:**
- OmniSharp.Extensions.LanguageServer
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging
- ... (약 30개 패키지)

튜토리얼 목적의 LSP 서버에는 Ionide의 경량 설계가 적합합니다.

---

## 직접 구현을 피해야 하는 이유

"LSP는 그냥 JSON이니까 직접 구현하면 되지 않을까?" - **이건 함정입니다.**

### LSP 스펙의 복잡성

**LSP 3.17 스펙 통계:**
- 문서 길이: 285페이지
- 메서드 수: 90개 이상
- 타입 정의: 407개
- 엣지 케이스: 수백 가지

### 직접 구현 시 마주칠 문제들

#### 1. Content-Length 헤더 파싱

LSP 메시지는 HTTP 스타일 헤더를 사용합니다.

```
Content-Length: 123\r\n
Content-Type: application/vscode-jsonrpc; charset=utf-8\r\n
\r\n
{...JSON...}
```

- `\r\n` (CRLF) 파싱 버그 발생 가능
- 헤더가 없거나 잘못된 경우 처리
- UTF-8 인코딩 문제

#### 2. UTF-16 Position 인코딩

LSP의 `Position`은 **UTF-16 code unit** 기준입니다.

```fsharp
let text = "안녕하세요 🎉"
// UTF-8: 각 한글 = 3바이트, 🎉 = 4바이트
// UTF-16: 각 한글 = 1 code unit, 🎉 = 2 code units

// LSP Position.character는 UTF-16 기준으로 계산해야 함
```

직접 구현하면 이모지, CJK 문자에서 위치 계산 버그가 발생합니다.

#### 3. 407개 타입 정의

LSP 타입을 모두 직접 정의하면:

```fsharp
type Position = { Line: int; Character: int }
type Range = { Start: Position; End: Position }
type Location = { Uri: DocumentUri; Range: Range }
type TextEdit = { Range: Range; NewText: string }
type CompletionItem = { ... }  // 30개 필드
type CompletionList = { ... }
type Hover = { ... }
// ... 400개 더
```

이미 Ionide에 모두 정의되어 있습니다.

#### 4. JSON 직렬화 버그

LSP는 `null`과 `undefined`를 구분합니다.

```json
// 이 둘은 의미가 다름
{"hoverProvider": null}       // hover 지원 안 함
{"hoverProvider": undefined}  // 필드 생략됨
```

직접 JSON 직렬화를 구현하면 이런 미묘한 차이를 놓칩니다.

### 결론: 바퀴의 재발명 회피

> "Don't reinvent the wheel. Use a proven library."

LSP 라이브러리는 이미 수년간 검증되었습니다. 튜토리얼의 목적은 **LSP 메시지 파싱**이 아니라 **LSP 서버 로직 구현**입니다.

---

## 결론: Ionide 선택

이 튜토리얼 시리즈는 **Ionide.LanguageServerProtocol**을 사용합니다.

### 선택 이유 요약

1. **F# 네이티브**: FunLang이 F#이므로 타입 시스템 자연스럽게 통합
2. **검증된 신뢰성**: FsAutoComplete가 프로덕션에서 검증
3. **경량 설계**: 최소한의 의존성, DI 불필요
4. **학습 곡선**: F# 개발자에게 친숙한 함수형 API
5. **최신 스펙 지원**: LSP 3.17 완전 지원

### 설치 방법

```bash
dotnet add package Ionide.LanguageServerProtocol --version 0.7.0
```

### 다음 단계

다음 문서에서는 Ionide.LanguageServerProtocol을 사용해 최소한의 LSP 서버를 구현합니다.

---

## 참고 자료

- [Ionide.LanguageServerProtocol NuGet](https://www.nuget.org/packages/Ionide.LanguageServerProtocol/)
- [Ionide 공식 사이트](https://ionide.io/)
- [FsAutoComplete GitHub](https://github.com/fsharp/FsAutoComplete)
- [OmniSharp Extensions](https://github.com/OmniSharp/csharp-language-server-protocol)
- [LSP 스펙 문서](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/)
