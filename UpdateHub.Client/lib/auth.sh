#!/usr/bin/env bash
# Вход на сервер и хранение токенов.
#
# shellcheck disable=SC2153
# Настройки USERNAME и PASSWORD и локальные username и password различаются
# только регистром — это не опечатка: первые приходят из файла настроек,
# вторые могут быть спрошены у человека.

ACCESS_TOKEN=""
REFRESH_TOKEN=""

# Возвращает путь к файлу с токенами.
tokens_file() {
    printf '%s/tokens\n' "$STATE_DIR"
}

# Читает сохранённые токены.
#
# Отсутствие файла — не ошибка: значит, вход ещё не выполнялся.
load_tokens() {
    local file
    file="$(tokens_file)"

    [ -f "$file" ] || return 0

    # shellcheck disable=SC1090
    . "$file"
}

# Сохраняет токены.
#
# Права 600 обязательны: refresh-токен действует неделю, и любой, кто его
# прочитает, будет неделю работать от имени этого пользователя.
save_tokens() {
    local file
    file="$(tokens_file)"

    ensure_dir "$STATE_DIR"

    local temporary="$file.new"
    umask 077
    {
        printf 'ACCESS_TOKEN=%q\n' "$ACCESS_TOKEN"
        printf 'REFRESH_TOKEN=%q\n' "$REFRESH_TOKEN"
    } >"$temporary" || die "Не удалось записать токены в $temporary"

    chmod 600 "$temporary"
    mv -f "$temporary" "$file" || die "Не удалось сохранить токены в $file"
}

# Удаляет сохранённые токены.
clear_tokens() {
    ACCESS_TOKEN=""
    REFRESH_TOKEN=""
    rm -f "$(tokens_file)"
}

# Записывает одно поле формы в файл параметров curl.
#
# Формат файла параметров: ключ и значение в одной строке, значение
# в двойных кавычках. Внутри кавычек обратный слэш и сама кавычка обязаны
# экранироваться — пароль с такими знаками иначе разорвал бы строку,
# и вход завершался бы «неверным паролем» без всякого объяснения.
write_curl_field() {
    local name="$1" value="$2"

    local escaped="${value//\\/\\\\}"
    escaped="${escaped//\"/\\\"}"

    printf -- '--data-urlencode "%s=%s"\n' "$name" "$escaped"
}

# Выполняет вход по логину и паролю.
#
# Пароль передаётся curl через файл параметров, а не аргументом командной
# строки: аргументы видны в выводе ps любому пользователю машины.
login_with_password() {
    local username="$1" password="$2" client_id="$3"

    local fields_file
    fields_file="$(mktemp)" || die "Не удалось создать временный файл"
    chmod 600 "$fields_file"

    {
        write_curl_field "username" "$username"
        write_curl_field "password" "$password"
        write_curl_field "client_id" "$client_id"

        # Сведения о машине передаются вместе со входом: отдельного запроса
        # для них нет, а администратору нужно видеть, что за компьютер вышел
        # на связь и когда.
        local fact
        while IFS= read -r fact; do
            write_curl_field "${fact%%=*}" "${fact#*=}"
        done < <(collect_machine_facts)
    } >"$fields_file"

    local response status body
    response="$(http_request POST /api/v1/auth/login --config "$fields_file")"
    rm -f "$fields_file"

    status="$(http_status "$response")"
    body="$(http_body "$response")"

    case "$status" in
        200) ;;
        000) UH_EXIT_CODE=75 die "Сервер недоступен: $SERVER_URL" ;;
        401) UH_EXIT_CODE=77 die "$(text_error_message "$body" "Неверный логин или пароль")" ;;
        403)
            # Компьютер известен серверу, но прав на него нет: это решается
            # не на машине, а администратором.
            UH_EXIT_CODE=77 die "$(text_error_message "$body" "Нет прав на работу за этим компьютером")"
            ;;
        404)
            UH_EXIT_CODE=78 die "$(text_error_message "$body" "Компьютер не зарегистрирован. Выполните 'updatehub enroll'")"
            ;;
        *) UH_EXIT_CODE=75 die "Вход не удался, код ответа $status: $(text_error_message "$body" "неизвестная ошибка")" ;;
    esac

    ACCESS_TOKEN="$(text_pair_value "$body" "access_token")"
    REFRESH_TOKEN="$(text_pair_value "$body" "refresh_token")"

    [ -n "$ACCESS_TOKEN" ] || die "Сервер не вернул access-токен"

    save_tokens

    if [ "$(text_pair_value "$body" "must_change_password")" = "1" ]; then
        log_warn "Пароль временный: смените его в панели управления"
    fi

    log_info "Вход выполнен: $(text_pair_value "$body" "username")"
}

# Обновляет access-токен по сохранённому refresh-токену.
#
# Возвращает 0 при успехе. Неудача — не повод прерывать работу: вызывающая
# сторона попробует войти заново по логину и паролю.
refresh_access_token() {
    [ -n "$REFRESH_TOKEN" ] || return 1

    local response status body
    response="$(http_request POST /api/v1/auth/refresh --data-urlencode "refresh_token=$REFRESH_TOKEN")"
    status="$(http_status "$response")"
    body="$(http_body "$response")"

    if [ "$status" != "200" ]; then
        log_debug "Обновить токен не удалось, код $status"
        return 1
    fi

    ACCESS_TOKEN="$(text_pair_value "$body" "access_token")"
    REFRESH_TOKEN="$(text_pair_value "$body" "refresh_token")"
    save_tokens

    log_debug "Access-токен обновлён"
    return 0
}

# Обеспечивает наличие действующего токена.
#
# Порядок такой: сохранённый refresh-токен, затем вход по паролю. Пароль
# спрашивается у человека только если его нет в настройках — обновление
# по расписанию обязано проходить без участия оператора.
ensure_authenticated() {
    local client_id="$1"

    load_tokens

    if [ -n "$ACCESS_TOKEN" ] && refresh_access_token; then
        return 0
    fi

    local username="$USERNAME" password="$PASSWORD"

    if [ -z "$username" ]; then
        if [ ! -t 0 ]; then
            UH_EXIT_CODE=78 die "В настройках не задан USERNAME, а спросить некого: запуск не из терминала"
        fi
        printf 'Логин: ' >&2
        IFS= read -r username
    fi

    if [ -z "$password" ]; then
        if [ ! -t 0 ]; then
            UH_EXIT_CODE=78 die "В настройках не задан PASSWORD, а спросить некого: запуск не из терминала"
        fi
        printf 'Пароль: ' >&2
        IFS= read -rs password
        printf '\n' >&2
    fi

    login_with_password "$username" "$password" "$client_id"
}

# Отзывает refresh-токен на сервере и удаляет его с машины.
logout() {
    load_tokens

    if [ -n "$ACCESS_TOKEN" ] && [ -n "$REFRESH_TOKEN" ]; then
        http_request_authorized POST /api/v1/auth/logout \
            --data-urlencode "refresh_token=$REFRESH_TOKEN" >/dev/null 2>&1 || true
    fi

    clear_tokens
    log_info "Токены удалены"
}
