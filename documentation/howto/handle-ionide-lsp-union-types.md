---
created: 2026-02-05
description: Ionide.LanguageServerProtocol의 U2/U3 union 타입 래핑 패턴
---

# Ionide LSP Union 타입 (U2/U3) 사용법

Ionide.LanguageServerProtocol 0.7.0은 LSP 스펙의 `X | Y` 타입을 F# discriminated union `U2<X,Y>`, `U3<X,Y,Z>`로 표현한다.

## The Insight

LSP 스펙에서 많은 필드가 여러 타입 중 하나를 가질 수 있다. 예: `Hover.contents`는 `MarkupContent | MarkedString | MarkedString[]` 중 하나. Ionide는 이걸 `U2<A,B>`, `U3<A,B,C>` 제네릭 union으로 표현하고, 각 케이스를 `U2.C1`, `U2.C2`, `U3.C1`, `U3.C2`, `U3.C3`으로 구분한다.

문제는 **어떤 필드가 어떤 U 타입을 쓰는지, C1/C2가 어떤 타입에 대응하는지** 문서화가 되어 있지 않다는 것이다. LSP 스펙의 union 순서와 Ionide의 C1/C2 순서가 대응하지만, 이를 확인하려면 Ionide 소스 코드를 읽어야 한다.

## Why This Matters

- `U2.C1`과 `U2.C2`를 바꿔 쓰면 **컴파일은 되지만** 런타임에 잘못된 JSON이 생성된다
- 타입 에러 없이 LSP 응답이 깨져서 원인을 찾기 어렵다
- Ionide 버전 업데이트 시 union 순서가 바뀔 수 있다

## Recognition Pattern

- Ionide.LanguageServerProtocol으로 LSP 서버를 구현할 때
- LSP 응답이 "뭔가 이상하게" 직렬화될 때
- `ServerCapabilities` 필드를 설정할 때
- Hover, Definition, Rename 등의 응답을 구성할 때

## The Approach

### Step 1: LSP 스펙에서 union 순서 확인

LSP 스펙의 타입 정의에서 `|` 순서가 C1, C2, C3 순서다.

```
// LSP Spec: Hover.contents
MarkupContent | MarkedString | MarkedString[]
    C1              C2             C3
```

### Step 2: 주요 U2/U3 매핑 레퍼런스

**ServerCapabilities (U2):**

| 필드 | U2.C1 | U2.C2 |
|------|-------|-------|
| `TextDocumentSyncOptions` | `TextDocumentSyncOptions` | `TextDocumentSyncKind` |
| `HoverProvider` | `bool` | `HoverOptions` |
| `DefinitionProvider` | `bool` | `DefinitionOptions` |
| `CompletionProvider` | (직접 사용) | — |
| `ReferencesProvider` | `bool` | `ReferenceOptions` |
| `RenameProvider` | `bool` | `RenameOptions` |
| `CodeActionProvider` | `bool` | `CodeActionOptions` |

일반 패턴: `C1 = bool` (단순 활성화), `C2 = Options` (상세 설정).

```fsharp
// 단순 활성화
HoverProvider = Some (U2.C1 true)
DefinitionProvider = Some (U2.C1 true)

// 옵션 지정
RenameProvider = Some (U2.C2 { PrepareProvider = Some true })
CodeActionProvider = Some (U2.C2 { CodeActionKinds = Some [| "quickfix" |] })
```

**Hover 응답 (U3):**

```fsharp
// Hover.contents: MarkupContent | MarkedString | MarkedString[]
//                     C1              C2             C3
Contents = U3.C1 {
    Kind = MarkupKind.Markdown
    Value = "```fsharp\nint\n```"
}
```

**Definition 응답 (U2):**

```fsharp
// Definition: Location | Location[]
//                C1          C2
return Some (U2.C1 location)  // 단일 위치
```

**PrepareRename 응답 (U3):**

```fsharp
// PrepareRenameResult: Range | { Range, Placeholder } | { DefaultBehavior }
//                        C1           C2                      C3
Some (U3.C2 { Range = range; Placeholder = name })
```

**TextDocumentSync 변경 (U2):**

```fsharp
// TextDocumentContentChangeEvent: Incremental | Full
//                                     C1          C2
| U2.C1 incrementalChange -> // Range + Text
| U2.C2 fullChange ->        // Text only
```

**Diagnostic.Code (U2):**

```fsharp
// Code: int | string
//        C1     C2
Code = Some (U2.C2 "unused-variable")  // 문자열 코드
```

**CodeAction.Edit 응답 (U2):**

```fsharp
// 배열 안에서 각 항목을 U2로 래핑
result |> Option.map (Array.map U2.C2)
```

### Step 3: 확인 방법

확실하지 않을 때 Ionide 소스에서 타입 정의를 확인한다:

```bash
# Ionide 패키지 소스 위치 (NuGet 캐시)
find ~/.nuget/packages/ionide.languageserverprotocol -name "Types.fs" | head -1

# 특정 타입 검색
grep -A 5 "HoverProvider" ~/.nuget/packages/ionide.languageserverprotocol/0.7.0/lib/netstandard2.0/*.fs
```

또는 IDE의 "Go to Definition"으로 `U2<_, _>` 타입의 제네릭 파라미터를 확인한다.

## Example

```fsharp
// ❌ BAD: C1/C2 혼동 — 컴파일 되지만 잘못된 JSON 생성
Contents = U3.C2 {  // C2는 MarkedString, C1이 MarkupContent!
    Kind = MarkupKind.Markdown
    Value = "hover info"
}

// ✅ GOOD: LSP 스펙 순서 확인 후 올바른 케이스 사용
Contents = U3.C1 {  // C1 = MarkupContent (첫 번째 union 멤버)
    Kind = MarkupKind.Markdown
    Value = "hover info"
}
```

```fsharp
// ❌ BAD: bool로 세부 옵션 설정 시도
RenameProvider = Some (U2.C1 true)  // PrepareProvider 설정 불가

// ✅ GOOD: U2.C2로 옵션 객체 사용
RenameProvider = Some (U2.C2 {
    PrepareProvider = Some true
    WorkDoneProgress = None
})
```

## 체크리스트

- [ ] U2/U3 사용 시 LSP 스펙의 union 순서를 확인했는가?
- [ ] C1이 첫 번째 타입, C2가 두 번째 타입에 대응하는가?
- [ ] ServerCapabilities에서 `C1 = bool`, `C2 = Options` 패턴을 따르는가?
- [ ] 새 LSP 기능 추가 시 Ionide 소스의 타입 정의를 확인했는가?

## 관련 문서

- [Ionide.LanguageServerProtocol GitHub](https://github.com/ionide/LanguageServerProtocol) - 소스 코드
- [LSP Specification 3.17](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/) - 타입 정의 참조
