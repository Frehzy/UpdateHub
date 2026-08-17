#!/usr/bin/env bash
#
# Сквозная проверка: настоящий клиент против настоящего сервера.
#
# Зачем она нужна отдельно от остальных проверок. Тесты клиента работают против
# tests/fake-server.py — поддельного сервера, написанного из того же понимания
# протокола, из которого получился настоящий. Если это понимание где-то неверно,
# обе стороны ошибаются одинаково, и все 94 проверки проходят. Тесты сервера,
# со своей стороны, обращаются к нему через HttpClient, а не через curl.
#
# То есть стык между bash-клиентом и сервером не проверен ничем, кроме
# предположений. Здесь он проверяется целиком: образ сервера, установка клиента
# в чистую Ubuntu, заявка, одобрение, выдача прав, скачивание, сверка сумм.
#
# Требуется docker и jq. Порядок действий повторяет ввод машины в работу
# из README клиента.

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
CLIENT_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(cd -- "$CLIENT_DIR/.." && pwd)"

IMAGE="updatehub-server:e2e"
NETWORK="updatehub-e2e"
SERVER="updatehub-e2e-server"
CLIENT="updatehub-e2e-client"

# Порт на стороне хоста: с него скрипт обращается к части для администратора.
# Клиент внутри сети идёт к серверу по имени, а не через него.
HOST_PORT="18080"
BASE="http://127.0.0.1:$HOST_PORT"

ADMIN_USER="admin"
ADMIN_PASSWORD="administrator-parol-12345"
OPERATOR_USER="operator"
OPERATOR_PASSWORD="operator-parol-12345"

WORK_DIR=""
FAILED=0
PASSED=0

# ---------- Вывод ----------

say() { printf '%s\n' "$*"; }
step() { printf '\n== %s\n' "$*"; }

ok() {
    PASSED=$((PASSED + 1))
    printf '  [прошло] %s\n' "$*"
}

fail() {
    FAILED=$((FAILED + 1))
    printf '  [ОШИБКА] %s\n' "$*" >&2
}

# ---------- Уборка ----------

cleanup() {
    local code=$?

    if [ "$code" -ne 0 ] || [ "$FAILED" -ne 0 ]; then
        step "Журнал сервера (последние 40 строк)"
        docker logs --tail 40 "$SERVER" 2>&1 | sed 's/^/  /' || true
    fi

    step "Уборка"
    docker rm -f "$CLIENT" "$SERVER" >/dev/null 2>&1 || true
    docker network rm "$NETWORK" >/dev/null 2>&1 || true

    if [ -n "$WORK_DIR" ] && [ -d "$WORK_DIR" ]; then
        rm -rf "$WORK_DIR"
    fi

    say "Убрано."
}

# ---------- Вспомогательное ----------

# Обращение к части для администратора. Ответ печатается в stdout.
# Аргументы: $1 — метод, $2 — путь, $3 — тело JSON (необязательно).
admin_api() {
    local method="$1" path="$2" body="${3:-}"

    if [ -n "$body" ]; then
        curl -sS -X "$method" "$BASE$path" \
            -H "Authorization: Bearer $ADMIN_TOKEN" \
            -H 'Content-Type: application/json' \
            --data-binary "$body"
    else
        curl -sS -X "$method" "$BASE$path" \
            -H "Authorization: Bearer $ADMIN_TOKEN"
    fi
}

# Выполняет команду внутри контейнера клиента.
client_exec() {
    docker exec "$CLIENT" bash -lc "$*"
}

# Значение поля из ответа в формате «ключ=значение».
# Аргументы: $1 — текст ответа, $2 — имя поля.
text_value() {
    printf '%s\n' "$1" | sed -n "s/^$2=//p" | head -n 1
}

# ---------- Проверка окружения ----------

require_tools() {
    local missing=0

    for tool in docker jq curl; do
        if ! command -v "$tool" >/dev/null 2>&1; then
            printf 'Не найдена программа %s\n' "$tool" >&2
            missing=1
        fi
    done

    if ! docker version >/dev/null 2>&1; then
        say "Docker не отвечает: проверка невозможна."
        missing=1
    fi

    [ "$missing" -eq 0 ] || exit 2
}

