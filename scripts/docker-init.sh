#!/usr/bin/env bash
set -euo pipefail

# Limita a senha antes da substituição de variável pelo sqlcmd.
if [[ ! "${APP_DB_PASSWORD:-}" =~ ^[a-zA-Z0-9_!]{16,128}$ ]]; then
  echo "APP_DB_PASSWORD deve conter 16 a 128 caracteres: letras, números, _ e !." >&2
  exit 1
fi

/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -C -b -i /scripts/docker-init.sql
