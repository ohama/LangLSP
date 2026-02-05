// =============================================
// All Features Integration Test
// =============================================
// 모든 LSP 기능을 하나의 파일에서 종합 테스트하세요.
//
// 기능 체크리스트:
//   [ ] 구문 강조 - 키워드, 문자열, 주석, 연산자에 색상 적용
//   [ ] 호버 - 변수 위에 마우스 → 타입 정보
//   [ ] 자동 완성 - Ctrl+Space → 변수/키워드 제안
//   [ ] 정의로 이동 - F12 → 정의 위치
//   [ ] 참조 찾기 - Shift+F12 → 모든 사용 위치
//   [ ] 이름 변경 - F2 → 일괄 변경
//   [ ] 코드 액션 - 사용하지 않는 변수 경고
//   [ ] 진단 - 타입 오류 실시간 표시
//   [ ] 주석 토글 - Ctrl+/ 과 Ctrl+Shift+A
//   [ ] 자동 닫기 - (, [, ", (* 자동 닫기
//   [ ] 스니펫 - let, match, if, fun 등

(* FunLang 함수형 프로그래밍 예제

   이 파일은 실제 알고리즘을 구현하면서
   모든 LSP 기능을 테스트할 수 있도록 구성되었습니다.
*)

// --- 유틸리티 함수 ---
let rec map f = fun xs ->
    match xs with
    | [] -> []
    | h :: t -> (f h) :: (map f t)
in

let rec filter pred = fun xs ->
    match xs with
    | [] -> []
    | h :: t ->
        if pred h
        then h :: (filter pred t)
        else filter pred t
in

let rec fold f = fun acc -> fun xs ->
    match xs with
    | [] -> acc
    | h :: t -> fold f (f acc h) t
in

let rec append xs = fun ys ->
    match xs with
    | [] -> ys
    | h :: t -> h :: (append t ys)
in

// --- 정렬 알고리즘 ---
let rec quicksort xs =
    match xs with
    | [] -> []
    | pivot :: rest ->
        let less = filter (fun x -> x < pivot) rest in
        let greater = filter (fun x -> x >= pivot) rest in
        append (quicksort less) (pivot :: quicksort greater)
in

// --- 수학 함수 ---
let rec factorial n =
    if n <= 1
    then 1
    else n * factorial (n - 1)
in

let rec fibonacci n =
    if n <= 1
    then n
    else fibonacci (n - 1) + fibonacci (n - 2)
in

// --- 리스트 연산 ---
let sum = fun xs -> fold (fun acc -> fun x -> acc + x) 0 xs in
let length = fun xs -> fold (fun acc -> fun _ -> acc + 1) 0 xs in
let isEven = fun n -> n / 2 * 2 = n in

// --- 결과 계산 ---
let nums = [5, 3, 8, 1, 9, 2, 7, 4, 6] in
let sorted = quicksort nums in
let evens = filter isEven sorted in
let total = sum sorted in
let count = length sorted in
let squares = map (fun x -> x * x) evens in

let fact10 = factorial 10 in
let fib15 = fibonacci 15 in

// --- 최종 결과 ---
(sorted, evens, total, count, squares, fact10, fib15)