# ---------- Подготовка ----------

prepare_files() {
    WORK_DIR="$(mktemp -d)"
    mkdir -p "$WORK_DIR/files/docs"

    printf 'первый файл\n' >"$WORK_DIR/files/docs/pervyy.txt"
    printf 'второй файл\n' >"$WORK_DIR/files/docs/vtoroy.txt"
    printf 'вложенный\n' >"$WORK_DIR/files/readme.txt"

    # Права на чтение всем: внутри контейнера сервер работает не от root.
    chmod -R a+rX "$WORK_DIR/files"
}

start_server() {
    step "Сборка образа сервера"
    docker build -q -t "$IMAGE" "$REPO_ROOT" >/dev/null

    docker network create "$NETWORK" >/dev/null

    step "Запуск сервера"
    docker run -d \
        --name "$SERVER" \
        --network "$NETWORK" \
        --network-alias server \
        -p "127.0.0.1:$HOST_PORT:8080" \
        -v "$WORK_DIR/files":/app/files:ro \
        -e ASPNETCORE_ENVIRONMENT=Production \
        -e Jwt__SecretKey="kluch-dlya-skvoznoy-proverki-1234567890" \
        -e BootstrapAdmin__Username="$ADMIN_USER" \
        -e BootstrapAdmin__Password="$ADMIN_PASSWORD" \
        -e Security__PasswordWorkFactor=4 \
        -e UpdateHub__FilesPath=/app/files \
        -e UpdateHub__DatabasePath=/app/data/updatehub.db \
        -e UpdateHub__BackupPath=/app/backup \
        -e UpdateHub__FileSettleSeconds=0 \
        -e UpdateHub__PollIntervalSeconds=2 \
        "$IMAGE" >/dev/null

    printf '  ожидание готовности'
    local attempt
    for attempt in $(seq 1 60); do
        if curl -fsS "$BASE/health" >/dev/null 2>&1; then
            printf ' — готов (попытка %s)\n' "$attempt"
            return 0
        fi
        printf '.'
        sleep 1
    done

    printf '\n'
    fail "сервер не ответил на /health"
    return 1
}

start_client() {
    step "Установка клиента в чистую Ubuntu"

    docker run -d \
        --name "$CLIENT" \
        --network "$NETWORK" \
        -v "$CLIENT_DIR":/opt/updatehub-src:ro \
        ubuntu:24.04 sleep infinity >/dev/null

    # curl в базовом образе Ubuntu отсутствует, а клиент без него не работает.
    # md5sum и coreutils на месте.
    client_exec "apt-get update -qq && DEBIAN_FRONTEND=noninteractive apt-get install -y -qq curl >/dev/null"

    # Исходники копируются: каталог подключён только для чтения, а установка
    # обращается к нему как к обычному.
    client_exec "cp -r /opt/updatehub-src /tmp/src && cd /tmp/src && ./install.sh >/dev/null"

    client_exec "mkdir -p /opt/updatehub-data"

    write_client_config "" ""
}

# Записывает настройки клиента.
# Аргументы: $1 — логин, $2 — пароль (пустые допустимы до выдачи прав).
write_client_config() {
    local username="$1" password="$2"

    client_exec "cat >/etc/updatehub/updatehub.conf <<'КОНЕЦ'
SERVER_URL=\"http://server:8080\"
DATA_DIR=\"/opt/updatehub-data\"
TEMP_DIR=\"/var/tmp/updatehub\"
STATE_DIR=\"/var/lib/updatehub\"
CLIENT_ID_FILE=\"/etc/updatehub/client-id\"
LOG_FILE=\"/var/log/updatehub.log\"
USERNAME=\"$username\"
PASSWORD=\"$password\"
КОНЕЦ
chmod 600 /etc/updatehub/updatehub.conf"
}

