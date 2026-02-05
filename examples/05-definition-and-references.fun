// =============================================
// Go to Definition & Find References Test
// =============================================
// F12 (정의로 이동)과 Shift+F12 (참조 찾기)를 테스트하세요.
//
// 확인 항목:
//   1. F12: 변수 사용 위치에서 정의 위치로 이동
//   2. Shift+F12: 변수의 모든 사용 위치 표시
//   3. 섀도잉된 변수의 올바른 해석

// --- 기본 정의/참조 ---
let base = 10 in               // ← base 정의
let height = 5 in              // ← height 정의
let area = base * height in    // base, height 에서 F12 → 위로 이동
                                // base 에서 Shift+F12 → 2곳 표시

// --- 함수 정의/참조 ---
let rec fibonacci n =          // ← fibonacci 정의
    if n <= 1
    then n
    else fibonacci (n - 1) + fibonacci (n - 2)
in                              // fibonacci 에서 Shift+F12 → 3곳 (재귀 호출 포함)

let fib10 = fibonacci 10 in   // fibonacci 에서 F12 → 위의 정의로 이동
let fib20 = fibonacci 20 in   // fibonacci 에서 F12 → 같은 정의로 이동

// --- 변수 섀도잉 ---
let x = 100 in                // ← 외부 x 정의
let first = x + 1 in          // 이 x 는 외부 x 참조 (F12 → 100)
let result =
    let x = 200 in            // ← 내부 x 정의 (섀도잉)
    let second = x + 2 in     // 이 x 는 내부 x 참조 (F12 → 200)
    second
in
let third = x + 3 in          // 이 x 는 다시 외부 x 참조 (F12 → 100)

// --- 람다 파라미터 ---
let transform = fun value ->   // ← value 정의
    let doubled = value * 2 in // value 에서 F12 → 위의 fun value
    let squared = value * value in
    doubled + squared
in

(area, fib10, first, result, third, transform 5)
