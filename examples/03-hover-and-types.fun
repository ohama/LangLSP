// =============================================
// Hover & Type Information Test
// =============================================
// 각 변수/함수 위에 마우스를 올려 타입 정보를 확인하세요.
//
// 확인 항목:
//   1. 정수 변수: int 타입 표시
//   2. 불리언 변수: bool 타입 표시
//   3. 문자열 변수: string 타입 표시
//   4. 함수: 시그니처 표시 (int -> int -> int 등)
//   5. 키워드(let, if 등): 한국어 설명 표시

// --- 기본 타입 ---
let count = 42 in              // hover → int
let name = "FunLang" in        // hover → string
let active = true in           // hover → bool

// --- 함수 타입 ---
let add = fun x -> fun y -> x + y in       // hover → int -> int -> int
let double = fun n -> n * 2 in             // hover → int -> int
let isPositive = fun n -> n > 0 in         // hover → int -> bool
let negate = fun b -> if b then false else true in  // hover → bool -> bool

// --- 고차 함수 ---
let apply = fun f -> fun x -> f x in       // hover → ('a -> 'b) -> 'a -> 'b
let twice = fun f -> fun x -> f (f x) in   // hover → (int -> int) -> int -> int

// --- 재귀 함수 ---
let rec sum xs =
    match xs with
    | [] -> 0
    | h :: t -> h + sum t
in                                          // hover on sum → list -> int

let rec map f = fun xs ->
    match xs with
    | [] -> []
    | h :: t -> (f h) :: (map f t)
in                                          // hover on map → ('a -> 'b) -> list -> list

// --- 키워드 hover ---
// let 위에 hover → "값을 이름에 바인딩합니다"
// if 위에 hover → "조건부 표현식"
// match 위에 hover → "패턴 매칭 표현식"
// fun 위에 hover → "익명 함수(람다)"

// --- 결과 ---
let nums = [1, 2, 3, 4, 5] in
let total = sum nums in
let doubled = map double nums in
(total, doubled, apply double 21, twice double 3)