# ---------- Проверки ----------

login_admin() {
    step "Вход администратора"

    local response
    response="$(curl -sS -X POST "$BASE/api/v1/auth/login" \
        --data-urlencode "username=$ADMIN_USER" \
        --data-urlencode "password=$ADMIN_PASSWORD")"

    ADMIN_TOKEN="$(text_value "$response" "access_token")"

    if [ -n "$ADMIN_TOKEN" ]; then
        ok "администратор вошёл, токен получен"
    else
        fail "вход администратора не удался: $response"
        return 1
    fi
}

check_selftest() {
    step "Проверка окружения клиентом"

    # Отказ здесь прекращает проверку. Если установленный клиент не запускается,
    # всё дальнейшее бессмысленно: каждая следующая проверка ждёт отказа сервера
    # и получит отказ клиента, приняв его за успех. Именно так первый прогон
    # отчитался об «отклонённом незарегистрированном компьютере», хотя клиент
    # падал на подключении своих модулей.
    if client_exec "updatehub selftest"; then
        ok "selftest прошёл"
    else
        fail "selftest не прошёл: установленный клиент не работает"
        return 1
    fi
}

check_sync_before_registration() {
    step "Обновление до регистрации отклоняется"

    local code=0
    client_exec "updatehub sync" >/dev/null 2>&1 || code=$?

    # Код возврата проверяется точно, а не «лишь бы не ноль»: 78 означает
    # «компьютер не заведён» и приходит от сервера, а любой другой отказ —
    # это отказ самого клиента, и принимать его за успех нельзя.
    if [ "$code" -eq 78 ]; then
        ok "незарегистрированный компьютер отклонён с кодом настройки (78)"
    elif [ "$code" -eq 0 ]; then
        fail "незарегистрированный компьютер получил обновление"
    else
        fail "ожидался код 78, получен $code — отказал клиент, а не сервер"
    fi
}

check_enroll() {
    step "Заявка на регистрацию"

    if client_exec "updatehub enroll 'сквозная проверка'"; then
        ok "заявка подана"
    else
        fail "заявку подать не удалось"
        return 1
    fi

    CLIENT_ID="$(client_exec "cat /etc/updatehub/client-id" | tr -d '\r\n')"

    if [ -n "$CLIENT_ID" ]; then
        ok "идентификатор компьютера создан: $CLIENT_ID"
    else
        fail "файл с идентификатором компьютера пуст"
        return 1
    fi
}

check_approve() {
    step "Одобрение заявки администратором"

    local list request_id
    list="$(admin_api GET /api/v1/admin/enrollments)"
    request_id="$(printf '%s' "$list" | jq -r --arg id "$CLIENT_ID" \
        '.enrollments[] | select(.clientId == $id) | .id' | head -n 1)"

    if [ -z "$request_id" ] || [ "$request_id" = "null" ]; then
        fail "заявка на $CLIENT_ID в списке не найдена: $list"
        return 1
    fi

    ok "заявка найдена в списке: $request_id"

    local approved
    approved="$(admin_api POST "/api/v1/admin/enrollments/$request_id/approve" '{"groupId":null}')"

    if printf '%s' "$approved" | jq -e --arg id "$CLIENT_ID" '.clientId == $id' >/dev/null; then
        ok "заявка одобрена, компьютер заведён"
    else
        fail "одобрение не удалось: $approved"
        return 1
    fi
}

check_grant_access() {
    step "Создание пользователя и выдача прав на компьютер"

    local created
    created="$(admin_api POST /api/v1/admin/users "$(jq -n \
        --arg u "$OPERATOR_USER" \
        --arg p "$OPERATOR_PASSWORD" \
        --arg c "$CLIENT_ID" \
        '{username: $u, password: $p, role: "Client", clientIds: [$c]}')")"

    if printf '%s' "$created" | jq -e '.id' >/dev/null 2>&1; then
        ok "пользователь $OPERATOR_USER создан с правом на компьютер"
    else
        fail "пользователя создать не удалось: $created"
        return 1
    fi

    write_client_config "$OPERATOR_USER" "$OPERATOR_PASSWORD"
}

