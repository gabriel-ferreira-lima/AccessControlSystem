#!/usr/bin/env bash
# Decide o resultado do gate de CI: confere o resultado REAL de cada verificação e escreve a tabela do resumo
# (contracts/ci-gate.md). Roda no job final `ci-gate`, que nunca é pulado (`if: always()`).
#
# Entradas (variáveis de ambiente):
#   NEEDS_JSON      toJSON(needs): o resultado de cada job de que o gate depende
#   AREA_BACKEND    true|false   (saída do detect-areas.sh)
#   AREA_FRONTEND   true|false
#   AREA_DOCS_ONLY  true|false
# Saída: a tabela `Verificação | Estado | Motivo` e a linha "Resultado do gate: APROVADO|REPROVADO", na saída padrão e,
# se existir $GITHUB_STEP_SUMMARY, também nele. Sai 0 (aprovado) ou 1 (reprovado).
#
# As DUAS ARMADILHAS que este script existe para fechar:
#   1. check exigido que nunca roda: o próprio gate roda sempre; aqui só se avalia o que ele recebeu.
#   2. check "skipped" que o GitHub conta como verde: AQUI skipped só vale como DISPENSA quando a área comprovadamente
#      não mudou. Em verificação aplicável, só `success` passa; falha, cancelamento, tempo esgotado, skipped e a
#      AUSÊNCIA de resultado reprovam. Nunca se aprova por falta de resultado.
#
# Estados: aprovada, reprovada, dispensada (área sem mudança) e não ativado (o passo k6 curto, até haver cenários).
# Usa jq.

set -euo pipefail

K6_ROW="k6 curto (não ativado)"
K6_REASON="cenários de carga: spec 06"

failed=0
rows=()

add_row() {   # nome, estado, motivo
  rows+=("| $1 | $2 | $3 |")
  if [[ "$2" == "reprovada" ]]; then
    failed=1
  fi
}

# O jq do Windows (nativo) devolve CRLF; o tr garante que nenhum CR entre nas comparações.
jq_raw() {
  jq -r "$@" | tr -d '\r'
}

needs="${NEEDS_JSON-}"
area_backend="${AREA_BACKEND-}"
area_frontend="${AREA_FRONTEND-}"
area_docs="${AREA_DOCS_ONLY-}"

is_bool() {
  [[ "$1" == "true" || "$1" == "false" ]]
}

# Resultado de um job em needs: o texto de `.result`, ou "__ausente__" se a chave nem existe.
result_of() {
  printf '%s' "$needs" | jq_raw --arg key "$1" 'if has($key) then (.[$key].result // "__ausente__") else "__ausente__" end'
}

needs_ok=true
if ! printf '%s' "$needs" | jq -e 'type == "object"' > /dev/null 2>&1; then
  needs_ok=false
  add_row "needs" "reprovada" "NEEDS_JSON ausente ou inválido: não há resultado para avaliar"
fi

areas_ok=true
for pair in "AREA_BACKEND=$area_backend" "AREA_FRONTEND=$area_frontend" "AREA_DOCS_ONLY=$area_docs"; do
  if ! is_bool "${pair#*=}"; then
    areas_ok=false
    add_row "${pair%%=*}" "reprovada" "valor '${pair#*=}' inválido: era esperado true ou false"
  fi
done

if [[ "$areas_ok" == true && "$area_backend" == false && "$area_frontend" == false && "$area_docs" == false ]]; then
  # "Nenhuma área" só vale para "só documentos".
  areas_ok=false
  add_row "áreas" "reprovada" "estado incoerente: nenhuma área exigida e a mudança não é só de documentos"
fi

# Uma linha por verificação aplicável a um lado do gate (backend ou frontend).
add_area_row() {   # nome, área exigida (true|false)
  local name="$1" required="$2" result
  result="$(result_of "$name")"

  if [[ "$result" == "__ausente__" ]]; then
    add_row "$name" "reprovada" "resultado ausente em needs"
  elif [[ "$required" == true ]]; then
    if [[ "$result" == "success" ]]; then
      add_row "$name" "aprovada" ""
    else
      add_row "$name" "reprovada" "verificação aplicável terminou com '$result'"
    fi
  else
    if [[ "$result" == "skipped" ]]; then
      add_row "$name" "dispensada" "nenhum arquivo de $name mudou"
    else
      add_row "$name" "reprovada" "área sem mudança, mas o resultado foi '$result' (incoerente)"
    fi
  fi
}

if [[ "$needs_ok" == true ]]; then
  changes="$(result_of changes)"
  if [[ "$changes" == "success" ]]; then
    add_row "changes" "aprovada" ""
  elif [[ "$changes" == "__ausente__" ]]; then
    add_row "changes" "reprovada" "resultado ausente em needs"
  else
    add_row "changes" "reprovada" "a detecção das áreas terminou com '$changes'"
  fi

  if is_bool "$area_backend"; then
    add_area_row backend "$area_backend"
  else
    add_row "backend" "reprovada" "área desconhecida: não dá para saber se era exigida"
  fi

  if is_bool "$area_frontend"; then
    add_area_row frontend "$area_frontend"
  else
    add_row "frontend" "reprovada" "área desconhecida: não dá para saber se era exigida"
  fi

  k6="$(result_of k6-short)"
  if [[ "$k6" == "success" ]]; then
    add_row "$K6_ROW" "não ativado" "$K6_REASON"
  elif [[ "$k6" == "__ausente__" ]]; then
    add_row "$K6_ROW" "reprovada" "resultado ausente em needs"
  else
    add_row "$K6_ROW" "reprovada" "terminou com '$k6'"
  fi
fi

if [[ "$failed" -eq 0 ]]; then
  verdict="APROVADO"
else
  verdict="REPROVADO"
fi

report="$(
  printf '## Resultado do gate\n\n'
  printf '| Verificação | Estado | Motivo |\n'
  printf '|---|---|---|\n'
  printf '%s\n' "${rows[@]}"
  printf '\nResultado do gate: %s\n' "$verdict"
)"

printf '%s\n' "$report"

if [[ -n "${GITHUB_STEP_SUMMARY-}" ]]; then
  printf '%s\n' "$report" >> "$GITHUB_STEP_SUMMARY"
fi

exit "$failed"
