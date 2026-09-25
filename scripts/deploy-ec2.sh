#!/usr/bin/env bash
set -euo pipefail
image=${1:?Informe a imagem ECR por digest}
region=${2:?Informe a região AWS}
[[ "$image" =~ ^[0-9]{12}\.dkr\.ecr\.[a-z0-9-]+\.amazonaws\.com/gh-rotas-api@sha256:[a-f0-9]{64}$ ]] || exit 2
[[ "$region" =~ ^[a-z]{2}-[a-z]+-[0-9]+$ ]] || exit 2
cd /home/ubuntu/gh-rotas-api
test -f .env && test -f compose.yaml
exec 9>/var/lock/gh-rotas-api-deploy.lock
flock -n 9 || { echo 'Outro deploy está em execução.'; exit 1; }

# Mantém o nome do projeto, a rede e os volumes existentes.
base=(docker compose -p gh-rotas-api -f compose.yaml)
container=$("${base[@]}" ps -q api)
test -n "$container" || { echo 'API existente não encontrada. Confira o projeto Compose.'; exit 1; }
previous=$(docker inspect --format '{{.Image}}' "$container")
sql=$("${base[@]}" ps -q sqlserver)
test -n "$sql" && test "$(docker inspect --format '{{.State.Health.Status}}' "$sql")" = healthy

work=$(mktemp -d)
trap 'rm -rf -- "$work"' EXIT
export DOCKER_CONFIG="$work/docker"
mkdir -m 700 "$DOCKER_CONFIG"
registry=${image%%/*}
aws ecr get-login-password --region "$region" | docker login --username AWS --password-stdin "$registry"
docker pull "$image"
printf 'services:\n  api:\n    image: "%s"\n' "$image" > "$work/image.yaml"
compose=("${base[@]}" -f "$work/image.yaml")
"${compose[@]}" config --quiet

# Homologação atual: Development disponibiliza o Swagger.
check_api() {
    local id port
    id=$("${base[@]}" ps -q api)
    test -n "$id" || return 1
    port=$(docker inspect --format '{{(index (index .NetworkSettings.Ports "8080/tcp") 0).HostPort}}' "$id")
    for _ in {1..30}; do
        if curl --silent --fail --max-time 5 "http://127.0.0.1:$port/swagger/v1/swagger.json" -o /dev/null; then
            return 0
        fi
        sleep 2
    done
    return 1
}

if "${compose[@]}" up -d --no-deps --no-build --pull never api && check_api; then
    install -m 600 "$work/image.yaml" compose.deploy.yaml
    echo "Deploy concluído: $image"
else
    echo 'Nova API não respondeu. Restaurando imagem anterior.'
    printf 'services:\n  api:\n    image: "%s"\n' "$previous" > "$work/image.yaml"
    "${compose[@]}" up -d --no-deps --no-build --pull never api
    check_api || { echo 'Rollback não respondeu; intervenção necessária.'; exit 1; }
    echo 'Imagem anterior restaurada. Deploy marcado como falha.'
    exit 1
fi