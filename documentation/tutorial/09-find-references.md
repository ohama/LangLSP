# Find References 구현하기

이 문서는 LSP 서버에서 **Find References** 기능을 구현하는 방법을 설명합니다. 변수나 함수를 선택하고 "Find References"를 실행하면 해당 심볼이 사용된 모든 위치를 찾아줍니다. 이 기능은 코드 리팩토링과 영향 분석(Impact Analysis)에 필수적입니다.

## 목차

1. [Find References란](#find-references란)
2. [LSP 프로토콜](#lsp-프로토콜)
3. [Definition과의 관계](#definition과의-관계)
4. [레퍼런스 수집 구현](#레퍼런스-수집-구현)
5. [스코프 인식 레퍼런스 수집](#스코프-인식-레퍼런스-수집)
6. [변수 섀도잉 처리](#변수-섀도잉-처리)
7. [핸들러 구현](#핸들러-구현)
8. [Server.fs 통합](#serverfs-통합)
9. [테스트 작성](#테스트-작성)
10. [Definition vs References 비교](#definition-vs-references-비교)

---

## Find References란

**Find References**는 선택한 심볼의 모든 사용처를 찾는 기능입니다. Go to Definition이 "정의 위치" 하나를 찾는다면, Find References는 "사용 위치" 여러 개를 찾습니다.

### VS Code에서의 사용

```
test.fun
────────
1 | let add = fun x -> fun y -> x + y in
2 | let result = add 1 2 in
3 | let result2 = add 3 4 in
        ^^^
        [Shift+F12] 또는 우클릭 → Find All References
        → add가 사용된 모든 위치 표시 (2번, 3번 라인)
```

**트리거 방법:**
- `Shift+F12` 키
- 우클릭 → "Find All References"
- 좌측 패널에 결과 목록 표시

### 핵심 동작

1. 사용자가 커서를 심볼 위에 놓음
2. 클라이언트가 `textDocument/references` 요청 전송
3. 서버가 해당 심볼의 **모든 사용 위치** 반환 (배열)
4. 클라이언트가 결과 패널에 위치 목록 표시

### 활용 사례

| 시나리오 | 사용 예시 |
|----------|----------|
| **리팩토링 전 영향 분석** | 함수명을 바꾸기 전에 사용처 확인 |
| **사용하지 않는 코드 찾기** | 레퍼런스가 0개면 미사용 함수 |
| **의존성 파악** | 특정 변수가 어디서 사용되는지 추적 |
| **버그 추적** | 잘못된 값이 어디로 전파되는지 확인 |

---

## LSP 프로토콜

### textDocument/references 요청

```typescript
interface ReferenceParams {
    textDocument: TextDocumentIdentifier  // { uri: "file:///test.fun" }
    position: Position                     // { line: 0, character: 4 }
    context: ReferenceContext             // { includeDeclaration: true }
}

interface ReferenceContext {
    includeDeclaration: boolean  // 정의 위치도 결과에 포함할지 여부
}
```

**요청 예시:**
```json
{
    "jsonrpc": "2.0",
    "id": 7,
    "method": "textDocument/references",
    "params": {
        "textDocument": { "uri": "file:///test.fun" },
        "position": { "line": 0, "character": 4 },
        "context": { "includeDeclaration": true }
    }
}
```

### includeDeclaration의 의미

| 값 | 동작 | 예시 |
|----|------|------|
| `true` | 정의 + 사용처 모두 반환 | `let x = 1` 포함 |
| `false` | 사용처만 반환 | `x + 1`만 포함, 정의는 제외 |

대부분의 경우 `true`가 유용합니다 (전체 영향 범위 파악).

### 응답 형식

```typescript
// Location 배열 반환
type ReferenceResponse = Location[] | null

interface Location {
    uri: string
    range: Range
}
```

**응답 예시:**
```json
{
    "jsonrpc": "2.0",
    "id": 7,
    "result": [
        {
            "uri": "file:///test.fun",
            "range": {
                "start": { "line": 0, "character": 4 },
                "end": { "line": 0, "character": 7 }
            }
        },
        {
            "uri": "file:///test.fun",
            "range": {
                "start": { "line": 1, "character": 13 },
                "end": { "line": 1, "character": 16 }
            }
        }
    ]
}
```

### Definition과의 차이점

| 기능 | Definition | References |
|------|------------|------------|
| 반환값 | `Location` (단일) | `Location[]` (배열) |
| 의미 | 심볼이 **정의된** 곳 | 심볼이 **사용된** 곳 |
| 개수 | 최대 1개 | 0개 이상 여러 개 |
| 포함 범위 | 정의 위치만 | 정의 위치는 context에 따라 선택적 |

---

## Definition과의 관계

Find References는 Go to Definition의 **역방향** 동작입니다.

### 상호 관계

```
┌─────────────────────────────────────────┐
│  let add = fun x -> fun y -> x + y in   │  ← 정의 위치 (Definition)
│      ^^^                                 │
│       │                                  │
│       │ Go to Definition (F12)           │
│       │                                  │
│  let result = add 1 2 in                 │  ← 사용 위치 1 (Reference)
│               ^^^                        │
│                │                         │
│                ↑                         │
│  let result2 = add 3 4 in                │  ← 사용 위치 2 (Reference)
│                ^^^                       │
│                 └─────────────────────┐  │
│                   Find References (Shift+F12)
└─────────────────────────────────────────┘
```

### Definition과 References의 데이터 흐름

```
┌──────────────────────────────────────────────┐
│ 1. 정의 찾기 (Go to Definition)              │
│    커서 위치 → 정의 위치 (1개)               │
└──────────────────────────────────────────────┘
                    ↓
┌──────────────────────────────────────────────┐
│ 2. 레퍼런스 찾기 (Find References)            │
│    커서 위치 → 정의 확인 → 사용 위치 (N개)   │
└──────────────────────────────────────────────┘
```

Find References 구현 시 **Definition 모듈을 재사용**합니다:
1. 커서 위치에서 심볼의 정의를 먼저 찾음 (`findDefinitionForVar`)
2. 그 정의를 참조하는 모든 Var 노드를 수집
3. 섀도잉을 고려하여 올바른 레퍼런스만 필터링

---

## 레퍼런스 수집 구현

### collectReferences 함수 (기본 버전)

먼저 **섀도잉 미고려** 버전을 구현합니다. 단순히 이름이 일치하는 모든 Var 노드를 수집합니다.

```fsharp
// References.fs
module LangLSP.Server.References

open Ast

/// Collect all variable references (Var nodes) matching a symbol name
/// Traverses entire AST and returns list of spans for all Var nodes with matching name
let collectReferences (varName: string) (ast: Expr) : Span list =
    let refs = ResizeArray<Span>()

    let rec traverse expr =
        match expr with
        | Var(name, span) when name = varName ->
            // 이름이 일치하는 변수 사용 발견
            refs.Add(span)

        | Let(_, value, body, _) ->
            traverse value
            traverse body

        | LetRec(_, _, fnBody, inExpr, _) ->
            traverse fnBody
            traverse inExpr

        | Lambda(_, body, _) ->
            traverse body

        | LambdaAnnot(_, _, body, _) ->
            traverse body

        | LetPat(_, value, body, _) ->
            traverse value
            traverse body

        | Match(scrutinee, clauses, _) ->
            traverse scrutinee
            for (_, clauseBody) in clauses do
                traverse clauseBody

        | App(fn, arg, _) ->
            traverse fn
            traverse arg

        | If(cond, thenExpr, elseExpr, _) ->
            traverse cond
            traverse thenExpr
            traverse elseExpr

        | Add(l, r, _) | Subtract(l, r, _) | Multiply(l, r, _) | Divide(l, r, _)
        | Equal(l, r, _) | NotEqual(l, r, _) | LessThan(l, r, _) | GreaterThan(l, r, _)
        | LessEqual(l, r, _) | GreaterEqual(l, r, _) | And(l, r, _) | Or(l, r, _)
        | Cons(l, r, _) ->
            traverse l
            traverse r

        | Negate(e, _) | Annot(e, _, _) ->
            traverse e

        | Tuple(exprs, _) | List(exprs, _) ->
            exprs |> List.iter traverse

        | Number _ | Bool _ | String _ | EmptyList _ | Var _ -> ()

    traverse ast
    refs |> Seq.toList
```

### 동작 원리

1. **ResizeArray 사용**: 효율적인 append를 위해 mutable 리스트 사용
2. **재귀 순회**: 모든 하위 표현식 탐색
3. **Var 노드 매칭**: `Var(name, span) when name = varName` 패턴으로 이름 일치 검사
4. **Span 수집**: 일치하는 Var 노드의 위치 정보 저장

### 예시

```funlang
let x = 1 in
  let y = x + 2 in
    x + y
```

`collectReferences "x" ast` 호출 시:
- 결과: `[Span(1, 10, 1, 11), Span(2, 4, 2, 5)]`
- 2번 라인의 `x + 2`와 3번 라인의 `x + y`에서 발견

---

## 스코프 인식 레퍼런스 수집

### 섀도잉 문제

기본 `collectReferences`는 **이름만** 체크하므로 섀도잉 처리가 안 됩니다.

```funlang
let x = 1 in           (* 정의 A *)
  let x = 2 in         (* 정의 B - A를 shadow *)
    x + 1              (* 이 x는 B를 참조 *)
```

사용자가 정의 A에서 "Find References"를 실행하면:
- **원하는 결과**: 빈 배열 (정의 A는 shadowed되어 사용되지 않음)
- **기본 구현 결과**: 3번 라인의 `x` 포함 (잘못됨!)

### collectReferencesForBinding 함수

**특정 정의**를 참조하는 Var 노드만 찾는 함수입니다.

```fsharp
// References.fs
open LangLSP.Server.Definition

/// Collect references to a SPECIFIC binding (shadowing-aware)
/// Only returns Var nodes that resolve to the definition at defSpan
let collectReferencesForBinding (varName: string) (defSpan: Span) (ast: Expr) : Span list =
    // 1. 이름이 일치하는 모든 Var 노드 찾기
    let allRefs = collectReferences varName ast

    // 2. 각 Var 노드가 우리의 정의를 참조하는지 확인
    allRefs
    |> List.filter (fun refSpan ->
        // Var 노드의 위치로 Position 생성
        let pos : Position = {
            Line = uint32 refSpan.StartLine
            Character = uint32 refSpan.StartColumn
        }
        // 이 Var 노드가 실제로 우리의 정의를 참조하는지 확인
        match findDefinitionForVar varName ast pos with
        | Some foundDefSpan ->
            // 찾은 정의가 우리가 찾는 정의와 같은지 비교
            foundDefSpan.StartLine = defSpan.StartLine &&
            foundDefSpan.StartColumn = defSpan.StartColumn
        | None -> false
    )
```

### 알고리즘 설명

```
┌─────────────────────────────────────────────┐
│ 1. collectReferences로 후보 수집            │
│    "x"라는 이름의 모든 Var 노드              │
└─────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────┐
│ 2. 각 Var 노드마다 정의 검색                 │
│    findDefinitionForVar 호출                 │
└─────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────┐
│ 3. 정의 위치 비교                            │
│    찾은 정의 == 타겟 정의?                   │
└─────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────┐
│ 4. 일치하는 것만 반환                        │
│    필터링된 Span 리스트                      │
└─────────────────────────────────────────────┘
```

### 왜 Definition 모듈을 재사용하는가?

`findDefinitionForVar`는 **섀도잉 해결 로직**을 이미 구현했습니다:
- 사용 위치 이전의 정의만 고려
- 가장 가까운 (내부) 정의 선택
- 렉시컬 스코핑 규칙 적용

Find References는 이 로직을 **여러 번 호출**하여 각 Var 노드가 올바른 정의를 참조하는지 검증합니다.

---

## 변수 섀도잉 처리

### 구체적 예시

```funlang
let x = 1 in           (* 정의 A: (0, 4) *)
  let x = 2 in         (* 정의 B: (1, 6) *)
    x                  (* 사용 1: (2, 4) *)
  in x                 (* 사용 2: (3, 5) *)
```

**정의 A에서 "Find References" 실행:**

1. `collectReferences "x"` 호출:
   - 결과: `[Span(2,4), Span(3,5)]` (사용 1, 사용 2)

2. 사용 1 검증 (`Span(2,4)`):
   - `findDefinitionForVar "x" ast (2,4)` 호출
   - 결과: `Span(1,6)` (정의 B)
   - `(1,6) != (0,4)` → **필터 아웃**

3. 사용 2 검증 (`Span(3,5)`):
   - `findDefinitionForVar "x" ast (3,5)` 호출
   - 결과: `Span(0,4)` (정의 A)
   - `(0,4) == (0,4)` → **포함**

4. 최종 결과: `[Span(3,5)]` (사용 2만 해당)

**정의 B에서 "Find References" 실행:**

1. `collectReferences "x"` 호출:
   - 결과: `[Span(2,4), Span(3,5)]`

2. 사용 1 검증:
   - 정의: `Span(1,6)` (정의 B)
   - `(1,6) == (1,6)` → **포함**

3. 사용 2 검증:
   - 정의: `Span(0,4)` (정의 A)
   - `(0,4) != (1,6)` → **필터 아웃**

4. 최종 결과: `[Span(2,4)]` (사용 1만 해당)

### 복잡한 중첩 예시

```funlang
let x = 1 in                    (* 정의 A: (0, 4) *)
  (let x = 2 in x) +            (* 정의 B: (1, 7), 사용 1: (1, 17) *)
  (let x = 3 in x) +            (* 정의 C: (2, 7), 사용 2: (2, 17) *)
  x                             (* 사용 3: (3, 2) *)
```

**정의 A에서 Find References:**
- 사용 1: B를 참조 → 제외
- 사용 2: C를 참조 → 제외
- 사용 3: A를 참조 → **포함**
- 결과: `[Span(3,2)]`

**정의 B에서 Find References:**
- 사용 1: B를 참조 → **포함**
- 사용 2: C를 참조 → 제외
- 사용 3: A를 참조 → 제외
- 결과: `[Span(1,17)]`

---

## 핸들러 구현

### handleReferences 함수

```fsharp
// References.fs
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.AstLookup
open LangLSP.Server.DocumentSync
open LangLSP.Server.Protocol

/// Handle textDocument/references request
/// Returns all locations where a symbol is referenced
let handleReferences (p: ReferenceParams) : Async<Location[] option> =
    async {
        let uri = p.TextDocument.Uri
        let pos = p.Position

        match getDocument uri with
        | None -> return None
        | Some text ->
            try
                // 1. 파싱
                let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(text)
                let ast = Parser.start Lexer.tokenize lexbuf

                // 2. 커서 위치의 노드 찾기
                match findNodeAtPosition pos ast with
                | None -> return None
                | Some node ->
                    // 3. 노드에서 심볼 이름과 정의 위치 추출
                    let identOpt, defSpanOpt =
                        match node with
                        | Var(name, _) ->
                            // 변수 사용처 - 정의 찾기
                            let def = findDefinitionForVar name ast pos
                            (Some name, def)
                        | Let(name, _, _, span) ->
                            // Let 바인딩 자체
                            (Some name, Some span)
                        | Lambda(param, _, span) ->
                            // 람다 파라미터
                            (Some param, Some span)
                        | LambdaAnnot(param, _, _, span) ->
                            // 타입 어노테이션이 있는 람다
                            (Some param, Some span)
                        | LetRec(name, _, _, _, span) ->
                            // 재귀 함수
                            (Some name, Some span)
                        | _ ->
                            // 레퍼런스를 찾을 수 없는 노드
                            (None, None)

                    match identOpt, defSpanOpt with
                    | Some varName, Some defSpan ->
                        // 4. 섀도잉 인식 레퍼런스 수집
                        let refSpans = collectReferencesForBinding varName defSpan ast

                        // 5. includeDeclaration 처리
                        let allSpans =
                            if p.Context.IncludeDeclaration then
                                defSpan :: refSpans
                            else
                                refSpans

                        // 6. Location[] 변환
                        let locations =
                            allSpans
                            |> List.map (fun span ->
                                {
                                    Uri = uri
                                    Range = spanToLspRange span
                                })
                            |> Array.ofList

                        return Some locations

                    | Some varName, None ->
                        // 정의를 찾지 못한 경우 (드물지만 가능)
                        // 섀도잉 무시하고 이름만으로 수집
                        let refSpans = collectReferences varName ast

                        let locations =
                            refSpans
                            |> List.map (fun span ->
                                {
                                    Uri = uri
                                    Range = spanToLspRange span
                                })
                            |> Array.ofList

                        return Some locations

                    | _ ->
                        // 심볼을 찾을 수 없음
                        return None

            with _ ->
                // 파싱 실패
                return None
    }
```

### 동작 흐름

```
사용자 Shift+F12 키
     │
     ▼
┌─────────────────────────────────────┐
│ 1. getDocument로 소스 텍스트 획득   │
└─────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────────────┐
│ 2. Parser로 AST 생성                │
└─────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────────────┐
│ 3. findNodeAtPosition               │
│    커서 위치의 AST 노드 찾기        │
└─────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────────────┐
│ 4. 노드 타입에 따라 처리            │
│    - Var: 정의 찾기                 │
│    - Let/Lambda: 직접 정의          │
└─────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────────────┐
│ 5. collectReferencesForBinding      │
│    섀도잉 인식 레퍼런스 수집        │
└─────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────────────┐
│ 6. includeDeclaration 처리          │
│    정의 위치 포함 여부 결정         │
└─────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────────────┐
│ 7. Location[] 반환                  │
│    URI + Range 배열                 │
└─────────────────────────────────────┘
```

### 노드 타입별 처리

| 노드 타입 | 의미 | 정의 위치 추출 |
|-----------|------|----------------|
| `Var(name, span)` | 변수 사용 | `findDefinitionForVar` 호출 |
| `Let(name, _, _, span)` | let 바인딩 | `span` 직접 사용 |
| `Lambda(param, _, span)` | 람다 파라미터 | `span` 직접 사용 |
| `LambdaAnnot(param, _, _, span)` | 타입 어노테이션 람다 | `span` 직접 사용 |
| `LetRec(name, _, _, _, span)` | 재귀 함수 | `span` 직접 사용 |
| 기타 | 연산자, 리터럴 등 | None (레퍼런스 없음) |

### includeDeclaration 처리

```fsharp
let allSpans =
    if p.Context.IncludeDeclaration then
        defSpan :: refSpans  // 정의 위치를 리스트 앞에 추가
    else
        refSpans             // 사용처만
```

**VS Code 동작:**
- 일반적으로 `includeDeclaration: true`로 요청
- 결과 패널에서 정의 위치가 첫 번째 항목으로 표시됨

---

## Server.fs 통합

### 서버 기능 등록

```fsharp
// Server.fs
let serverCapabilities : ServerCapabilities =
    { ServerCapabilities.Default with
        TextDocumentSync = ...
        HoverProvider = Some (U2.C1 true)
        DefinitionProvider = Some (U2.C1 true)
        ReferencesProvider = Some (U2.C1 true)  // 추가!
    }
```

### 핸들러 등록

```fsharp
// Server.fs
module Handlers =
    /// Handle textDocument/references request
    let textDocumentReferences (p: ReferenceParams) : Async<Location[] option> =
        handleReferences p
```

### 전체 연결

클라이언트가 `initialize` 요청을 보내면:
1. 서버가 `ReferencesProvider = true` 응답
2. 클라이언트는 이제 References 기능 사용 가능

클라이언트가 `textDocument/references` 요청을 보내면:
1. 서버가 `Handlers.textDocumentReferences` 호출
2. References.fs의 `handleReferences` 실행
3. `Location[]` 또는 `null` 반환

---

## 테스트 작성

### 기본 테스트 구조

```fsharp
// ReferencesTests.fs
module LangLSP.Tests.ReferencesTests

open Expecto
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.References
open LangLSP.Server.DocumentSync

let makeReferenceParams uri line char includeDecl : ReferenceParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
        Context = { IncludeDeclaration = includeDecl }
    }
```

### 핵심 테스트 케이스들

```fsharp
[<Tests>]
let referencesTests =
    testSequenced <| testList "References" [

        testCase "let binding: finds all uses" <| fun _ ->
            clearAll()
            let uri = "file:///test-ref-let.fun"
            let text = "let x = 1 in x + x"
            handleDidOpen { TextDocument = { Uri = uri; LanguageId = "funlang"; Version = 1; Text = text } }

            // x 정의 위치 (col 4)에서 references 요청
            let result = handleReferences (makeReferenceParams uri 0 4 false) |> Async.RunSynchronously
            Expect.isSome result "Should find references"
            let locations = result.Value
            Expect.equal locations.Length 2 "Should find 2 references"

        testCase "includeDeclaration: includes definition" <| fun _ ->
            clearAll()
            let uri = "file:///test-ref-incldecl.fun"
            let text = "let x = 1 in x + x"
            handleDidOpen { TextDocument = { Uri = uri; LanguageId = "funlang"; Version = 1; Text = text } }

            // includeDeclaration = true
            let result = handleReferences (makeReferenceParams uri 0 4 true) |> Async.RunSynchronously
            Expect.isSome result "Should find references"
            let locations = result.Value
            Expect.equal locations.Length 3 "Should find 2 refs + 1 definition"

        testCase "shadowed variable: only finds inner scope uses" <| fun _ ->
            clearAll()
            let uri = "file:///test-ref-shadow.fun"
            let text = "let x = 1 in let x = 2 in x"
            handleDidOpen { TextDocument = { Uri = uri; LanguageId = "funlang"; Version = 1; Text = text } }

            // 외부 x (col 4)에서 references 요청
            let result = handleReferences (makeReferenceParams uri 0 4 false) |> Async.RunSynchronously
            Expect.isSome result "Should find references"
            let locations = result.Value
            Expect.equal locations.Length 0 "Outer x is shadowed, no uses"

        testCase "shadowed variable: inner definition has uses" <| fun _ ->
            clearAll()
            let uri = "file:///test-ref-shadow2.fun"
            let text = "let x = 1 in let x = 2 in x"
            handleDidOpen { TextDocument = { Uri = uri; LanguageId = "funlang"; Version = 1; Text = text } }

            // 내부 x (col 21)에서 references 요청
            let result = handleReferences (makeReferenceParams uri 0 21 false) |> Async.RunSynchronously
            Expect.isSome result "Should find references"
            let locations = result.Value
            Expect.equal locations.Length 1 "Inner x is used once"

        testCase "lambda parameter: finds all uses in body" <| fun _ ->
            clearAll()
            let uri = "file:///test-ref-lambda.fun"
            let text = "fun x -> x + x"
            handleDidOpen { TextDocument = { Uri = uri; LanguageId = "funlang"; Version = 1; Text = text } }

            // x 파라미터 (col 4)에서 references 요청
            let result = handleReferences (makeReferenceParams uri 0 4 false) |> Async.RunSynchronously
            Expect.isSome result "Should find references"
            let locations = result.Value
            Expect.equal locations.Length 2 "Parameter x used twice"

        testCase "let rec: finds recursive calls" <| fun _ ->
            clearAll()
            let uri = "file:///test-ref-letrec.fun"
            let text = "let rec f n = if n = 0 then 1 else n * f (n - 1) in f 5"
            handleDidOpen { TextDocument = { Uri = uri; LanguageId = "funlang"; Version = 1; Text = text } }

            // f 정의 (col 8)에서 references 요청
            let result = handleReferences (makeReferenceParams uri 0 8 false) |> Async.RunSynchronously
            Expect.isSome result "Should find references"
            let locations = result.Value
            Expect.equal locations.Length 2 "f used in recursive call + final use"

        testCase "unused variable: zero references" <| fun _ ->
            clearAll()
            let uri = "file:///test-ref-unused.fun"
            let text = "let x = 1 in 42"
            handleDidOpen { TextDocument = { Uri = uri; LanguageId = "funlang"; Version = 1; Text = text } }

            // x 정의에서 references 요청
            let result = handleReferences (makeReferenceParams uri 0 4 false) |> Async.RunSynchronously
            Expect.isSome result "Should return empty array"
            let locations = result.Value
            Expect.equal locations.Length 0 "No references to unused variable"
    ]
```

### 테스트 요점

1. **testSequenced**: 공유 DocumentSync 상태로 인한 간섭 방지
2. **clearAll()**: 각 테스트 전에 문서 저장소 초기화
3. **includeDeclaration 테스트**: true/false 동작 차이 검증
4. **섀도잉 테스트**: 외부/내부 정의 각각의 레퍼런스 정확도 확인
5. **미사용 변수 테스트**: 레퍼런스 0개 케이스 검증

### 테스트 실행

```bash
dotnet run --project src/LangLSP.Tests

# 출력:
# [References] let binding: finds all uses - Passed
# [References] includeDeclaration: includes definition - Passed
# [References] shadowed variable: only finds inner scope uses - Passed
# [References] shadowed variable: inner definition has uses - Passed
# [References] lambda parameter: finds all uses in body - Passed
# [References] let rec: finds recursive calls - Passed
# [References] unused variable: zero references - Passed
```

---

## Definition vs References 비교

### 기능 비교 표

| 측면 | Go to Definition | Find References |
|------|------------------|-----------------|
| **LSP 메서드** | `textDocument/definition` | `textDocument/references` |
| **반환 타입** | `Location` (단일) | `Location[]` (배열) |
| **의미** | "어디서 정의되었나?" | "어디서 사용되나?" |
| **개수** | 최대 1개 | 0개 이상 |
| **방향** | 사용처 → 정의 | 정의 → 사용처 |
| **트리거** | F12, Ctrl+클릭 | Shift+F12 |
| **includeDeclaration** | 없음 | `context.includeDeclaration` |

### 구현 복잡도 비교

```
┌─────────────────────────────────────────────┐
│ Go to Definition                            │
│ - 난이도: 중                                 │
│ - 핵심: collectDefinitions + 섀도잉 해결    │
│ - 반환: 단일 Location                        │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│ Find References                             │
│ - 난이도: 중상                               │
│ - 핵심: collectReferences + Definition 재사용│
│ - 반환: Location 배열                        │
│ - 추가 고려: includeDeclaration             │
└─────────────────────────────────────────────┘
```

### 코드 재사용

Find References는 Definition 모듈을 **필수적으로** 재사용합니다:

```fsharp
// References.fs에서 Definition 함수 사용
open LangLSP.Server.Definition

// 1. 각 Var 노드가 어느 정의를 참조하는지 확인
match findDefinitionForVar varName ast pos with
| Some foundDefSpan -> ...

// 2. 정의 위치 비교로 섀도잉 처리
foundDefSpan.StartLine = defSpan.StartLine &&
foundDefSpan.StartColumn = defSpan.StartColumn
```

**왜 재사용이 중요한가?**
- 섀도잉 로직 중복 방지
- 정의 검색 알고리즘 일관성 보장
- 유지보수 용이 (Definition 로직 변경 시 References도 자동 반영)

### 사용자 워크플로우

```
┌─────────────────────────────────────────────┐
│ 1. 변수 위에서 Ctrl+클릭 (Go to Definition) │
│    → 정의 위치로 이동                        │
└─────────────────────────────────────────────┘
           ↓
┌─────────────────────────────────────────────┐
│ 2. 정의 위치에서 Shift+F12 (Find References)│
│    → 모든 사용처 확인                        │
└─────────────────────────────────────────────┘
           ↓
┌─────────────────────────────────────────────┐
│ 3. 결과 목록에서 항목 클릭                   │
│    → 해당 사용처로 이동                      │
└─────────────────────────────────────────────┘
```

---

## 흔한 실수

### 1. 섀도잉 무시

**잘못된 구현:**
```fsharp
// 이름만 체크
let refs = collectReferences varName ast
return refs  // 섀도잉 고려 안 함!
```

**올바른 구현:**
```fsharp
// 정의를 먼저 찾고, 해당 정의를 참조하는 것만 필터링
let refSpans = collectReferencesForBinding varName defSpan ast
```

### 2. includeDeclaration 무시

**잘못된 구현:**
```fsharp
// 항상 정의 포함
let allSpans = defSpan :: refSpans
```

**올바른 구현:**
```fsharp
// context에 따라 조건부 포함
let allSpans =
    if p.Context.IncludeDeclaration then
        defSpan :: refSpans
    else
        refSpans
```

### 3. Var 노드만 처리하지 않음

**잘못된 구현:**
```fsharp
let rec traverse expr =
    match expr with
    | Number(_, span) ->
        refs.Add(span)  // 숫자도 레퍼런스로 수집?!
```

**올바른 구현:**
```fsharp
let rec traverse expr =
    match expr with
    | Var(name, span) when name = varName ->
        refs.Add(span)  // Var 노드만!
    | Number _ -> ()    // 리터럴은 레퍼런스 아님
```

### 4. 정의 위치를 Var로 착각

**문제 상황:**
```funlang
let x = 1 in x
    ^        ^
    정의     사용
```

`collectReferences`는 **사용처만** 찾아야 하므로:
- 정의 위치의 `x`는 `Let(name, ...)` 노드의 일부
- `Var(name, span)` 노드는 사용처에서만 등장

**검증:**
```fsharp
// collectReferences는 Var 노드만 수집
| Var(name, span) when name = varName ->
    refs.Add(span)

// Let 노드는 순회만 하고 수집하지 않음
| Let(name, value, body, _) ->
    traverse value   // value와 body만 순회
    traverse body
```

### 5. 재귀 함수의 파라미터 누락

**잘못된 구현:**
```fsharp
| LetRec(name, _, fnBody, inExpr, span) ->
    // name만 처리, param 무시
    if name = varName then refs.Add(span)
```

**올바른 구현:**
```fsharp
// let rec은 함수명과 파라미터 모두 확인 필요
// 하지만 collectReferences는 Var만 수집하므로
// fnBody와 inExpr 순회만 하면 자동으로 파라미터 사용처 수집됨
| LetRec(_, _, fnBody, inExpr, _) ->
    traverse fnBody
    traverse inExpr
```

---

## 최적화 고려사항

### 성능 분석

```fsharp
// collectReferencesForBinding의 복잡도
O(N * M)
  N = 전체 Var 노드 개수
  M = 각 Var마다 findDefinitionForVar 호출 (O(D), D = 정의 개수)
```

**대규모 파일에서의 문제:**
- 파일이 크면 Var 노드가 많음
- 각 Var마다 정의 검색 수행
- 섀도잉이 많으면 정의 비교 횟수 증가

### 최적화 전략 (미래 개선)

1. **정의 인덱스 캐싱**
   ```fsharp
   // 파일별 정의 맵 저장
   type DefinitionIndex = Map<string, Span list>
   let buildIndex ast : DefinitionIndex = ...
   ```

2. **스코프 트리 구축**
   ```fsharp
   // 각 정의의 유효 범위 사전 계산
   type ScopeTree = {
       Name: string
       Span: Span
       ValidRange: Range
       Children: ScopeTree list
   }
   ```

3. **증분 업데이트**
   ```fsharp
   // 문서 변경 시 영향받는 스코프만 재계산
   let updateReferences (edit: TextEdit) (oldIndex: DefinitionIndex) : DefinitionIndex
   ```

**현재 구현은 단순성 우선:**
- FunLang 파일은 일반적으로 작음 (< 1000 lines)
- 성능 문제 없음
- 코드 이해와 유지보수 용이

---

## 정리

Find References 구현의 핵심:

1. **레퍼런스 수집** - AST 순회로 모든 Var 노드 수집
   - `collectReferences`: 이름 일치만 체크 (기본)

2. **섀도잉 처리** - Definition 모듈 재사용
   - `collectReferencesForBinding`: 특정 정의의 레퍼런스만 필터링
   - 각 Var가 올바른 정의를 참조하는지 검증

3. **includeDeclaration** - 컨텍스트에 따라 정의 포함
   - true: 정의 + 사용처 모두
   - false: 사용처만

4. **Location[] 반환** - 배열로 여러 위치 전달
   - 각 위치는 URI + Range

5. **Definition과의 관계** - 역방향 검색
   - Definition: 사용처 → 정의
   - References: 정의 → 사용처들

---

## 다음 단계

Find References를 완성했습니다! 이제 사용자가 심볼의 영향 범위를 파악할 수 있습니다.

다음 Phase에서 구현할 기능들:
- **Rename**: Find References 활용하여 모든 사용처 일괄 변경
- **Code Actions**: 미사용 변수 제거 등 자동 수정 제안
- **Document Symbols**: 파일 내 모든 심볼 목록

---

## 참고 자료

- [LSP Specification - textDocument/references](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_references)
- [Find All References in VS Code](https://code.visualstudio.com/docs/editor/editingevolved#_reference-information)
- [Implementing Find References - LSP Guide](https://langserver.org/implementing-find-references/)
- [FunLang AST (Ast.fs)](https://github.com/kodu-ai/LangTutorial/blob/main/FunLang/Ast.fs)

---

**-> 다음: Rename 구현**
