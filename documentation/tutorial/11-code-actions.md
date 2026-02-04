# Code Actions 구현하기

코드 에디터에서 전구 아이콘(💡)을 본 적이 있나요? 문제가 있는 코드에 커서를 놓으면 나타나는 "빠른 수정(Quick Fix)" 메뉴가 바로 Code Actions입니다.

이 튜토리얼에서는 FunLang LSP에 Code Actions 기능을 구현하여, 사용자가 진단(Diagnostics)에 대한 자동 수정을 받을 수 있도록 만듭니다.

## 목차

1. [Code Actions이란](#code-actions이란)
2. [LSP 프로토콜](#lsp-프로토콜)
3. [Diagnostics와의 관계](#diagnostics와의-관계)
4. [미사용 변수 감지 구현](#미사용-변수-감지-구현)
5. [QuickFix CodeAction 생성](#quickfix-codeaction-생성)
6. [handleCodeAction 구현](#handlecodeaction-구현)
7. [Server.fs 통합](#serverfs-통합)
8. [테스트 작성](#테스트-작성)
9. [확장 가능성](#확장-가능성)
10. [주의사항](#주의사항)

---

## Code Actions이란

**Code Actions**는 LSP에서 제공하는 "코드 수정 제안" 기능입니다.

### 사용 사례

| 상황 | Code Action |
|------|-------------|
| 미사용 변수 `x` | "Prefix 'x' with underscore" → `_x`로 변경 |
| Import 누락 | "Add missing import" → `import` 문 추가 |
| Typo 감지 | "Did you mean 'calculate'?" → 오타 수정 |
| 타입 오류 | "Convert to expected type" → 타입 변환 추가 |

### VS Code에서의 표시

```
test.fun
────────
1 | let x = 1 in
    ~~~~~ Warning: Unused variable 'x'
    💡 Prefix 'x' with underscore
```

사용자가:
1. 노란 밑줄에 커서를 놓으면
2. 전구 아이콘(💡) 표시
3. 클릭하면 Code Action 목록 표시
4. 선택하면 자동으로 코드 수정

### Code Action의 종류

LSP 스펙에서 정의한 `CodeActionKind`:

| Kind | 용도 |
|------|------|
| `quickfix` | 진단(diagnostic)에 대한 빠른 수정 |
| `refactor` | 리팩토링 (함수 추출, 변수 인라인 등) |
| `refactor.extract` | 코드 추출 (함수, 변수) |
| `refactor.inline` | 인라인 치환 |
| `refactor.rewrite` | 코드 재작성 |
| `source` | 소스 레벨 액션 (파일 정리, import 정렬) |
| `source.organizeImports` | Import 정리 |

우리는 `quickfix`를 중점적으로 구현합니다.

---

## LSP 프로토콜

### textDocument/codeAction 요청

클라이언트가 서버에 Code Actions를 요청하는 구조입니다.

```typescript
interface CodeActionParams {
    textDocument: TextDocumentIdentifier  // 문서 URI
    range: Range                          // 선택 영역 (또는 커서 위치)
    context: CodeActionContext            // 컨텍스트 (진단 정보 포함)
}

interface CodeActionContext {
    diagnostics: Diagnostic[]             // 이 위치의 모든 진단
    only?: CodeActionKind[]               // 요청된 액션 종류 필터
    triggerKind?: CodeActionTriggerKind   // 트리거 방식
}
```

**핵심 포인트:**
- **context.diagnostics**: 클라이언트가 해당 위치의 진단을 함께 전송
- 서버는 이 진단 정보를 기반으로 적절한 수정 제안

### CodeAction 응답

서버가 반환하는 액션 목록입니다.

```typescript
interface CodeAction {
    title: string                     // 사용자에게 표시할 제목
    kind?: CodeActionKind             // 액션 종류
    diagnostics?: Diagnostic[]        // 이 액션이 해결하는 진단
    isPreferred?: boolean             // 기본 선택 여부
    disabled?: { reason: string }     // 비활성화 사유
    edit?: WorkspaceEdit              // 수행할 편집 작업
    command?: Command                 // 실행할 명령 (edit 대신 사용 가능)
}
```

### WorkspaceEdit 구조

코드 수정을 표현하는 구조입니다.

```typescript
interface WorkspaceEdit {
    changes?: { [uri: string]: TextEdit[] }  // URI별 편집 목록
    documentChanges?: TextDocumentEdit[]     // 문서 변경 (순서 보장)
}

interface TextEdit {
    range: Range      // 수정할 범위
    newText: string   // 새 텍스트
}
```

**예시: 변수명 변경**

```fsharp
// 'x'를 '_x'로 변경하는 WorkspaceEdit
{
    changes = Map [
        ("file:///test.fun", [|
            {
                Range = { Start = { Line = 0u; Character = 4u }
                          End = { Line = 0u; Character = 5u } }
                NewText = "_x"
            }
        |])
    ]
}
```

---

## Diagnostics와의 관계

Code Actions는 **Diagnostics와 긴밀히 연결**되어 있습니다.

### 전체 흐름

```
1. 사용자가 코드 입력
   ↓
2. textDocument/didChange 알림
   ↓
3. 서버가 코드 분석
   ↓
4. textDocument/publishDiagnostics 발행
   ↓ (에디터에 노란/빨간 밑줄 표시)
5. 사용자가 진단 위치에 커서 놓음
   ↓
6. 에디터가 textDocument/codeAction 요청
   ↓ (context.diagnostics에 진단 포함)
7. 서버가 진단별 수정 제안 반환
   ↓
8. 사용자가 액션 선택
   ↓
9. 에디터가 workspace/applyEdit 요청
   ↓
10. 서버가 편집 적용 확인
```

### 진단 코드(Diagnostic Code) 활용

Diagnostics에 `code` 필드를 설정하면, Code Actions에서 진단 종류를 구분할 수 있습니다.

```fsharp
// Diagnostics.fs - 미사용 변수 진단 생성
{
    Range = spanToLspRange span
    Severity = Some DiagnosticSeverity.Warning
    Code = Some (U2.C2 "unused-variable")  // 진단 코드 설정
    Source = Some "funlang"
    Message = sprintf "Unused variable '%s'" name
    Tags = Some [| DiagnosticTag.Unnecessary |]
    RelatedInformation = None
    Data = None
}
```

```fsharp
// CodeActions.fs - 진단 코드로 액션 분기
match diag.Code with
| Some (U2.C2 "unused-variable") ->
    // 미사용 변수 → underscore prefix 액션
    createPrefixUnderscoreAction diag uri
| Some (U2.C2 "type-mismatch") ->
    // 타입 불일치 → 타입 정보 액션
    createTypeInfoAction diag
| _ ->
    // 기타 진단 → 일반 액션 또는 스킵
    None
```

---

## 미사용 변수 감지 구현

Code Actions를 제공하려면, 먼저 **미사용 변수를 감지**하는 진단이 필요합니다.

### findUnusedVariables 함수

```fsharp
// Diagnostics.fs
module LangLSP.Server.Diagnostics

open LangLSP.Server.References

/// Find unused let-bound variables in the AST
/// Returns list of (name, span) for unused variables
let findUnusedVariables (ast: Ast.Expr) : (string * Ast.Span) list =
    let unusedVars = ResizeArray<string * Ast.Span>()

    let rec traverse expr =
        match expr with
        | Ast.Let(name, value, body, span) ->
            // Skip variables prefixed with underscore (intentionally unused)
            if not (name.StartsWith("_")) then
                // Check if this variable is used in the body
                let references = collectReferences name body
                if List.isEmpty references then
                    unusedVars.Add(name, span)
            traverse value
            traverse body

        | Ast.LetRec(name, param, fnBody, inExpr, span) ->
            // Check recursive function usage
            if not (name.StartsWith("_")) then
                let referencesInBody = collectReferences name fnBody
                let referencesInExpr = collectReferences name inExpr
                if List.isEmpty referencesInBody && List.isEmpty referencesInExpr then
                    unusedVars.Add(name, span)
            // Check parameter usage
            if not (param.StartsWith("_")) then
                let paramRefs = collectReferences param fnBody
                if List.isEmpty paramRefs then
                    unusedVars.Add(param, span)
            traverse fnBody
            traverse inExpr

        | Ast.Lambda(param, body, span) ->
            if not (param.StartsWith("_")) then
                let references = collectReferences param body
                if List.isEmpty references then
                    unusedVars.Add(param, span)
            traverse body

        | Ast.LambdaAnnot(param, _, body, span) ->
            if not (param.StartsWith("_")) then
                let references = collectReferences param body
                if List.isEmpty references then
                    unusedVars.Add(param, span)
            traverse body

        // 기타 노드 순회 생략 (전체 코드는 Diagnostics.fs 참조)
        | _ -> ()

    traverse ast
    unusedVars |> Seq.toList
```

**핵심 로직:**
1. **AST 순회**: 모든 binding(Let, LetRec, Lambda) 노드 방문
2. **References 확인**: `collectReferences` 함수로 변수 사용처 검색
3. **Underscore 예외**: `_x` 형태는 의도적으로 미사용이므로 스킵
4. **Span 수집**: 미사용 변수의 위치 저장

### analyze 함수 확장

기존 `analyze` 함수에 미사용 변수 검사를 추가합니다.

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
        let typeDiags =
            match typecheckAst ast with
            | Ok _ -> []
            | Error typeDiag -> [typeDiag]

        // Check for unused variables
        let unusedVars = findUnusedVariables ast
        let unusedDiags =
            unusedVars
            |> List.map (fun (name, span) ->
                {
                    Range = spanToLspRange span
                    Severity = Some DiagnosticSeverity.Warning
                    Code = Some (U2.C2 "unused-variable")
                    CodeDescription = None
                    Source = Some "funlang"
                    Message = sprintf "Unused variable '%s'" name
                    Tags = Some [| DiagnosticTag.Unnecessary |]
                    RelatedInformation = None
                    Data = None
                })

        // Combine type errors and unused variable warnings
        typeDiags @ unusedDiags
```

**DiagnosticTag.Unnecessary의 효과:**
- VS Code에서 미사용 변수를 **흐리게(faded)** 표시
- 노란 밑줄로 경고 표시
- 전구 아이콘(💡) 표시

---

## QuickFix CodeAction 생성

미사용 변수 진단에 대한 자동 수정 액션을 생성합니다.

### createPrefixUnderscoreAction 함수

```fsharp
// CodeActions.fs
module LangLSP.Server.CodeActions

open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.Protocol

/// Create "Prefix with underscore" quickfix action for unused variables
let createPrefixUnderscoreAction (diagnostic: Diagnostic) (uri: string) : CodeAction =
    // Extract variable name from message "Unused variable 'x'"
    let message = diagnostic.Message
    let varName =
        if message.Contains("'") then
            let startIdx = message.IndexOf("'") + 1
            let endIdx = message.LastIndexOf("'")
            if endIdx > startIdx then
                message.Substring(startIdx, endIdx - startIdx)
            else
                "variable"
        else
            "variable"

    let newName = "_" + varName
    let edit = {
        Range = diagnostic.Range
        NewText = newName
    }

    let workspaceEdit = createWorkspaceEdit uri [| edit |]

    {
        Title = sprintf "Prefix '%s' with underscore" varName
        Kind = Some "quickfix"
        Diagnostics = Some [| diagnostic |]
        Edit = Some workspaceEdit
        Command = None
        IsPreferred = Some true
        Disabled = None
        Data = None
    }
```

**구현 세부사항:**

1. **변수명 추출**: 진단 메시지 "Unused variable 'x'"에서 'x' 파싱
2. **새 이름 생성**: `_x` 형태로 변경
3. **TextEdit 생성**: 진단 Range를 그대로 사용하여 변수명만 교체
4. **WorkspaceEdit 생성**: `createWorkspaceEdit` 헬퍼 사용
5. **IsPreferred = true**: VS Code에서 기본 선택으로 표시

### createWorkspaceEdit 헬퍼

```fsharp
// Protocol.fs
/// Create WorkspaceEdit from URI and list of TextEdits
let createWorkspaceEdit (uri: string) (edits: TextEdit[]) : WorkspaceEdit =
    {
        Changes = Some (Map [ (uri, edits) ])
        DocumentChanges = None
        ChangeAnnotations = None
    }
```

**간단한 헬퍼로 코드 가독성 향상.**

### createTypeInfoAction 함수

타입 오류에 대한 **정보성 액션**(편집 없음)도 추가할 수 있습니다.

```fsharp
/// Create informational action showing expected type for type errors
let createTypeInfoAction (diagnostic: Diagnostic) : CodeAction =
    // Extract expected type from diagnostic message if present
    // Example: "Type mismatch: expected Int, got Bool"
    let message = diagnostic.Message
    let typeInfo =
        if message.Contains("expected") then
            let startIdx = message.IndexOf("expected")
            message.Substring(startIdx)
        else
            "Type mismatch - see diagnostic message"

    {
        Title = sprintf "Info: %s" typeInfo
        Kind = Some "quickfix"
        Diagnostics = Some [| diagnostic |]
        Edit = None  // Informational only, no automatic fix
        Command = None
        IsPreferred = None
        Disabled = None
        Data = None
    }
```

**Edit = None**: 코드 변경 없이 정보만 제공하는 액션.

---

## handleCodeAction 구현

`textDocument/codeAction` 요청을 처리하는 핸들러입니다.

### 전체 구현

```fsharp
/// Handle textDocument/codeAction request
/// Returns code actions (quickfixes) for diagnostics at cursor position
let handleCodeAction (p: CodeActionParams) : Async<CodeAction[] option> =
    async {
        let uri = p.TextDocument.Uri
        let diagnostics = p.Context.Diagnostics

        if Array.isEmpty diagnostics then
            return None
        else
            let actions = ResizeArray<CodeAction>()

            for diag in diagnostics do
                // Check diagnostic code/message to determine action type
                match diag.Code with
                | Some (U2.C2 "unused-variable") ->
                    // ACTION-01: Prefix with underscore
                    let action = createPrefixUnderscoreAction diag uri
                    actions.Add(action)

                | _ ->
                    // Check if it's a type error (severity Error)
                    if diag.Severity = Some DiagnosticSeverity.Error then
                        // ACTION-02: Informational type hint
                        let action = createTypeInfoAction diag
                        actions.Add(action)

            if actions.Count > 0 then
                return Some (actions.ToArray())
            else
                return None
    }
```

**처리 흐름:**
1. **진단 확인**: `context.diagnostics`가 비어있으면 None 반환
2. **진단별 액션 생성**:
   - 진단 코드가 `"unused-variable"` → Prefix 액션
   - 타입 에러 → Type info 액션
3. **액션 배열 반환**: 여러 진단에 대한 액션 모두 반환

### U2 타입 처리

Ionide LSP 라이브러리는 `Code` 필드를 `U2<int, string>` 타입으로 정의합니다.

```fsharp
// Ionide 타입 정의
type Diagnostic = {
    Code: U2<int, string> option
    // ...
}
```

**패턴 매칭:**
- `U2.C1 n`: int 값 (예: `123`)
- `U2.C2 s`: string 값 (예: `"unused-variable"`)

우리는 string 코드를 사용하므로 `U2.C2` 케이스를 매칭합니다.

---

## Server.fs 통합

LSP 서버에 Code Actions 핸들러를 등록합니다.

### 서버 초기화 시 Capability 선언

```fsharp
// Server.fs - initialize 함수
let initialize (p: InitializeParams) : Async<InitializeResult> =
    async {
        Log.Information("Client initialized: {ClientInfo}", p.ClientInfo)

        let result: InitializeResult = {
            Capabilities = {
                // 기존 capabilities
                TextDocumentSync = Some textDocSync
                HoverProvider = Some (U2.C1 true)
                DefinitionProvider = Some (U2.C1 true)
                CompletionProvider = Some completionOptions
                ReferencesProvider = Some (U2.C1 true)
                RenameProvider = Some (U2.C2 renameOptions)

                // Code Actions 추가
                CodeActionProvider = Some (U2.C2 {
                    CodeActionKinds = Some [| "quickfix" |]
                    ResolveProvider = Some false
                })

                // 기타 capabilities
                // ...
            }
            ServerInfo = Some serverInfo
        }
        return result
    }
```

**CodeActionProvider 설정:**
- **CodeActionKinds**: 지원하는 액션 종류 선언 (`["quickfix"]`)
- **ResolveProvider = false**: 모든 정보를 즉시 반환 (resolve 단계 없음)

### 요청 핸들러 등록

```fsharp
// Server.fs - main 함수
[<EntryPoint>]
let main argv =
    // Serilog 설정 및 LSP 서버 생성 (생략)

    // 핸들러 등록
    server.RegisterHandler(
        "textDocument/codeAction",
        Func<CodeActionParams, CancellationToken, Task<CodeAction[] option>>(fun p ct ->
            CodeActions.handleCodeAction p |> Async.StartAsTask
        ),
        jsonOptions
    )

    // 서버 시작
    server.StartAsync().Wait()
    0
```

**중요:** `CodeAction[] option` 반환 타입 유지 (None이면 액션 없음).

---

## 테스트 작성

Expecto를 사용하여 Code Actions 기능을 테스트합니다.

### 기본 테스트 구조

```fsharp
// CodeActionsTests.fs
module LangLSP.Tests.CodeActionsTests

open Expecto
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.CodeActions
open LangLSP.Server.Diagnostics
open LangLSP.Server.DocumentSync

[<Tests>]
let codeActionsTests =
    testList "CodeActions" [
        // 테스트 케이스들
    ]
```

### Test Case 1: 미사용 변수 액션

```fsharp
testCase "unused variable returns prefix underscore action" <| fun _ ->
    let uri = "file:///test.fun"
    let source = "let x = 1 in 2"  // 'x' is unused

    // Create diagnostic (normally from Diagnostics module)
    let diagnostic = {
        Range = { Start = { Line = 0u; Character = 4u }
                  End = { Line = 0u; Character = 5u } }
        Severity = Some DiagnosticSeverity.Warning
        Code = Some (U2.C2 "unused-variable")
        Source = Some "funlang"
        Message = "Unused variable 'x'"
        Tags = Some [| DiagnosticTag.Unnecessary |]
        CodeDescription = None
        RelatedInformation = None
        Data = None
    }

    let params = {
        TextDocument = { Uri = uri }
        Range = diagnostic.Range
        Context = {
            Diagnostics = [| diagnostic |]
            Only = None
            TriggerKind = None
        }
    }

    let result = handleCodeAction params |> Async.RunSynchronously

    match result with
    | None ->
        failtest "Expected code action, got None"
    | Some actions ->
        Expect.equal actions.Length 1 "Should have 1 action"
        let action = actions.[0]
        Expect.equal action.Kind (Some "quickfix") "Should be quickfix"
        Expect.stringContains action.Title "underscore" "Title should mention underscore"
        Expect.isSome action.Edit "Should have edit"

        match action.Edit with
        | Some edit ->
            match edit.Changes with
            | Some changes ->
                let edits = changes.[uri]
                Expect.equal edits.Length 1 "Should have 1 edit"
                Expect.equal edits.[0].NewText "_x" "Should prefix with underscore"
            | None -> failtest "Expected changes in edit"
        | None -> failtest "Expected edit"
```

### Test Case 2: 진단 없으면 액션 없음

```fsharp
testCase "no diagnostics returns None" <| fun _ ->
    let uri = "file:///test.fun"
    let params = {
        TextDocument = { Uri = uri }
        Range = { Start = { Line = 0u; Character = 0u }
                  End = { Line = 0u; Character = 1u } }
        Context = {
            Diagnostics = [||]  // Empty
            Only = None
            TriggerKind = None
        }
    }

    let result = handleCodeAction params |> Async.RunSynchronously

    Expect.isNone result "No diagnostics should return None"
```

### Test Case 3: 타입 오류 정보 액션

```fsharp
testCase "type error returns info action" <| fun _ ->
    let uri = "file:///test.fun"
    let diagnostic = {
        Range = { Start = { Line = 0u; Character = 4u }
                  End = { Line = 0u; Character = 8u } }
        Severity = Some DiagnosticSeverity.Error
        Code = None
        Source = Some "funlang"
        Message = "Type mismatch: expected Int, got Bool"
        Tags = None
        CodeDescription = None
        RelatedInformation = None
        Data = None
    }

    let params = {
        TextDocument = { Uri = uri }
        Range = diagnostic.Range
        Context = {
            Diagnostics = [| diagnostic |]
            Only = None
            TriggerKind = None
        }
    }

    let result = handleCodeAction params |> Async.RunSynchronously

    match result with
    | None ->
        failtest "Expected code action, got None"
    | Some actions ->
        Expect.isNonEmpty actions "Should have actions"
        let action = actions.[0]
        Expect.equal action.Kind (Some "quickfix") "Should be quickfix"
        Expect.stringContains action.Title "expected" "Should show expected type"
        Expect.isNone action.Edit "Info action should not have edit"
```

### 통합 테스트: Diagnostics → CodeActions 흐름

```fsharp
testCase "full flow: analyze produces diagnostic, code action fixes it" <| fun _ ->
    let uri = "file:///test.fun"
    let source = "let unused = 42 in 100"

    // 1. Analyze to get diagnostics
    let diagnostics = Diagnostics.analyze uri source
    Expect.isNonEmpty diagnostics "Should have unused variable diagnostic"

    let unusedDiag = diagnostics |> List.find (fun d ->
        d.Code = Some (U2.C2 "unused-variable"))

    // 2. Request code actions for that diagnostic
    let params = {
        TextDocument = { Uri = uri }
        Range = unusedDiag.Range
        Context = {
            Diagnostics = [| unusedDiag |]
            Only = None
            TriggerKind = None
        }
    }

    let result = handleCodeAction params |> Async.RunSynchronously

    // 3. Verify action exists and is correct
    match result with
    | Some actions ->
        Expect.isNonEmpty actions "Should have code action"
        let action = actions.[0]
        Expect.equal action.Title "Prefix 'unused' with underscore" "Correct title"

        match action.Edit with
        | Some edit ->
            match edit.Changes with
            | Some changes ->
                let edits = changes.[uri]
                Expect.equal edits.[0].NewText "_unused" "Should fix variable name"
            | None -> failtest "Expected changes"
        | None -> failtest "Expected edit"
    | None ->
        failtest "Expected code action"
```

### 테스트 실행

```bash
dotnet run --project src/LangLSP.Tests

# 출력:
# [CodeActions] unused variable returns prefix underscore action - Passed
# [CodeActions] no diagnostics returns None - Passed
# [CodeActions] type error returns info action - Passed
# [CodeActions] full flow: analyze produces diagnostic, code action fixes it - Passed
```

---

## 확장 가능성

Code Actions 시스템은 매우 확장 가능합니다. 다음과 같은 액션을 추가할 수 있습니다:

### 1. Refactoring Actions

```fsharp
/// Extract expression to variable
let createExtractVariableAction (range: Range) (uri: string) : CodeAction =
    {
        Title = "Extract to variable"
        Kind = Some "refactor.extract"
        Diagnostics = None
        Edit = Some (createExtractionEdit range uri)
        Command = None
        IsPreferred = None
        Disabled = None
        Data = None
    }
```

### 2. Import/Module Actions

```fsharp
/// Add missing module import
let createAddImportAction (moduleName: string) (uri: string) : CodeAction =
    {
        Title = sprintf "Import module '%s'" moduleName
        Kind = Some "quickfix"
        Diagnostics = None
        Edit = Some (createImportEdit moduleName uri)
        Command = None
        IsPreferred = Some true
        Disabled = None
        Data = None
    }
```

### 3. Type Annotation Actions

```fsharp
/// Add explicit type annotation
let createAddTypeAnnotationAction (varName: string) (inferredType: string) (range: Range) (uri: string) : CodeAction =
    let newText = sprintf "%s : %s" varName inferredType
    {
        Title = sprintf "Add type annotation ': %s'" inferredType
        Kind = Some "refactor.rewrite"
        Diagnostics = None
        Edit = Some (createWorkspaceEdit uri [| { Range = range; NewText = newText } |])
        Command = None
        IsPreferred = None
        Disabled = None
        Data = None
    }
```

### 4. Source-level Actions

```fsharp
/// Organize imports/declarations
let createOrganizeImportsAction (uri: string) : CodeAction =
    {
        Title = "Organize imports"
        Kind = Some "source.organizeImports"
        Diagnostics = None
        Edit = None
        Command = Some {
            Title = "Organize"
            Command = "funlang.organizeImports"
            Arguments = Some [| uri |]
        }
        IsPreferred = None
        Disabled = None
        Data = None
    }
```

### 5. Command-based Actions

Edit 대신 Command를 사용하면 복잡한 다단계 작업이 가능합니다.

```fsharp
/// Run auto-formatter
let createFormatAction (uri: string) : CodeAction =
    {
        Title = "Format document"
        Kind = Some "source"
        Diagnostics = None
        Edit = None
        Command = Some {
            Title = "Format"
            Command = "funlang.formatDocument"
            Arguments = Some [| uri |]
        }
        IsPreferred = None
        Disabled = None
        Data = None
    }
```

---

## 주의사항

### 1. Range 정확성

**문제:** TextEdit의 Range가 부정확하면 잘못된 위치를 수정합니다.

```fsharp
// ❌ 잘못된 예: 전체 Let 표현식 범위 사용
let edit = {
    Range = letExprSpan |> spanToLspRange  // "let x = 1 in x" 전체
    NewText = "_x"  // 전체를 "_x"로 교체!
}

// ✅ 올바른 예: 변수명만 정확히 타겟팅
let edit = {
    Range = diagnostic.Range  // 변수명 "x"만
    NewText = "_x"
}
```

**해결:** Diagnostics에서 정확한 Range를 설정하거나, `findNameInSource`로 이름 위치를 찾습니다.

### 2. WorkspaceEdit vs Command

**WorkspaceEdit**: 단순 텍스트 교체에 적합
```fsharp
{ Edit = Some workspaceEdit; Command = None }
```

**Command**: 복잡한 로직 필요 시 사용
```fsharp
{ Edit = None; Command = Some { Command = "funlang.customAction"; Arguments = ... } }
```

**주의:** Command 사용 시 클라이언트가 해당 명령을 등록해야 합니다.

### 3. IsPreferred 사용

```fsharp
{
    Title = "Prefix with underscore"
    IsPreferred = Some true  // VS Code에서 기본 선택
    // ...
}
```

여러 액션이 있을 때, 가장 권장되는 액션에만 `IsPreferred = true` 설정.

### 4. Multiple Diagnostics

같은 위치에 여러 진단이 있을 수 있습니다.

```fsharp
let handleCodeAction (p: CodeActionParams) : Async<CodeAction[] option> =
    async {
        let actions = ResizeArray<CodeAction>()

        for diag in p.Context.Diagnostics do
            // 각 진단별로 액션 생성
            match diag.Code with
            | Some code -> actions.Add(createActionForCode code diag p.TextDocument.Uri)
            | None -> ()

        if actions.Count > 0 then
            return Some (actions.ToArray())
        else
            return None
    }
```

### 5. 진단 코드 일관성

Diagnostics와 CodeActions에서 같은 코드를 사용해야 합니다.

```fsharp
// Diagnostics.fs
Code = Some (U2.C2 "unused-variable")

// CodeActions.fs
match diag.Code with
| Some (U2.C2 "unused-variable") -> // 같은 문자열!
```

**권장:** 코드 상수를 공유 모듈에 정의
```fsharp
// DiagnosticCodes.fs
module LangLSP.Server.DiagnosticCodes

[<Literal>]
let UnusedVariable = "unused-variable"

[<Literal>]
let TypeMismatch = "type-mismatch"
```

### 6. Async 처리

```fsharp
let handleCodeAction (p: CodeActionParams) : Async<CodeAction[] option> =
    async {
        // 비동기 작업 (예: 파일 읽기, AST 파싱)
        // ...
    }
```

현재는 동기적이지만, 미래 확장성을 위해 Async 유지.

### 7. None vs Some [||]

```fsharp
// ✅ 액션 없음
return None

// ❌ 빈 배열 반환 (비효율적, 일부 클라이언트에서 문제 가능)
return Some [||]
```

LSP 스펙에서는 액션이 없으면 `null` (F#에서 None) 반환 권장.

---

## 다음 단계

Code Actions 구현이 완료되었습니다! 이제 사용자는:

1. **자동 수정**: 미사용 변수를 클릭 한 번으로 수정
2. **타입 힌트**: 타입 오류에 대한 상세 정보 확인
3. **생산성 향상**: 반복적인 수정 작업 자동화

### Phase 4 완료!

**구현한 고급 기능:**
- ✅ Find References (참조 찾기)
- ✅ Rename (이름 변경)
- ✅ Code Actions (코드 수정)
- ✅ Unused Variable Detection (미사용 변수 감지)
- ✅ DiagnosticTag.Unnecessary (흐림 처리)

### 추가 개선 아이디어

1. **더 많은 QuickFix**:
   - 타입 불일치 → 타입 캐스팅 추가
   - 미정의 변수 → let 바인딩 제안
   - Import 누락 → 자동 import 추가

2. **Refactoring Actions**:
   - Extract Function (함수 추출)
   - Inline Variable (변수 인라인)
   - Rename All Occurrences (모든 사용처 이름 변경)

3. **Source Actions**:
   - Format Document (문서 포맷팅)
   - Remove Unused Imports (미사용 import 제거)
   - Sort Definitions (정의 정렬)

4. **CodeAction Resolve**:
   - 복잡한 액션은 두 단계로 처리 (목록 → resolve → edit)
   - 성능 최적화 (많은 액션이 있을 때)

---

## 참고 자료

- [LSP Specification - Code Action](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_codeAction)
- [LSP Specification - Workspace Edit](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#workspaceEdit)
- [VS Code Code Actions Guide](https://code.visualstudio.com/api/language-extensions/programmatic-language-features#provide-code-actions)
- [Ionide.LanguageServerProtocol Types](https://github.com/ionide/LanguageServerProtocol/blob/main/src/LanguageServerProtocol/Types.fs)

---

**→ 다음: Phase 5 - Documentation** - 프로젝트 문서화 및 배포
