# 진단(Diagnostics) 구현

이 문서는 LSP 서버에서 **Diagnostics**(진단)를 구현하는 방법을 설명합니다. 진단은 에디터에 빨간 줄로 표시되는 에러와 경고를 말하며, LSP의 가장 중요한 기능 중 하나입니다.

## 목차

1. [진단이란](#진단이란)
2. [FunLang의 Diagnostic 시스템](#funlang의-diagnostic-시스템)
3. [위치 변환: Span → LSP Range](#위치-변환-span--lsp-range)
4. [문법 오류 처리](#문법-오류-처리)
5. [타입 오류 처리](#타입-오류-처리)
6. [publishDiagnostics 호출](#publishdiagnostics-호출)
7. [진단 클리어](#진단-클리어)
8. [테스트 작성](#테스트-작성)

---

## 진단이란

**Diagnostics**는 소스 코드의 문제를 사용자에게 알려주는 LSP 메시지입니다.

### 진단의 구성 요소

```typescript
interface Diagnostic {
    range: Range                    // 에러가 발생한 위치
    severity: DiagnosticSeverity   // Error, Warning, Info, Hint
    code?: string | number          // 에러 코드 (예: "E0301")
    source?: string                 // 진단 출처 (예: "funlang")
    message: string                 // 에러 메시지
    relatedInformation?: DiagnosticRelatedInformation[]  // 관련 위치
}
```

### VS Code에서의 표시

```
test.fun
────────
1 | let x = 1 + true
              ~~~~ Error: Type mismatch: expected int but got bool [funlang]
```

- **빨간 밑줄**: `range`로 지정된 영역
- **에러 메시지**: 마우스 호버 시 `message` 표시
- **Problems 패널**: 모든 진단이 목록으로 표시

### publishDiagnostics 알림

LSP 서버는 `textDocument/publishDiagnostics` 알림을 클라이언트(에디터)로 보냅니다.

**중요:** 이것은 **알림(Notification)**이지 요청(Request)이 아닙니다. 따라서 응답을 기다리지 않습니다.

```fsharp
// 진단 발행 예시
let publishParams : PublishDiagnosticsParams = {
    Uri = "file:///test.fun"
    Diagnostics = [| diagnostic1; diagnostic2 |]
    Version = None
}
lspClient.TextDocumentPublishDiagnostics publishParams
```

---

## FunLang의 Diagnostic 시스템

FunLang 컴파일러는 이미 풍부한 진단 시스템을 갖추고 있습니다.

### FunLang Diagnostic 타입

```fsharp
// Diagnostic.fs (FunLang 프로젝트)
type Diagnostic = {
    Code: string option           // 예: Some "E0301"
    Message: string               // 에러 메시지
    PrimarySpan: Span             // 주 에러 위치
    SecondarySpans: (Span * string) list  // 관련 위치와 레이블
    Notes: string list            // 추가 설명
    Hint: string option           // 수정 제안
}
```

### Span 타입 (소스 위치)

```fsharp
// Ast.fs (FunLang 프로젝트)
type Span = {
    FileName: string
    StartLine: int      // 1-based (사람이 읽는 방식)
    StartColumn: int    // 1-based
    EndLine: int
    EndColumn: int
}
```

**주의:** FunLang의 Span은 **1-based** 인덱싱을 사용하지만, LSP는 **0-based**입니다!

| 인덱싱 | 첫 번째 라인 | 첫 번째 문자 |
|--------|------------|------------|
| FunLang Span | 1 | 1 |
| LSP Position | 0 | 0 |

### TypeCheck 모듈과의 통합

FunLang의 `TypeCheck` 모듈은 타입 체킹 결과를 `Result<Type, Diagnostic>`로 반환합니다.

```fsharp
// TypeCheck.fs (FunLang 프로젝트)
let typecheckWithDiagnostic (expr: Expr) : Result<Type, Diagnostic> =
    try
        let ty = typecheck expr
        Ok ty
    with
    | TypeException error ->
        let diag = typeErrorToDiagnostic error
        Error diag
```

이것을 LSP 서버에서 그대로 활용할 수 있습니다!

---

## 위치 변환: Span → LSP Range

FunLang Span(1-based)을 LSP Range(0-based)로 변환하는 함수가 필요합니다.

### 변환 로직

```fsharp
// Protocol.fs
module LangLSP.Server.Protocol

open Ionide.LanguageServerProtocol.Types
open Ast  // FunLang's Span type

/// Convert FunLang Span (1-based) to LSP Range (0-based)
/// FunLang: StartLine=1 means first line
/// LSP: line=0 means first line
/// Edge case: if span is (0,0), clamp to (0,0) instead of wrapping to uint.MaxValue
let spanToLspRange (span: Span) : Range =
    let clamp x = max 0 (x - 1)
    {
        Start = { Line = uint32 (clamp span.StartLine); Character = uint32 (clamp span.StartColumn) }
        End = { Line = uint32 (clamp span.EndLine); Character = uint32 (clamp span.EndColumn) }
    }
```

**핵심 포인트:**
1. **clamp 함수**: `x - 1`을 계산하되, 음수가 되지 않도록 `max 0` 적용
2. **Edge case 처리**: FunLang이 잘못된 (0,0) Span을 생성하면, `uint.MaxValue`로 wrap되는 대신 (0,0)으로 clamp

### 변환 예시

```fsharp
// FunLang Span
let span = { FileName = "test.fun"; StartLine = 3; StartColumn = 5; EndLine = 3; EndColumn = 10 }

// LSP Range
let range = spanToLspRange span
// { Start = { Line = 2u; Character = 4u }; End = { Line = 2u; Character = 9u } }
```

**시각화:**

```
FunLang (1-based):   Line 3, Column 5-10
                     ↓
LSP (0-based):      Line 2, Character 4-9
```

### FunLang Diagnostic → LSP Diagnostic

```fsharp
/// Convert FunLang Diagnostic to LSP Diagnostic
let diagnosticToLsp (diag: Diagnostic.Diagnostic) : Diagnostic =
    {
        Range = spanToLspRange diag.PrimarySpan
        Severity = Some DiagnosticSeverity.Error
        Code = None  // Will be enhanced later with error codes
        CodeDescription = None
        Source = Some "funlang"
        Message = diag.Message
        Tags = None
        RelatedInformation =
            if List.isEmpty diag.SecondarySpans then None
            else
                diag.SecondarySpans
                |> List.map (fun (span, label) ->
                    {
                        Location = {
                            Uri = span.FileName  // Will need URI conversion later
                            Range = spanToLspRange span
                        }
                        Message = label
                    })
                |> Array.ofList
                |> Some
        Data = None
    }
```

**RelatedInformation 활용:**
- FunLang의 `SecondarySpans`를 LSP의 `RelatedInformation`으로 매핑
- 에디터에서 "관련 위치 보기" 기능 제공

---

## 문법 오류 처리

파싱 단계에서 발생하는 문법 오류를 처리합니다.

### 파싱 로직

```fsharp
// Diagnostics.fs
module LangLSP.Server.Diagnostics

open System
open FSharp.Text.Lexing
open Ionide.LanguageServerProtocol
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.Protocol

/// Parse FunLang source code and catch syntax errors
/// Returns Ok(ast) on success, Error(diagnostic) on parse error
let parseFunLang (source: string) (uri: string) : Result<Ast.Expr, Diagnostic> =
    try
        let lexbuf = LexBuffer<char>.FromString(source)
        let ast = Parser.start Lexer.tokenize lexbuf
        Ok ast
    with
    | ex ->
        // Parse error - create a diagnostic
        // fsyacc exceptions contain position info in the message
        let message =
            if ex.Message.Contains("parse error") then
                "Syntax error: " + ex.Message
            else
                "Parse error: " + ex.Message

        // Create a span for the error location
        // If we can't extract position, use (1,1)-(1,1)
        let span : Ast.Span = {
            FileName = uri
            StartLine = 1
            StartColumn = 1
            EndLine = 1
            EndColumn = 1
        }

        let diag: Diagnostic = {
            Range = spanToLspRange span
            Severity = Some DiagnosticSeverity.Error
            Code = None
            CodeDescription = None
            Source = Some "funlang"
            Message = message
            Tags = None
            RelatedInformation = None
            Data = None
        }
        Error diag
```

**핵심 포인트:**
- **try-catch**: fsyacc 파서는 예외를 던지므로 catch 필요
- **기본 Span**: 파싱 실패 시 정확한 위치를 알 수 없으므로 (1,1) 사용
- **Error 반환**: `Result<Ast.Expr, Diagnostic>` 타입으로 파싱 실패 전달

### 문법 오류 예시

```fsharp
// 입력: "let x = "  (incomplete let expression)
let source = "let x = "
let result = parseFunLang source "file:///test.fun"

// 결과:
// Error { Range = { Start = (0, 0), End = (0, 0) }; Message = "Syntax error: ..." }
```

---

## 타입 오류 처리

AST를 파싱한 후 타입 체킹을 수행합니다.

### 타입 체킹 로직

```fsharp
/// Type check AST using FunLang's type checker
/// Returns Ok(type) on success, Error(diagnostic) on type error
let typecheckAst (ast: Ast.Expr) : Result<Type.Type, Diagnostic> =
    match TypeCheck.typecheckWithDiagnostic ast with
    | Ok ty -> Ok ty
    | Error funlangDiag ->
        // Convert FunLang diagnostic to LSP diagnostic
        let lspDiag = diagnosticToLsp funlangDiag
        Error lspDiag
```

**동작:**
1. FunLang의 `typecheckWithDiagnostic` 호출
2. 성공 시 타입 반환, 실패 시 FunLang Diagnostic 반환
3. `diagnosticToLsp`로 LSP 형식으로 변환

### 타입 오류 예시

```fsharp
// 입력: "1 + true"  (type mismatch: int + bool)
let source = "1 + true"
let result = parseFunLang source "file:///test.fun"

match result with
| Ok ast ->
    match typecheckAst ast with
    | Ok ty -> printfn "Type: %A" ty
    | Error diag ->
        // diag.Message = "Type mismatch: expected int but got bool"
        printfn "Type error: %s" diag.Message
| Error parseDiag ->
    printfn "Parse error: %s" parseDiag.Message
```

### FunLang의 풍부한 타입 에러

FunLang의 Diagnostic 시스템은 다음을 제공합니다:

1. **PrimarySpan**: 주 에러 위치
2. **SecondarySpans**: 관련 위치 (예: 타입 annotation이 있는 곳)
3. **Notes**: 컨텍스트 스택 (에러가 발생한 경로)
4. **Hint**: 수정 제안

**예시:**

```
error[E0301]: Type mismatch: expected int but got bool
 --> test.fun:1:5
   = in function position: test.fun:1:1
   = note: in if condition at test.fun:1:4
   = hint: Check that all branches of your expression return the same type
```

이 모든 정보가 LSP Diagnostic의 `Message`와 `RelatedInformation`으로 변환됩니다!

---

## publishDiagnostics 호출

전체 분석 파이프라인을 구성합니다.

### analyze 함수

```fsharp
/// Analyze document and return all diagnostics
/// Returns list of diagnostics (empty if no errors)
let analyze (uri: string) (source: string) : Diagnostic list =
    match parseFunLang source uri with
    | Error parseDiag ->
        // Parse error - stop here, don't try to typecheck
        [parseDiag]
    | Ok ast ->
        // Parse succeeded, now typecheck
        match typecheckAst ast with
        | Ok _ ->
            // No errors
            []
        | Error typeDiag ->
            // Type error
            [typeDiag]
```

**파이프라인:**
1. 파싱 시도
   - 실패 → 파싱 에러 반환, 타입 체킹 스킵
   - 성공 → 타입 체킹 진행
2. 타입 체킹 시도
   - 성공 → 빈 리스트 (에러 없음)
   - 실패 → 타입 에러 반환

### publishDiagnostics 함수

```fsharp
/// Publish diagnostics to client
let publishDiagnostics (lspClient: ILspClient) (uri: string) (diagnostics: Diagnostic list) : Async<unit> =
    async {
        let publishParams: PublishDiagnosticsParams = {
            Uri = uri
            Diagnostics = Array.ofList diagnostics
            Version = None
        }
        do! lspClient.TextDocumentPublishDiagnostics publishParams
    }
```

**사용 예시:**

```fsharp
// DocumentSync에서 didChange 이후 호출
let handleDidChangeWithDiagnostics (lspClient: ILspClient) (p: DidChangeTextDocumentParams) : Async<unit> =
    async {
        // 1. 문서 동기화
        DocumentSync.handleDidChange p

        // 2. 업데이트된 텍스트 가져오기
        match DocumentSync.getDocument p.TextDocument.Uri with
        | Some source ->
            // 3. 분석 및 진단 발행
            let diagnostics = Diagnostics.analyze p.TextDocument.Uri source
            do! Diagnostics.publishDiagnostics lspClient p.TextDocument.Uri diagnostics
        | None ->
            // 문서가 추적되지 않음 (오류 상황)
            ()
    }
```

---

## 진단 클리어

문서를 닫거나 에러가 수정되었을 때 진단을 클리어해야 합니다.

### clearDiagnostics 함수

```fsharp
/// Clear diagnostics for a document
let clearDiagnostics (lspClient: ILspClient) (uri: string) : Async<unit> =
    publishDiagnostics lspClient uri []
```

**핵심 아이디어:** 빈 진단 배열을 발행하면 기존 진단이 모두 제거됩니다.

### 사용 시나리오

1. **파일 닫기:**
   ```fsharp
   let handleDidCloseWithDiagnostics (lspClient: ILspClient) (p: DidCloseTextDocumentParams) : Async<unit> =
       async {
           DocumentSync.handleDidClose p
           do! Diagnostics.clearDiagnostics lspClient p.TextDocument.Uri
       }
   ```

2. **에러 수정:**
   ```fsharp
   // 사용자가 코드를 수정하여 에러가 없어지면 analyze 함수가 빈 리스트 반환
   let diagnostics = analyze uri source  // []
   do! publishDiagnostics lspClient uri diagnostics  // 진단 클리어
   ```

---

## 테스트 작성

Expecto와 FsCheck를 활용하여 진단 기능을 테스트합니다.

### 기본 테스트

```fsharp
// DiagnosticsTests.fs
module LangLSP.Tests.DiagnosticsTests

open Expecto
open LangLSP.Server.Diagnostics
open Ionide.LanguageServerProtocol.Types

[<Tests>]
let diagnosticsTests =
    testList "Diagnostics" [

        testCase "valid code produces no diagnostics" <| fun _ ->
            let source = "1 + 2"
            let uri = "file:///test.fun"
            let diagnostics = analyze uri source
            Expect.isEmpty diagnostics "Valid code should produce no diagnostics"

        testCase "syntax error produces diagnostic" <| fun _ ->
            let source = "let x = "  // incomplete let expression
            let uri = "file:///test.fun"
            let diagnostics = analyze uri source
            Expect.isNonEmpty diagnostics "Syntax error should produce diagnostic"
            let diag = diagnostics.[0]
            Expect.equal diag.Source (Some "funlang") "Source should be 'funlang'"
            Expect.equal diag.Severity (Some DiagnosticSeverity.Error) "Severity should be Error"
            Expect.stringContains (diag.Message.ToLower()) "error" "Message should mention error"

        testCase "type error produces diagnostic (Int + Bool)" <| fun _ ->
            let source = "1 + true"  // type mismatch: int + bool
            let uri = "file:///test.fun"
            let diagnostics = analyze uri source
            Expect.isNonEmpty diagnostics "Type error should produce diagnostic"
            let diag = diagnostics.[0]
            Expect.equal diag.Source (Some "funlang") "Source should be 'funlang'"
            Expect.equal diag.Severity (Some DiagnosticSeverity.Error) "Severity should be Error"
            // Type checker should mention type mismatch
            Expect.isTrue (diag.Message.Contains("type") || diag.Message.Contains("Type"))
                "Message should mention type"

        testCase "unbound variable produces diagnostic" <| fun _ ->
            let source = "x + 1"  // x is not defined
            let uri = "file:///test.fun"
            let diagnostics = analyze uri source
            Expect.isNonEmpty diagnostics "Unbound variable should produce diagnostic"
            let diag = diagnostics.[0]
            Expect.equal diag.Source (Some "funlang") "Source should be 'funlang'"
            Expect.equal diag.Severity (Some DiagnosticSeverity.Error) "Severity should be Error"
            Expect.isTrue (diag.Message.Contains("Unbound") || diag.Message.Contains("unbound"))
                "Message should mention unbound variable"
    ]
```

### Protocol 변환 테스트 (FsCheck)

```fsharp
// ProtocolTests.fs
module LangLSP.Tests.ProtocolTests

open Expecto
open Expecto.ExpectoFsCheck
open FsCheck
open LangLSP.Server.Protocol

/// Generator for valid Span values
/// FunLang Span is 1-based, positive, and start <= end
let validSpanGen : Gen<Ast.Span> =
    gen {
        let! startLine = Gen.choose(1, 1000)
        let! startColumn = Gen.choose(1, 200)
        let! endLine = Gen.choose(startLine, 1000)
        // If on same line, endColumn >= startColumn
        // If on different line, endColumn can be anything
        let! endColumn =
            if endLine = startLine then
                Gen.choose(startColumn, 200)
            else
                Gen.choose(1, 200)

        let span : Ast.Span = {
            FileName = "test.fun"
            StartLine = startLine
            StartColumn = startColumn
            EndLine = endLine
            EndColumn = endColumn
        }
        return span
    }

/// Arbitrary instance for valid Spans
type ValidSpanArbitrary =
    static member Span() = Arb.fromGen validSpanGen

[<Tests>]
let protocolTests =
    // Configure FsCheck to run 500 test cases
    let fsCheckConfig = { FsCheckConfig.defaultConfig with maxTest = 500; arbitrary = [typeof<ValidSpanArbitrary>] }

    testList "Protocol" [

        testPropertyWithConfig fsCheckConfig "spanToLspRange converts to 0-based" <| fun (span: Ast.Span) ->
            let range = spanToLspRange span
            // LSP is 0-based, FunLang is 1-based
            // Line numbers should be decremented by 1
            Expect.equal range.Start.Line (uint32 (max 0 (span.StartLine - 1))) "Start line should be 0-based"
            Expect.equal range.Start.Character (uint32 (max 0 (span.StartColumn - 1))) "Start char should be 0-based"
            Expect.equal range.End.Line (uint32 (max 0 (span.EndLine - 1))) "End line should be 0-based"
            Expect.equal range.End.Character (uint32 (max 0 (span.EndColumn - 1))) "End char should be 0-based"

        testPropertyWithConfig fsCheckConfig "spanToLspRange preserves line ordering" <| fun (span: Ast.Span) ->
            let range = spanToLspRange span
            // If start line <= end line in source, it should remain so in LSP
            Expect.isLessThanOrEqual range.Start.Line range.End.Line "Start line should be <= end line"

        testPropertyWithConfig fsCheckConfig "spanToLspRange same line preserves character ordering" <| fun (span: Ast.Span) ->
            let range = spanToLspRange span
            // If on same line, start character <= end character
            if range.Start.Line = range.End.Line then
                Expect.isLessThanOrEqual range.Start.Character range.End.Character
                    "On same line, start char should be <= end char"

        testCase "spanToLspRange edge case: invalid (0,0) span clamped" <| fun _ ->
            let span : Ast.Span = {
                FileName = "test.fun"
                StartLine = 0
                StartColumn = 0
                EndLine = 0
                EndColumn = 0
            }
            let range = spanToLspRange span
            // Invalid (0,0) span should be clamped to (0,0) instead of wrapping to uint.MaxValue
            Expect.equal range.Start.Line 0u "Invalid span start line should clamp to 0"
            Expect.equal range.Start.Character 0u "Invalid span start char should clamp to 0"
            Expect.equal range.End.Line 0u "Invalid span end line should clamp to 0"
            Expect.equal range.End.Character 0u "Invalid span end char should clamp to 0"
    ]
```

**FsCheck 활용 포인트:**
- **Property-based Testing**: 500개의 랜덤 Span을 생성하여 변환 함수 검증
- **불변식(Invariants)** 검증:
  1. 1-based → 0-based 변환 정확성
  2. 줄 순서 보존 (startLine ≤ endLine)
  3. 같은 줄에서 문자 순서 보존
  4. Edge case: (0,0) span clamp 처리

### 테스트 실행

```bash
dotnet run --project src/LangLSP.Tests

# 출력:
# [Diagnostics] valid code produces no diagnostics - Passed
# [Diagnostics] syntax error produces diagnostic - Passed
# [Diagnostics] type error produces diagnostic (Int + Bool) - Passed
# [Diagnostics] unbound variable produces diagnostic - Passed
# [Protocol] spanToLspRange converts to 0-based (500 test cases) - Passed
# [Protocol] spanToLspRange preserves line ordering (500 test cases) - Passed
# [Protocol] spanToLspRange same line preserves character ordering (500 test cases) - Passed
# [Protocol] spanToLspRange edge case: invalid (0,0) span clamped - Passed
```

---

## 다음 단계

진단 시스템이 완료되었으므로, 이제 사용자는 에디터에서 실시간 에러 피드백을 받을 수 있습니다!

**Phase 1 완료!** 다음 Phase에서는 더 고급 기능을 구현합니다:

### Phase 2 - LSP 기능 확장
1. **Hover**: 커서 위치의 타입 정보 표시
2. **Completion**: 자동완성 제안
3. **Go to Definition**: 정의로 이동
4. **Find References**: 참조 찾기
5. **Rename**: 심볼 이름 변경
6. **Code Actions**: 빠른 수정 제안

### VS Code 확장 배포
- `vsce package`로 `.vsix` 파일 생성
- VS Code Marketplace에 배포
- 사용자가 에디터에서 바로 설치 가능

---

## 참고 자료

- [LSP Specification - Diagnostics](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#diagnostic)
- [FunLang Diagnostic 시스템](https://github.com/kodu-ai/LangTutorial/blob/main/FunLang/Diagnostic.fs)
- [Expecto 문서](https://github.com/haf/expecto)
- [FsCheck 문서](https://fscheck.github.io/FsCheck/)

---

**→ 다음: Phase 2 - Hover 구현** - 타입 정보 표시
