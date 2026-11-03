#!/usr/bin/env bash
# Aplica as proteções da branch principal: o ruleset `protect-main` e a varredura de segredos com bloqueio no envio.
# Quem roda é o DONO do repositório, com o gh autenticado, e NO WINDOWS NO GIT BASH (contracts/repository-protections.md).
#
# Uso:
#   apply-protections.sh              aplica: cria ou atualiza o ruleset, liga a varredura de segredos e confere o resultado
#   apply-protections.sh --dry-run    só LÊ o estado, imprime o corpo e o que faria; NUNCA escreve (nenhum POST, PUT, PATCH)
#   apply-protections.sh --disable    EMERGÊNCIA: ruleset com enforcement = disabled (a plataforma registra quem mudou e quando)
#   apply-protections.sh --enable     religa: enforcement = active
#
# Antes de enviar QUALQUER coisa, o script confere (e para sem escrever se falhar):
#   - a ORDEM DE ATIVAÇÃO: já existe um check run `ci-gate` na `main`. Exigir um check que ainda não existe travaria todos os
#     pull requests, inclusive o que o cria;
#   - o `integration_id`: o aplicativo que reportou esse check na `main` é o mesmo que o ruleset exige (main-ruleset.json,
#     hoje o GitHub Actions, 15368). Um valor errado travaria todos os pull requests;
#   - a leitura da varredura de segredos (o bloco `security_and_analysis`, que só o administrador enxerga).
# `--disable` e `--enable` só mudam o `enforcement` e NÃO exigem o gate (são para a emergência).
#
# É idempotente: se o ruleset `protect-main` já existe, atualiza o mesmo (PUT) em vez de criar um segundo.
# O gh é chamado por "$GH_BIN" (padrão: gh). SÓ OS TESTES trocam o GH_BIN por um gh falso; ninguém mais precisa dele.

set -euo pipefail

GH_BIN="${GH_BIN:-gh}"

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ruleset="$here/protections/main-ruleset.json"

# O gh troca {owner} e {repo} pelo repositório do diretório atual.
repo='repos/{owner}/{repo}'

usage() {
  printf 'Uso: apply-protections.sh [--dry-run | --disable | --enable]\n'
}

fail() {
  printf 'erro: %s\n' "$1" >&2
  exit 1
}

# O jq do Windows (nativo) devolve CRLF; o tr garante que nenhum CR entre nas comparações.
jq_raw() {
  jq -r "$@" | tr -d '\r'
}

# ---- Argumentos (antes de qualquer chamada) -------------------------------------------------------------------------

if [[ $# -gt 1 ]]; then
  usage >&2
  exit 2
fi

case "${1-}" in
  "") mode=apply ;;
  --dry-run) mode=dry-run ;;
  --disable) mode=disable ;;
  --enable) mode=enable ;;
  *)
    usage >&2
    exit 2
    ;;
esac

# ---- 1. Ferramentas -------------------------------------------------------------------------------------------------

if ! command -v jq > /dev/null 2>&1; then
  fail "jq não encontrado. No Windows rode este script no Git Bash (o bash do PowerShell é o atalho do WSL, que não enxerga o jq do Windows) e instale o jq (winget install jqlang.jq)."
fi

if ! command -v "$GH_BIN" > /dev/null 2>&1; then
  fail "gh não encontrado ('$GH_BIN'). No Windows rode este script no Git Bash (o bash do PowerShell é o atalho do WSL, que não enxerga o gh do Windows), instale o GitHub CLI e entre com 'gh auth login'."
fi

[[ -f "$ruleset" ]] || fail "não achei $ruleset (o corpo do ruleset)."

# O que o ruleset exige vem do main-ruleset.json (nunca duplicado aqui).
ruleset_name="$(jq_raw '.name' < "$ruleset")"
check_name="$(jq_raw '[.rules[] | select(.type == "required_status_checks") | .parameters.required_status_checks[0].context] | first // empty' < "$ruleset")"
expected_id="$(jq_raw '[.rules[] | select(.type == "required_status_checks") | .parameters.required_status_checks[0].integration_id] | first // empty' < "$ruleset")"

