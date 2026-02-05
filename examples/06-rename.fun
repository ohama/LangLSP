// =============================================
// Rename Symbol Test
// =============================================
// F2 를 눌러 심볼 이름 변경을 테스트하세요.
//
// 확인 항목:
//   1. F2: 변수 이름 일괄 변경
//   2. 변경 전 미리보기 표시
//   3. 정의 + 모든 사용 위치가 동시에 변경
//   4. 섀도잉된 변수는 독립적으로 변경

// --- 변수 이름 변경 ---
// 아래 'count' 위에서 F2 → 'total' 로 변경 시도
let count = 0 in
let step1 = count + 1 in
let step2 = count + 2 in
let step3 = count + 3 in
// 'count'의 정의와 3개 사용 위치 모두 변경되어야 함

// --- 함수 이름 변경 ---
// 아래 'square' 위에서 F2 → 'squared' 로 변경 시도
let square = fun n -> n * n in
let a = square 3 in
let b = square 4 in
let c = square 5 in
// 'square'의 정의와 3개 호출 위치 모두 변경되어야 함

// --- 재귀 함수 이름 변경 ---
// 'sumTo' 변경 시 재귀 호출도 함께 변경되어야 함
let rec sumTo n =
    if n <= 0
    then 0
    else n + sumTo (n - 1)
in

// --- 섀도잉 독립 변경 ---
let val = 10 in
let outer = val + 1 in
let result =
    let val = 20 in         // 이 val 을 F2 로 변경하면
    val + 1                 // 이것만 변경되고 외부 val 은 유지
in

(step1, step2, step3, a, b, c, sumTo 10, outer, result)
