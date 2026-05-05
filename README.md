## 1) 개요

이 플러그인은 트랙에 `Master Router Phonemizer` 하나만 설정한 뒤,  
노트 가사 앞의 라우팅 태그로 하위 포네마이저를 분기합니다.

- 기본 라우팅 문법: `:포네마이저태그:가사`
- 포네마이저태그 기준: `[Phonemizer("이름", "태그", ...)]`의 **두 번째 인자(태그)**
- 기본 동작: 언어 경계에서는 연결(VC/연속음)을 끊는 `Hard Boundary`

---

## 2) 설치

1. OpenUtau 종료
2. 아래 파일을 `\OpenUtau\Plugins`에 복사
   - `MasterRouter.dll`
   - `master-router.config.json` (선택이지만 권장)
3. OpenUtau 실행
4. 트랙 포네마이저에서 `Master Router Phonemizer` 선택

주의:
- DLL 교체 중 `파일이 다른 프로세스에서 사용 중` 오류가 나면 OpenUtau가 실행 중인 상태입니다.

---

## 3) 라우팅 문법

### 기본

- `:포네마이저 이름:가사`

예시:
- `:JA VCV & CVVC:あ`
- `:EN VCCV:ah`
- `:KO CVC:가`

### alias 사용

`master-router.config.json`의 `aliases`로 짧은 태그를 매핑할 수 있습니다.

예시:
- `:ja:あ` -> `JA VCV & CVVC`
- `:en:hello` -> `EN VCCV`

실 사용례: 
<img width="1017" height="842" alt="스크린샷 2026-04-05 113849" src="https://github.com/user-attachments/assets/db3f0a72-a60b-4e57-b162-2603a8b7040d" />

<img width="1296" height="785" alt="스크린샷 2026-04-05 113909" src="https://github.com/user-attachments/assets/a55bd11c-ecc5-4336-b2ee-7e6335611821" />




---

## 4) 경계 처리 (Hard Boundary + 브리지)

기본 규칙:
- 현재 노트와 이웃 노트의 하위 포네마이저가 다르면 경계로 판단
- 경계에서는 `prev/next` 연결 정보를 끊어 VC/연결음을 강제로 차단

### 브리지 문법: `>`

경계 노트 끝에 `>`를 붙이면 다음 노트 방향 연결 힌트를 넣을 수 있습니다.

#### 4-1) 수동 힌트

- 형식: `가사>힌트`
- 예시: `:KO CVVC:각>k`
- 동작: `k`를 다음 발음 시작 힌트로 사용

#### 4-2) 자동 힌트

- 형식: `가사>`
- 예시: `:KO CVVC:각>`
- 동작 순서:
1. 다음 노트 `phoneticHint` 첫 토큰 사용
2. 없으면 다음 노트 가사를 언어별 테이블로 onset 추정 (현재 JA/EN/KO)
3. 추정 실패 시 브리지 미적용(기본 Hard Boundary 유지)

---

## 5) Fallback 규칙

라우팅 태그가 잘못되었거나 미등록일 때:

1. 가사 첫 유효 문자가 히라가나/가타카나면 `jaFallback`
2. 그 외는 `primary`

---

## 6) 설정 파일 (`master-router.config.json`)

예시:

```json
{
  "primary": "DEFAULT",
  "jaFallback": "JA VCV & CVVC",
  "quickTag": "JA VCV & CVVC",
  "aliases": {
    "ja": "JA VCV & CVVC",
    "en": "EN ARPA+",
    "ko": "KO CVVC",
    "default": "DEFAULT"
  }
}
```

필드 설명:
- `primary`: 기본 포네마이저 태그
- `jaFallback`: 일본어 문자 시작 시 폴백 태그
- `quickTag`: 선택 노트 태그 일괄 부여 플러그인 기본값
- `aliases`: 사용자 약어 -> 실제 태그 매핑

---

## 7) 지원 범위 / 비지원 범위

### 권장(지원 대상)
- 같은 singer 형식 내부 라우팅
  - 예: UTAU 계열끼리, DiffSinger 계열끼리
- 내장 포네마이저 외에, 사용자가 추가한 커스텀 포네마이저도 지원

### 비권장(지원 예정 없음)
- 서로 다른 singer 형식 혼합 라우팅
  - 예: UTAU 포네마이저와 DiffSinger 포네마이저를 같은 트랙에서 혼합

---

## 8) 문제 해결

### 포네마이저가 목록에 안 보일 때

1. DLL 위치 확인: `\OpenUtau\Plugins\MasterRouter.dll`
2. OpenUtau 재시작
3. `More... -> General` 그룹에서 확인
4. 로그 확인: `\OpenUtau\Logs\logYYYYMMDD.txt`

### DLL 복사가 안 될 때

- OpenUtau 완전 종료 후 다시 복사
- 백그라운드에 OpenUtau 프로세스가 남아있으면 종료 필요

### 안내&주의사항
- 이 프로그램은 AI를 이용하여, 100% 바이브코딩으로 작성되었고 인간은 검수만 완료했습니다.
- 음소 간 다른 포네마이저를 쓸 때 의도대로 연결되지 않는 버그가 있을 수 있습니다.
- 이 프로그램을 사용하며 발생하는 손해 등에 대해서는 개발자는 어느 것도 책임지지 않습니다.