[[ -n "$ruleset_name" ]] || fail "main-ruleset.json não tem o nome do ruleset."
[[ -n "$check_name" ]] || fail "main-ruleset.json não define o check exigido (context)."
[[ "$expected_id" =~ ^[0-9]+$ ]] || fail "main-ruleset.json não define um integration_id numérico para o check exigido."

# ---- Chamadas ao gh -------------------------------------------------------------------------------------------------

gh_read() {   # endpoint -> JSON na saída padrão; falha com mensagem clara
  local out
  if ! out="$("$GH_BIN" api "$1" 2>&1)"; then
    fail "a leitura 'gh api $1' falhou: $out (o gh está autenticado? rode 'gh auth status'. Ler as proteções exige ser administrador do repositório)."
  fi
  printf '%s' "$out" | tr -d '\r'
}

gh_write() {   # método, endpoint, corpo (texto); falha com mensagem clara
  local out
  if ! out="$(printf '%s' "$3" | "$GH_BIN" api -X "$1" "$2" --input - 2>&1)"; then
    fail "a chamada 'gh api -X $1 $2' falhou: $out"
  fi
}

find_ruleset_id() {   # id do ruleset de mesmo nome, ou vazio
  local list
  list="$(gh_read "$repo/rulesets")"
  printf '%s' "$list" | jq_raw --arg name "$ruleset_name" '[.[] | select(.name == $name) | .id] | first // empty'
}

# ---- Emergência: só o enforcement -----------------------------------------------------------------------------------

if [[ "$mode" == disable || "$mode" == enable ]]; then
  id="$(find_ruleset_id)"
  [[ -n "$id" ]] || fail "o ruleset '$ruleset_name' não existe neste repositório: rode apply-protections.sh sem opções para criá-lo."

  if [[ "$mode" == disable ]]; then
    value=disabled
  else
    value=active
  fi

  # SÓ o enforcement vai no corpo: nada mais do ruleset muda.
  gh_write PUT "$repo/rulesets/$id" "{\"enforcement\":\"$value\"}"

  printf 'ruleset %s (id %s): enforcement = %s. A plataforma registra quem mudou e quando.\n' "$ruleset_name" "$id" "$value"
  if [[ "$value" == disabled ]]; then
    printf 'ATENÇÃO: com o ruleset desligado a main NÃO está protegida. Religue assim que a emergência passar:\n'
    printf '  bash scripts/github/apply-protections.sh --enable\n'
  fi
  exit 0
fi

# ---- 2. Ordem de ativação e integration_id (ANTES de escrever qualquer coisa) ---------------------------------------

runs="$(gh_read "$repo/commits/main/check-runs?check_name=$check_name")"

total="$(printf '%s' "$runs" | jq_raw '.total_count // 0')"
[[ "$total" =~ ^[0-9]+$ ]] || fail "resposta inesperada ao procurar o check '$check_name' na main (total_count inválido)."

if [[ "$total" -lt 1 ]]; then
  fail "ordem de ativação: ainda não existe nenhum check run '$check_name' na main. Exigir um check que não existe travaria todos os pull requests, inclusive o que o cria. Aceite primeiro o pull request que traz o gate e espere o '$check_name' rodar verde na main; depois rode este script."
fi

if ! printf '%s' "$runs" | jq -e --argjson id "$expected_id" 'any(.check_runs[]?; .app.id == $id)' > /dev/null 2>&1; then
  found="$(printf '%s' "$runs" | jq_raw '[.check_runs[]?.app.id // "?"] | unique | join(", ")')"
  fail "o check '$check_name' existe na main, mas NÃO vem do aplicativo de integration_id $expected_id que o ruleset exige (app.id encontrado: $found). Exigi-lo assim travaria todos os pull requests: confira o integration_id em $ruleset."
fi

# ---- 3 e 4. Estado atual: o ruleset e a varredura de segredos (só leituras até aqui) ---------------------------------

ruleset_id="$(find_ruleset_id)"

scanning_enabled() {   # JSON do repositório -> sucesso se as duas proteções estão `enabled`
  printf '%s' "$1" | jq -e '.security_and_analysis.secret_scanning.status == "enabled" and .security_and_analysis.secret_scanning_push_protection.status == "enabled"' > /dev/null 2>&1
}

repo_json="$(gh_read "$repo")"

