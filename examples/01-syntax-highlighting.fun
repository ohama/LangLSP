// =============================================
// Syntax Highlighting Test
// =============================================
// 이 파일을 VS Code에서 열어 구문 강조를 확인하세요.
//
// 확인 항목:
//   - 키워드: let, in, if, then, else, match, with, fun, rec
//   - 타입: int, bool, string, list
//   - 상수: 숫자(42), 불리언(true, false)
//   - 문자열: "hello"
//   - 연산자: +, -, *, /, =, <>, <, >, <=, >=, &&, ||, ->, ::
//   - 주석: // 한 줄 주석, (* 블록 주석 *)

// --- 키워드 (keyword.control) ---
let x = 1 in
let y = 2 in
let maxXY = if x > y then x else y in

// --- 재귀 함수 (rec 키워드) ---
let rec factorial n =
    if n <= 1
    then 1
    else n * factorial (n - 1)
in

// --- 람다 (fun 키워드) ---
let add = fun a -> fun b -> a + b in
let double = fun n -> n * 2 in

// --- 패턴 매칭 (match, with, 파이프 |) ---
let rec length xs =
    match xs with
    | [] -> 0
    | _ :: t -> 1 + length t
in

// --- 타입 키워드 (support.type) ---
// int, bool, string, list 타입은 별도 색상으로 표시

// --- 불리언 상수 (constant.language) ---
let isTrue = true in
let isFalse = false in

// --- 숫자 상수 (constant.numeric) ---
let answer = 42 in
let zero = 0 in

// --- 문자열 (string.quoted) ---
let greeting = "hello world" in
let escaped = "tab\there\nnewline" in

// --- 다중 문자 연산자 (올바른 토큰화 확인) ---
let arrow = fun x -> x in          // -> 한 토큰
let cons = 1 :: 2 :: [] in         // :: 한 토큰
let neq = 1 <> 2 in                // <> 한 토큰
let leq = 1 <= 2 in                // <= 한 토큰
let geq = 2 >= 1 in                // >= 한 토큰
let logicAnd = true && false in    // && 한 토큰
let logicOr = true || false in     // || 한 토큰

// --- 와일드카드 ---
let _ = 0 in

// --- 타입 변수 (미래 확장용) ---
// 'a, 'b 같은 타입 변수

// --- 최종 결과 ---
(maxXY, factorial 5, add 1 2, double 3, length [1, 2, 3],
 isTrue, isFalse, answer, zero, greeting, escaped,
 arrow 1, cons, neq, leq, geq, logicAnd, logicOr)
