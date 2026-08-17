#!/usr/bin/env bash
# Обращения к серверу.
#
# Ответ и код состояния возвращаются вместе: curl по умолчанию молчит об
# ошибочном коде, а различать «сервер отказал» и «сервера нет» приходится
# на каждом шагу. Поэтому код состояния дописывается последней строкой,
# а разбирают его подпрограммы http_status и http_body.

# Выполняет запрос и печатает тело ответа, а последней строкой — код состояния.
#
# Аргументы: $1 — метод, $2 — путь от корня сервера, далее — дополнительные
# параметры curl.
http_request() {
    local method="$1" path="$2"
    shift 2

    local response
    response="$(curl --silent --show-error \
        --request "$method" \
        --max-time "$REQUEST_TIMEOUT" \
        --write-out $'\n%{http_code}' \
        "$@" \
        "$SERVER_URL$path" 2>&1)" || {
        # Сюда попадает и отказ разрешения имени, и отказ соединения:
        # для вызывающей стороны это одно и то же — сервера сейчас нет.
        printf '%s\n000\n' "$response"
        return 0
    }

    printf '%s\n' "$response"
}

# Выполняет запрос с заголовком авторизации.
http_request_authorized() {
    local method="$1" path="$2"
    shift 2

    http_request "$method" "$path" --header "Authorization: Bearer $ACCESS_TOKEN" "$@"
}

# Возвращает код состояния из результата http_request.
http_status() {
    printf '%s\n' "$1" | tail -n 1
}

# Возвращает тело ответа из результата http_request.
http_body() {
    printf '%s\n' "$1" | sed '$d'
}

# Проверяет, что сервер отвечает.
#
# Отдельная проверка нужна ради внятного сообщения: без неё первая же попытка
# входа сообщала бы «неверный логин или пароль» при выключенном сервере.
check_server_reachable() {
    local response status
    response="$(http_request GET /health)"
    status="$(http_status "$response")"

    case "$status" in
        200) return 0 ;;
        000) die "Сервер недоступен: $SERVER_URL. Проверьте адрес и сеть" ;;
        *) die "Сервер отвечает с кодом $status — проверьте адрес $SERVER_URL" ;;
    esac
}