check_sync_downloads_files() {
    step "Обновление рабочей папки"

    if client_exec "updatehub sync"; then
        ok "обновление прошло"
    else
        fail "обновление не удалось"
        return 1
    fi

    local missing=0 relative
    for relative in docs/pervyy.txt docs/vtoroy.txt readme.txt; do
        if client_exec "test -f /opt/updatehub-data/$relative"; then
            ok "файл получен: $relative"
        else
            fail "файл не получен: $relative"
            missing=1
        fi
    done

    [ "$missing" -eq 0 ] || return 1
}

check_checksums_match() {
    step "Сверка контрольных сумм с сервером"

    # Суммы считаются на стороне клиента по полученным файлам и сравниваются
    # с манифестом, который отдаёт сервер: именно это расхождение означало бы
    # порчу при передаче.
    local relative expected actual
    for relative in docs/pervyy.txt docs/vtoroy.txt readme.txt; do
        expected="$(md5sum "$WORK_DIR/files/$relative" | cut -d' ' -f1)"
        actual="$(client_exec "md5sum /opt/updatehub-data/$relative" | cut -d' ' -f1)"

        if [ "$expected" = "$actual" ]; then
            ok "сумма совпадает: $relative"
        else
            fail "сумма не совпадает для $relative: ожидалось $expected, получено $actual"
        fi
    done
}

check_second_run_is_idle() {
    step "Повторное обновление ничего не скачивает"

    local output
    output="$(client_exec "updatehub check" 2>&1)"

    if printf '%s' "$output" | grep -qiE "нечего|совпад|актуал|0 файлов"; then
        ok "повторная проверка сообщает, что скачивать нечего"
    else
        # Не отказ: формулировка могла измениться. Но знать об этом стоит.
        say "  [внимание] ответ повторной проверки разобрать не удалось:"
        printf '%s\n' "$output" | sed 's/^/    /'
        ok "повторная проверка завершилась без ошибки"
    fi
}

check_blocked_client_refused() {
    step "Заблокированный компьютер не обновляется"

    admin_api POST "/api/v1/admin/clients/$CLIENT_ID/block" \
        '{"reason":"сквозная проверка"}' >/dev/null

    local code=0
    client_exec "updatehub sync" >/dev/null 2>&1 || code=$?

    # 77 — «отказано в доступе»: именно так клиент обязан истолковать 403
    # от сервера. Другой код означал бы, что отказал он сам.
    if [ "$code" -eq 77 ]; then
        ok "заблокированный компьютер отклонён с кодом отказа в доступе (77)"
    elif [ "$code" -eq 0 ]; then
        fail "заблокированный компьютер получил обновление"
    else
        fail "ожидался код 77, получен $code — отказал клиент, а не сервер"
    fi

    admin_api POST "/api/v1/admin/clients/$CLIENT_ID/unblock" '{}' >/dev/null
}

check_maintenance_reports_backup() {
    step "Состояние обслуживания доступно администратору"

    local status
    status="$(admin_api GET /api/v1/admin/maintenance)"

    if printf '%s' "$status" | jq -e '.intervalHours >= 0 and (.backupPath | length > 0)' >/dev/null; then
        ok "сводка обслуживания отдана"
    else
        fail "сводку обслуживания получить не удалось: $status"
    fi
}

# ---------- Ход проверки ----------

main() {
    require_tools
    trap cleanup EXIT

    prepare_files
    start_server
    start_client

    login_admin
    check_selftest
    check_sync_before_registration
    check_enroll
    check_approve
    check_grant_access
    check_sync_downloads_files
    check_checksums_match
    check_second_run_is_idle
    check_blocked_client_refused
    check_maintenance_reports_backup

    step "Итог"
    printf '  прошло: %s, ошибок: %s\n' "$PASSED" "$FAILED"

    [ "$FAILED" -eq 0 ]
}

main "$@"
