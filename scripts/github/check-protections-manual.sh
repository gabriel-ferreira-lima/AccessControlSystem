#!/usr/bin/env bash
# Conferência À MÃO das proteções da `main`: o que o endpoint de regras (usado pela conferência automática) NÃO mostra e que
# exige ser administrador do repositório (contracts/repository-protections.md). O dono roda, com o gh autenticado, e no
# Windows NO GIT BASH.
#
# Confere e imprime `OK:` ou `FALHA:` para cada item:
#   1. bypass_actors do ruleset `protect-main` está VAZIO (ninguém fura as regras, nem admin)
#   2. current_user_can_bypass = never (nem quem está rodando consegue furar)
#   3. enforcement = active (o ruleset está valendo)
#   4. secret_scanning.status = enabled (varredura de segredos ligada)
#   5. secret_scanning_push_protection.status = enabled (bloqueio no envio ligado)
# Sai 0 se TUDO estiver certo, 1 se algum item falhar. Não conseguir ler um item (falta de permissão de administrador, ruleset
# inexistente) é FALHA: nunca se aprova sem ter visto. SÓ LÊ: nenhuma chamada de escrita.
#
# O gh é chamado por "$GH_BIN" (padrão: gh). SÓ OS TESTES trocam o GH_BIN por um gh falso.

set -euo pipefail

GH_BIN="${GH_BIN:-gh}"

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ruleset="$here/protections/main-ruleset.json"

# O gh troca {owner} e {repo} pelo repositório do diretório atual.
repo='repos/{owner}/{repo}'

fail() {
  printf 'erro: %s\n' "$1" >&2
  exit 1
}

# O jq do Windows (nativo) devolve CRLF; o tr garante que nenhum CR entre nas comparações.
jq_raw() {
  jq -r "$@" | tr -d '\r'
}

if ! command -v jq > /dev/null 2>&1; then
  fail "jq não encontrado. No Windows rode este script no Git Bash (o bash do PowerShell é o atalho do WSL, que não enxerga o jq do Windows) e instale o jq (winget install jqlang.jq)."
fi

if ! command -v "$GH_BIN" > /dev/null 2>&1; then
  fail "gh não encontrado ('$GH_BIN'). No Windows rode este script no Git Bash (o bash do PowerShell é o atalho do WSL, que não enxerga o gh do Windows), instale o GitHub CLI e entre com 'gh auth login'."
fi

[[ -f "$ruleset" ]] || fail "não achei $ruleset (o corpo do ruleset)."
ruleset_name="$(jq_raw '.name' < "$ruleset")"

gh_read() {   # endpoint -> JSON na saída padrão; falha com mensagem clara
  local out
  if ! out="$("$GH_BIN" api "$1" 2>&1)"; then
    fail "a leitura 'gh api $1' falhou: $out (o gh está autenticado? rode 'gh auth status'. Esta conferência exige ser administrador do repositório)."
  fi
  printf '%s' "$out" | tr -d '\r'
}

failed=0

ok() {
  printf 'OK: %s\n' "$1"
}

bad() {
  printf 'FALHA: %s\n' "$1"
  failed=1
}

# ---- O ruleset: bypass, quem está rodando e enforcement ------------------------------------------------------------

list="$(gh_read "$repo/rulesets")"
id="$(printf '%s' "$list" | jq_raw --arg name "$ruleset_name" '[.[] | select(.name == $name) | .id] | first // empty')"

if [[ -z "$id" ]]; then
  bad "o ruleset '$ruleset_name' não existe: as regras da main não estão aplicadas (rode scripts/github/apply-protections.sh)."
else
  detail="$(gh_read "$repo/rulesets/$id")"

  # 1. bypass_actors vazio. Quem não é administrador nem vê o campo: não conseguir ler é FALHA.
  if ! printf '%s' "$detail" | jq -e '.bypass_actors | type == "array"' > /dev/null 2>&1; then
    bad "bypass_actors: não consegui ler (é preciso ser administrador do repositório)."
  elif [[ "$(printf '%s' "$detail" | jq_raw '.bypass_actors | length')" == "0" ]]; then
    ok "bypass_actors vazio: ninguém fura as regras da main."
  else
    bad "bypass_actors NÃO está vazio: $(printf '%s' "$detail" | jq_raw '.bypass_actors | length') ator(es) podem furar as regras."
  fi

  # 2. nem quem está rodando consegue furar
  can_bypass="$(printf '%s' "$detail" | jq_raw '.current_user_can_bypass // "ausente"')"
  if [[ "$can_bypass" == "never" ]]; then
    ok "current_user_can_bypass = never: nem a conta que está rodando fura as regras."
  else
    bad "current_user_can_bypass = $can_bypass (esperado: never)."
  fi

  # 3. o ruleset está valendo
  enforcement="$(printf '%s' "$detail" | jq_raw '.enforcement // "ausente"')"
  if [[ "$enforcement" == "active" ]]; then
    ok "enforcement = active: o ruleset está valendo."
  else
    bad "enforcement = $enforcement (esperado: active)."
  fi
fi

# ---- A varredura de segredos ---------------------------------------------------------------------------------------

repo_json="$(gh_read "$repo")"

if ! printf '%s' "$repo_json" | jq -e '.security_and_analysis | type == "object"' > /dev/null 2>&1; then
  bad "secret_scanning e secret_scanning_push_protection: o bloco security_and_analysis não veio na resposta (só o administrador do repositório o enxerga)."
else
  scanning="$(printf '%s' "$repo_json" | jq_raw '.security_and_analysis.secret_scanning.status // "ausente"')"
  if [[ "$scanning" == "enabled" ]]; then
    ok "secret_scanning.status = enabled: a varredura de segredos está ligada."
  else
    bad "secret_scanning.status = $scanning (esperado: enabled)."
  fi

  push="$(printf '%s' "$repo_json" | jq_raw '.security_and_analysis.secret_scanning_push_protection.status // "ausente"')"
  if [[ "$push" == "enabled" ]]; then
    ok "secret_scanning_push_protection.status = enabled: o envio com segredo é barrado."
  else
    bad "secret_scanning_push_protection.status = $push (esperado: enabled)."
  fi
fi

exit "$failed"