if ! printf '%s' "$repo_json" | jq -e '.security_and_analysis | type == "object"' > /dev/null 2>&1; then
  fail "não consegui ler o bloco security_and_analysis do repositório: só o administrador o enxerga. Rode com uma conta de administrador (gh auth status) para ler e ligar a varredura de segredos."
fi

secret_manual_step() {
  printf 'Ligue a varredura À MÃO: no GitHub, Settings > Advanced Security: ligue "Secret scanning" e "Push protection". Depois rode este script de novo (ele é idempotente) e o check-protections-manual.sh.\n' >&2
}

# ---- --dry-run: mostra, não escreve ---------------------------------------------------------------------------------

if [[ "$mode" == dry-run ]]; then
  printf '[dry-run] NADA será escrito: só leituras foram feitas.\n'
  printf '[dry-run] ordem de ativação ok: o check %s existe na main, vindo do aplicativo %s.\n' "$check_name" "$expected_id"
  printf '[dry-run] corpo do ruleset (%s):\n' "$ruleset"
  cat "$ruleset"
  printf '\n'
  if [[ -n "$ruleset_id" ]]; then
    printf '[dry-run] atualizaria o ruleset existente (id %s): PUT %s/rulesets/%s\n' "$ruleset_id" "$repo" "$ruleset_id"
  else
    printf '[dry-run] criaria o ruleset: POST %s/rulesets\n' "$repo"
  fi
  if scanning_enabled "$repo_json"; then
    printf '[dry-run] varredura de segredos e proteção de envio já estão ligadas: nenhum PATCH.\n'
  else
    printf '[dry-run] ligaria a varredura de segredos e a proteção de envio: PATCH %s\n' "$repo"
  fi
  printf '[dry-run] depois rodaria a conferência automática (check-rules.sh) e imprimiria os passos da conferência à mão.\n'
  exit 0
fi

# ---- Escrita: o ruleset ---------------------------------------------------------------------------------------------

if [[ -n "$ruleset_id" ]]; then
  gh_write PUT "$repo/rulesets/$ruleset_id" "$(cat "$ruleset")"
  printf 'ruleset %s atualizado (id %s).\n' "$ruleset_name" "$ruleset_id"
else
  gh_write POST "$repo/rulesets" "$(cat "$ruleset")"
  printf 'ruleset %s criado.\n' "$ruleset_name"
fi

# ---- Escrita: a varredura de segredos (só se estiver desligada) -----------------------------------------------------

if scanning_enabled "$repo_json"; then
  printf 'varredura de segredos e proteção de envio já estavam ligadas: nada a fazer.\n'
else
  patch='{"security_and_analysis":{"secret_scanning":{"status":"enabled"},"secret_scanning_push_protection":{"status":"enabled"}}}'

  if ! patch_out="$(printf '%s' "$patch" | "$GH_BIN" api -X PATCH "$repo" --input - 2>&1)"; then
    printf 'erro: o GitHub recusou ligar a varredura de segredos pela API: %s\n' "$patch_out" >&2
    secret_manual_step
    exit 1
  fi

  # Relê para CONFIRMAR: nunca se finge sucesso.
  repo_json="$(gh_read "$repo")"
  if ! scanning_enabled "$repo_json"; then
    printf 'erro: o PATCH foi aceito, mas a releitura mostra a varredura de segredos ainda desligada.\n' >&2
    secret_manual_step
    exit 1
  fi

  printf 'varredura de segredos e proteção de envio ligadas e confirmadas.\n'
fi

# ---- 5. Conferência automática do que acabou de ser aplicado --------------------------------------------------------

rules="$(gh_read "$repo/rules/branches/main?per_page=100")"

printf '\nConferência automática (check-rules.sh):\n'
if ! printf '%s' "$rules" | bash "$here/check-rules.sh"; then
  fail "a conferência automática reprovou: o ruleset foi enviado, mas a leitura das regras ativas não mostra tudo (veja as linhas 'falta:' acima). Rode este script de novo; se persistir, confira o ruleset na interface."
fi

printf '\nFalta a conferência À MÃO, para o que o endpoint de regras não mostra (quem pode furar as regras e a varredura de segredos):\n'
printf '  bash scripts/github/check-protections-manual.sh\n'
