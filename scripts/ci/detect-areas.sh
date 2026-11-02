#!/usr/bin/env bash
# Classifica os arquivos alterados em áreas e diz quais verificações a mudança exige (data-model.md, seção 1).
#
# Entrada (stdin): um caminho por linha, o que o `git diff --name-only` imprime. O CR do fim de linha e as linhas em
# branco são ignorados.
# Saída (stdout), três linhas prontas para o $GITHUB_OUTPUT:
#   backend=true|false     a verificação do backend é exigida
#   frontend=true|false    a verificação do frontend é exigida
#   docs_only=true|false   TODOS os arquivos são documentos (e há pelo menos um)
#
# Cada arquivo cai em UMA classe, nesta ordem de precedência:
#   1. documento: specs/**, .specify/**, .claude/** e qualquer *.md (inclusive em src/Backend e src/Frontend)
#   2. backend:   src/Backend/**
#   3. frontend:  src/Frontend/**
#   4. fora das áreas: todo o resto (.github/**, docker/**, docker-compose.yml, scripts/**, .gitignore...). É FECHADO POR
#      PADRÃO: exige backend E frontend. Só se dispensa o que comprovadamente não mudou.
# Lista vazia (por exemplo, `before` zerado num push): tudo aplicável, nunca "nenhuma". Sem jq.

set -euo pipefail

backend=false
frontend=false
total=0
documents=0

while IFS= read -r file || [[ -n "$file" ]]; do
  file="${file%$'\r'}"

  # Linha em branco (ou só espaços) não é arquivo.
  if [[ -z "${file//[[:space:]]/}" ]]; then
    continue
  fi

  total=$((total + 1))

  case "$file" in
    specs/* | .specify/* | .claude/* | *.md)
      documents=$((documents + 1))
      ;;
    src/Backend/*)
      backend=true
      ;;
    src/Frontend/*)
      frontend=true
      ;;
    *)
      backend=true
      frontend=true
      ;;
  esac
done

if [[ "$total" -eq 0 ]]; then
  # Sem arquivos não dá para dizer que nada mudou: exige tudo.
  backend=true
  frontend=true
  docs_only=false
elif [[ "$documents" -eq "$total" ]]; then
  docs_only=true
else
  docs_only=false
fi

printf 'backend=%s\n' "$backend"
printf 'frontend=%s\n' "$frontend"
printf 'docs_only=%s\n' "$docs_only"
