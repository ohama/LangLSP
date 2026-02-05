---
created: 2026-02-05
description: TextMate grammar에서 패턴 순서, 중첩 주석, 키워드 경계 처리
---

# TextMate Grammar 패턴 순서와 중첩 처리

TextMate grammar은 regex를 순서대로 적용한다. 이 순서와 자기참조 패턴을 잘못 쓰면 토큰이 깨진다.

## The Insight

TextMate grammar은 "첫 번째로 매칭되는 패턴이 이긴다." regex 엔진이 패턴 목록을 위에서 아래로 시도하고, 처음 매칭되면 그 토큰으로 확정한다. 이 특성 때문에:

1. **연산자 순서**: 멀티 캐릭터 연산자(`->`, `<=`, `<>`)가 단일 캐릭터 연산자(`-`, `<`, `>`) 보다 먼저 와야 한다.
2. **키워드 경계**: `\b` 없이 `let`을 매칭하면 `letter`의 `let`도 매칭된다.
3. **중첩 구조**: 블록 주석 안에 블록 주석이 들어가려면 패턴이 자기 자신을 참조해야 한다.

## Why This Matters

**연산자 순서를 무시하면:** `->` 가 `-`와 `>`로 쪼개져서 화살표 연산자가 뺄셈+부등호로 표시된다. `<=`가 `<`와 `=`으로 분리된다.

**`\b` 없이 키워드를 매칭하면:** `letter`, `infinity`, `match_result` 같은 변수명 안의 키워드 부분이 하이라이팅된다.

**중첩 주석을 처리 안 하면:** `(* outer (* inner *) still comment *)` 에서 첫 번째 `*)`에서 주석이 끝나고, 나머지 `still comment *)`가 코드로 표시된다.

## Recognition Pattern

- 새 언어용 TextMate grammar을 작성할 때
- "한 글자 연산자"와 "여러 글자 연산자"가 접두사를 공유할 때 (`-` vs `->`)
- ML 계열, Haskell, Rust 등 중첩 블록 주석을 지원하는 언어

## The Approach

### Step 1: 연산자를 길이 내림차순으로 정렬

패턴 배열에서 멀티 캐릭터 연산자를 먼저 배치한다.

```json
{
  "operators": {
    "patterns": [
      { "name": "keyword.operator.arrow.funlang", "match": "->" },
      { "name": "keyword.operator.cons.funlang", "match": "::" },
      { "name": "keyword.operator.comparison.funlang", "match": "<>|<=|>=|<|>" },
      { "name": "keyword.operator.logical.funlang", "match": "&&|\\|\\|" },
      { "name": "keyword.operator.arithmetic.funlang", "match": "[+\\-*/]" },
      { "name": "keyword.operator.assignment.funlang", "match": "=" },
      { "name": "keyword.operator.pipe.funlang", "match": "\\|" }
    ]
  }
}
```

핵심: `->` 가 `[+\-*/]` 보다 위에 있다. 아래에 있으면 `-`가 먼저 매칭된다.

비교 연산자도 마찬가지: `<>|<=|>=` 가 `<|>` 보다 앞에 온다. regex OR `|` 에서도 왼쪽이 우선이다.

### Step 2: 키워드에 `\b` 워드 바운더리 사용

```json
{
  "keywords": {
    "name": "keyword.control.funlang",
    "match": "\\b(if|then|else|match|with|let|in|fun|rec)\\b"
  }
}
```

`\b`가 단어 경계에서만 매칭을 보장한다. 없으면:
- `letter` → `let` + `ter` (let이 키워드로 매칭)
- `infinity` → `in` + `finity` (in이 키워드로 매칭)

### Step 3: 중첩 블록 주석에 자기참조 패턴 사용

블록 주석 패턴 안에서 자기 자신을 include 한다:

```json
{
  "comments": {
    "patterns": [
      { "name": "comment.line.double-slash.funlang", "match": "//.*$" },
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
  }
}
```

동작 원리:
1. 외부 `(* ... *)` 가 `comment.block`으로 매칭
2. 내부에서 다시 `(*`를 만나면 `block-comment-nested`가 새 레벨 시작
3. `block-comment-nested`가 자기 자신을 include하므로 무한 중첩 가능
4. 각 레벨의 `*)`가 해당 레벨의 `(*`와 쌍을 이룸

## Example

```
// ❌ BAD: 단일 문자가 먼저
"patterns": [
  { "match": "[+\\-*/]" },     // `-` 매칭
  { "match": "->" }            // 도달 불가! `-`가 먼저 먹음
]

// ✅ GOOD: 멀티 캐릭터 먼저
"patterns": [
  { "match": "->" },           // `->` 먼저 시도
  { "match": "[+\\-*/]" }      // `-` 단독일 때만 매칭
]
```

```
// ❌ BAD: 단순 begin/end (중첩 불가)
{
  "begin": "\\(\\*",
  "end": "\\*\\)"
  // inner `(* ... *)` 에서 첫 *)로 전체 종료
}

// ✅ GOOD: 자기참조 (중첩 가능)
{
  "begin": "\\(\\*",
  "end": "\\*\\)",
  "patterns": [
    { "include": "#block-comment-nested" }  // 재귀!
  ]
}
```

## 체크리스트

- [ ] 멀티 캐릭터 연산자가 단일 캐릭터보다 위에 있는가?
- [ ] regex OR(`|`) 안에서도 긴 패턴이 왼쪽인가?
- [ ] 모든 키워드 패턴에 `\b` 워드 바운더리가 있는가?
- [ ] 중첩 구조(주석, 문자열 보간)에 자기참조 패턴을 사용하는가?
- [ ] VS Code "Developer: Inspect Editor Tokens and Scopes"로 토큰 확인했는가?

## 관련 문서

- [TextMate Grammar Language](https://macromates.com/manual/en/language_grammars) - 공식 문법 참조
- [VS Code Syntax Highlighting Guide](https://code.visualstudio.com/api/language-extensions/syntax-highlight-guide) - VS Code 문서
