#!/usr/bin/env bash
# Verificação REAL dos cabeçalhos de segurança do console (FR-013): constrói a imagem do console como ela é entregue,
# sobe-a ao lado de um upstream simulado (alias "backend"; o nginx não sobe se o host do proxy_pass não resolver) e
# confere, por requisição de verdade, página, arquivo estático, 404 e resposta encaminhada, sem e com
# "X-Forwarded-Proto: https". Contrato: specs/20261102-125831-security-perimeter/contracts/security-headers.md
#
# Precisa de: docker, curl, openssl e bash (Git Bash no Windows serve). Roda do CI (frontend-ci.yml) e localmente:
#   cd src/Frontend && bash scripts/verify-security-headers.sh
set -euo pipefail

# Git Bash no Windows converteria argumentos como "127.0.0.1::80" em caminhos.
export MSYS_NO_PATHCONV=1
export MSYS2_ARG_CONV_EXCL="*"

cd "$(dirname "${BASH_SOURCE[0]}")/.."   # src/Frontend

SUFFIX="$$-$(date +%s)"
NETWORK="acs-headers-net-${SUFFIX}"
APP_IMAGE="acs-headers-app:${SUFFIX}"
STUB_IMAGE="acs-headers-stub:${SUFFIX}"
APP="acs-headers-app-${SUFFIX}"
STUB="acs-headers-stub-${SUFFIX}"

# Limpeza em QUALQUER saída (sucesso, divergência ou erro): contêineres, rede e imagens do teste.
cleanup() {
  docker rm -f "${APP}" "${STUB}" >/dev/null 2>&1 || true
  docker network rm "${NETWORK}" >/dev/null 2>&1 || true
  docker rmi -f "${APP_IMAGE}" "${STUB_IMAGE}" >/dev/null 2>&1 || true
}
trap cleanup EXIT

fail() {
  echo "FALHA: $*" >&2
  exit 1
}

ok() {
  echo "OK    $*"
}

echo "== Construindo as imagens (console e upstream simulado)"
docker build -q -t "${APP_IMAGE}" . >/dev/null
docker build -q -t "${STUB_IMAGE}" -f scripts/headers-check/stub.Dockerfile scripts/headers-check >/dev/null

echo "== Subindo o upstream simulado e o console na mesma rede"
docker network create "${NETWORK}" >/dev/null
docker run -d --name "${STUB}" --network "${NETWORK}" --network-alias backend "${STUB_IMAGE}" >/dev/null
docker run -d --name "${APP}" --network "${NETWORK}" -p 127.0.0.1::80 "${APP_IMAGE}" >/dev/null

PORT="$(docker port "${APP}" 80/tcp | head -n 1 | sed 's/.*://' | tr -d '\r')"
[ -n "${PORT}" ] || fail "não foi possível descobrir a porta publicada do console"
BASE="http://127.0.0.1:${PORT}"

for _ in $(seq 1 60); do
  if curl -s -o /dev/null --max-time 2 "${BASE}/"; then break; fi
  sleep 0.5
done
curl -s -o /dev/null --max-time 5 "${BASE}/" || fail "o console não respondeu em ${BASE} (logs: docker logs ${APP})"

# Cabeçalhos da resposta (sem CR). Argumentos extras vão para o curl.
headers_of() {
  curl -sS -D - -o /dev/null --max-time 10 "$@" | tr -d '\r'
}

# Quantas vezes o cabeçalho aparece (sem diferenciar maiúsculas).
count_of() {
  local headers="$1" name="$2"
  printf '%s\n' "${headers}" | grep -ci "^${name}:" || true
}

value_of() {
  local headers="$1" name="$2"
  printf '%s\n' "${headers}" | grep -i "^${name}:" | head -n 1 | sed -E "s/^[^:]+:[[:space:]]*//"
}

# Hash sha256 (base64) do texto do único <script> escrito na página servida, com CRLF normalizado para LF (o navegador
# calcula o hash da CSP assim). Em pipe, para o shell não comer espaços/linhas do fim do texto.
served_script_hash() {
  curl -sS --max-time 10 "${BASE}/" | tr -d '\r' \
    | awk 'BEGIN { RS = "</script>" } { i = index($0, "<script>"); if (i > 0) { printf "%s", substr($0, i + 8); exit } }' \
    | openssl dgst -sha256 -binary | openssl base64 -A
}

