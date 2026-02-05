// =============================================
// Completion (Auto-Complete) Test
// =============================================
// Ctrl+Space 를 눌러 자동 완성을 테스트하세요.
//
// 확인 항목:
//   1. 키워드 자동 완성: let, if, match, fun 등
//   2. 스코프 내 변수/함수 자동 완성
//   3. 타입 정보와 함께 표시

// --- 스코프에 변수 추가 ---
let alpha = 1 in
let beta = 2 in
let gamma = 3 in

let add = fun x -> fun y -> x + y in
let multiply = fun x -> fun y -> x * y in

let rec factorial n =
    if n <= 1
    then 1
    else n * factorial (n - 1)
in

// --- 여기서 Ctrl+Space 테스트 ---
// 아래 줄 끝에 커서를 놓고 Ctrl+Space:
//   - alpha, beta, gamma 가 보여야 함
//   - add, multiply, factorial 이 보여야 함
//   - let, if, match, fun 키워드도 보여야 함

// 'a' 타이핑 후 Ctrl+Space → alpha, add 제안
// 'f' 타이핑 후 Ctrl+Space → factorial, fun 제안
// 'm' 타이핑 후 Ctrl+Space → multiply, match 제안

// --- 중첩 스코프 ---
let outer = 100 in
let result =
    let inner = 200 in
    // 여기서 Ctrl+Space → inner, outer 모두 보여야 함
    inner + outer
in

result + factorial 5
