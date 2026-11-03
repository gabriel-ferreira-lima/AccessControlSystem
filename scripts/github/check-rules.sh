#!/usr/bin/env bash
# Conferência AUTOMÁTICA das proteções da `main` (contracts/repository-protections.md).
#
# Entrada (stdin): a resposta de `GET /repos/{dono}/{repo}/rules/branches/main`, uma lista JSON de regras ativas.
# O nome do check exigido e o `integration_id` esperado vêm de protections/main-ruleset.json (ao lado deste script):
# a fonte única, nunca duplicados aqui.
#
# Confere, e imprime uma linha `falta: ...` para cada ausência:
#   - regra pull_request
#   - regra required_status_checks com um item de `context` E `integration_id` iguais aos esperados (um status externo
#     com o mesmo nome, de outro aplicativo, NÃO vale)
#   - regra non_fast_forward (sem envio forçado)
#   - regra deletion (sem exclusão da branch)
# Sai 0 (tudo no lugar) ou 1. Não conseguir ler as regras (resposta de erro, 403 por permissão ou limite, entrada vazia ou
# que não é uma lista) REPROVA: nunca se aprova por falta de informação.
#
# Este script SÓ LÊ a entrada: não chama a rede nem tem caminho anônimo. Quem o alimenta é sempre autenticado (o workflow
# passa o token da execução; à mão, o gh do dono). Usa jq.

set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ruleset="$here/protections/main-ruleset.json"

# O jq do Windows (nativo) devolve CRLF; o tr garante que nenhum CR entre nas comparações.
jq_raw() {
  jq -r "$@" | tr -d '\r'
}

fail() {
  printf 'erro: %s\n' "$1"
  exit 1
}

[[ -f "$ruleset" ]] || fail "não achei $ruleset (a fonte do nome do check e do integration_id)."

# O que esperar vem do main-ruleset.json (lido pela entrada padrão para o caminho valer igual no Git Bash e no Linux).
expected_name="$(jq_raw '[.rules[] | select(.type == "required_status_checks") | .parameters.required_status_checks[0].context] | first // empty' < "$ruleset")"
expected_id="$(jq_raw '[.rules[] | select(.type == "required_status_checks") | .parameters.required_status_checks[0].integration_id] | first // empty' < "$ruleset")"

[[ -n "$expected_name" ]] || fail "main-ruleset.json não define o check exigido (context)."
[[ "$expected_id" =~ ^[0-9]+$ ]] || fail "main-ruleset.json não define um integration_id numérico para o check exigido."

input="$(cat)"

# A resposta precisa ser uma LISTA de regras. Erro da API, {} e entrada vazia reprovam.
if ! printf '%s' "$input" | jq -e 'type == "array"' > /dev/null 2>&1; then
  fail "a resposta não é uma lista JSON de regras (resposta de erro, sem permissão, limite de chamadas ou entrada vazia): não foi possível conferir."
fi

missing=0

has_rule() {   # tipo
  printf '%s' "$input" | jq -e --arg type "$1" 'any(.[]; type == "object" and .type == $type)' > /dev/null 2>&1
}

for type in pull_request non_fast_forward deletion; do
  if has_rule "$type"; then
    printf 'ok: %s\n' "$type"
  else
    printf 'falta: %s\n' "$type"
    missing=$((missing + 1))
  fi
done

# O item do check: nome E origem. Comparação estrita (integration_id é número; "15368" em texto não vale).
if printf '%s' "$input" | jq -e --arg name "$expected_name" --argjson id "$expected_id" '
      any(.[];
          type == "object"
          and .type == "required_status_checks"
          and ((.parameters.required_status_checks // []) | any(.[]; type == "object" and .context == $name and .integration_id == $id)))
    ' > /dev/null 2>&1; then
  printf 'ok: required_status_checks (%s, integration_id %s)\n' "$expected_name" "$expected_id"
else
  printf 'falta: required_status_checks (check %s com integration_id %s)\n' "$expected_name" "$expected_id"
  missing=$((missing + 1))
fi

if [[ "$missing" -gt 0 ]]; then
  exit 1
fi