SCRIPT_HASH="$(served_script_hash)"
[ -n "${SCRIPT_HASH}" ] || fail "não achei o <script> escrito na página servida"

# check_case <rótulo> <caminho> <status esperado> <com https? (sim|nao)>
check_case() {
  local label="$1" path="$2" expected_status="$3" https="$4"
  local -a extra=()
  [ "${https}" = "sim" ] && extra=(-H "X-Forwarded-Proto: https")

  local headers
  headers="$(headers_of "${extra[@]+"${extra[@]}"}" "${BASE}${path}")"

  local status
  status="$(printf '%s\n' "${headers}" | head -n 1 | awk '{print $2}')"
  [ "${status}" = "${expected_status}" ] || fail "${label}: status ${status}, esperado ${expected_status}"

  for name in Content-Security-Policy X-Frame-Options X-Content-Type-Options Referrer-Policy Permissions-Policy; do
    local n
    n="$(count_of "${headers}" "${name}")"
    [ "${n}" = "1" ] || fail "${label}: o cabeçalho ${name} apareceu ${n} vez(es), esperado 1"
  done

  [ "$(value_of "${headers}" X-Frame-Options)" = "DENY" ] || fail "${label}: X-Frame-Options diferente de DENY"
  [ "$(value_of "${headers}" X-Content-Type-Options)" = "nosniff" ] || fail "${label}: X-Content-Type-Options diferente de nosniff"
  [ "$(value_of "${headers}" Referrer-Policy)" = "no-referrer" ] || fail "${label}: Referrer-Policy diferente de no-referrer"

  local csp
  csp="$(value_of "${headers}" Content-Security-Policy)"
  case "${csp}" in *"frame-ancestors 'none'"*) ;; *) fail "${label}: CSP sem frame-ancestors 'none'" ;; esac
  case "${csp}" in *"unsafe-inline"*) fail "${label}: a CSP contém unsafe-inline" ;; esac
  case "${csp}" in *"'sha256-${SCRIPT_HASH}'"*) ;; *) fail "${label}: a CSP não libera o hash 'sha256-${SCRIPT_HASH}' do script servido" ;; esac

  local hsts_count
  hsts_count="$(count_of "${headers}" Strict-Transport-Security)"
  if [ "${https}" = "sim" ]; then
    [ "${hsts_count}" = "1" ] || fail "${label}: com X-Forwarded-Proto https o HSTS apareceu ${hsts_count} vez(es), esperado 1"
    local max_age
    max_age="$(value_of "${headers}" Strict-Transport-Security | sed -E 's/.*max-age=([0-9]+).*/\1/')"
    [ "${max_age}" -ge 31536000 ] 2>/dev/null || fail "${label}: HSTS com max-age ${max_age}, esperado >= 31536000"
    value_of "${headers}" Strict-Transport-Security | grep -qiE 'includeSubDomains|preload' \
      && fail "${label}: o HSTS não deve ter includeSubDomains nem preload"
  else
    [ "${hsts_count}" = "0" ] || fail "${label}: sem X-Forwarded-Proto https o HSTS apareceu ${hsts_count} vez(es), esperado 0"
  fi

  ok "${label} (HTTP ${status}, https=${https})"
}

echo "== Conferindo os cabeçalhos"
ASSET="$(curl -sS --max-time 10 "${BASE}/" | grep -oE '/assets/[^"]+\.js' | head -n 1)"
[ -n "${ASSET}" ] || fail "não achei o arquivo .js do build no index.html servido"

for https in nao sim; do
  check_case "página inicial ${BASE}/" "/" 200 "${https}"
  check_case "arquivo estático ${ASSET}" "${ASSET}" 200 "${https}"
  check_case "erro 404 do nginx /assets/nao-existe.js" "/assets/nao-existe.js" 404 "${https}"
  check_case "resposta encaminhada /v1/ping (upstream simulado)" "/v1/ping" 200 "${https}"
done

echo "== Todos os cabeçalhos de segurança conferem (hash do script servido: sha256-${SCRIPT_HASH})"
