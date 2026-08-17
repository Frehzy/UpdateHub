#!/usr/bin/env bash
# Команды программы.

# Подаёт заявку на регистрацию компьютера.
#
# Единственная команда, работающая без учётных данных: компьютер ещё не
# заведён, и предъявить ему нечего. Заявка ничего не открывает — она лишь
# сообщает администратору, что появилась машина, которую стоит завести.
command_enroll() {
    local client_id
    client_id="$(read_client_id)"

    check_server_reachable

    local comment="${1:-}"
    local fingerprint
    fingerprint="$(read_hardware_fingerprint)"

    local hostname_value os_version
    hostname_value="$(hostname 2>/dev/null || true)"
    [ -r /etc/os-release ] && os_version="$(awk -F'"' '/^PRETTY_NAME=/ { print $2 }' /etc/os-release)"

    local response status body
    response="$(http_request POST /api/v1/enroll \
        --data-urlencode "client_id=$client_id" \
        --data-urlencode "hardware_fingerprint=$fingerprint" \
        --data-urlencode "hostname=$hostname_value" \
        --data-urlencode "os_version=${os_version:-}" \
        --data-urlencode "username=${USERNAME:-}" \
        --data-urlencode "comment=$comment")"

    status="$(http_status "$response")"
    body="$(http_body "$response")"

    case "$status" in
        200)
            printf 'Заявка подана.\n'
            printf '  Идентификатор компьютера: %s\n' "$client_id"
            printf '  Номер заявки: %s\n' "$(text_pair_value "$body" "request_id")"
            printf '\nСообщите номер администратору. После одобрения выполните: updatehub sync\n'
            ;;
        000) UH_EXIT_CODE=75 die "Сервер недоступен: $SERVER_URL" ;;
        *) die "Заявка не принята (код $status): $(text_error_message "$body" "неизвестная ошибка")" ;;
    esac
}

# Сравнивает манифесты и, если задано, обновляет файлы.
#
# Аргументы: $1 — режим, «check» только показывает разницу, «sync» обновляет.
run_sync() {
    local mode="$1"
    local client_id
    client_id="$(read_client_id)"

    ensure_dir "$DATA_DIR"
    ensure_authenticated "$client_id"

    log_info "Составление манифеста каталога $DATA_DIR"

    local manifest_file
    manifest_file="$(mktemp)" || die "Не удалось создать временный файл"
    build_manifest "$DATA_DIR" >"$manifest_file"

    local own_count unsupported
    own_count="$(wc -l <"$manifest_file" | tr -d ' ')"
    unsupported="$(grep -c '^\\' "$manifest_file" 2>/dev/null || true)"
    log_info "Своих файлов: $own_count"

    if [ "${unsupported:-0}" -gt 0 ]; then
        log_warn "Файлов с непередаваемыми именами: $unsupported (перевод строки или обратный слэш в имени). Они не обновятся"
    fi

    local query="client_id=$client_id"
    [ "$mode" = "check" ] && query="$query&check=true"

    local response status body
    response="$(http_request_authorized POST "/api/v1/sync/diff?$query" \
        --header 'Content-Type: text/plain; charset=utf-8' \
        --data-binary "@$manifest_file")"

    status="$(http_status "$response")"
    body="$(http_body "$response")"
    rm -f "$manifest_file"

    # Access-токен мог протухнуть между запусками: обновляем и повторяем один раз.
    if [ "$status" = "401" ] && refresh_access_token; then
        response="$(http_request_authorized POST "/api/v1/sync/diff?$query" \
            --header 'Content-Type: text/plain; charset=utf-8' \
            --data-binary "@$manifest_file")"
        status="$(http_status "$response")"
        body="$(http_body "$response")"
    fi

    case "$status" in
        200) ;;
        000) UH_EXIT_CODE=75 die "Сервер недоступен: $SERVER_URL" ;;
        401 | 403) UH_EXIT_CODE=77 die "$(text_error_message "$body" "Доступ запрещён")" ;;
        404) UH_EXIT_CODE=78 die "$(text_error_message "$body" "Компьютер не зарегистрирован. Выполните 'updatehub enroll'")" ;;
        *) die "Сервер ответил кодом $status: $(text_error_message "$body" "неизвестная ошибка")" ;;
    esac

    local download_file extra_file
    download_file="$(mktemp)" || die "Не удалось создать временный файл"
    extra_file="$(mktemp)" || die "Не удалось создать временный файл"

    parse_plan "$body" "$download_file" "$extra_file" \
        || die "Ответ сервера не похож на план обновления"

    report_plan "$extra_file"

    if [ "$PLAN_STATUS" = "ok" ] && [ "$PLAN_INVALID" -eq 0 ]; then
        printf 'Обновлять нечего.\n'
        rm -f "$download_file" "$extra_file"
        return 0
    fi

    if [ "$mode" = "check" ]; then
        printf '\nПроверка завершена, файлы не изменялись. Для обновления: updatehub sync\n'
        rm -f "$download_file" "$extra_file"
        return 0
    fi

    check_free_space "$PLAN_SIZE"

    log_info "Загрузка во временный каталог $TEMP_DIR"
    local download_failed=0
    download_all "$client_id" "$download_file" || download_failed=1

    if ! verify_downloads "$download_file"; then
        clear_temp_dir
        rm -f "$download_file" "$extra_file"
        UH_EXIT_CODE=75 die "Скачанное не прошло проверку контрольных сумм, рабочая папка не тронута"
    fi

    apply_downloads "$download_file" || download_failed=1

    clear_temp_dir
    rm -f "$download_file" "$extra_file"

    if [ "$download_failed" -ne 0 ]; then
        UH_EXIT_CODE=75 die "Обновление завершено не полностью, повторите запуск"
    fi

    save_last_sync "$PLAN_GENERATION"
    printf '\nОбновление завершено.\n'
}

# Печатает разбор плана для человека.
report_plan() {
    local extra_file="$1"

    printf '\n'
    printf 'Поколение манифеста сервера: %s\n' "${PLAN_GENERATION:-неизвестно}"
    printf 'Файлов к загрузке: %s (%s)\n' "$PLAN_COUNT" "$(human_size "$PLAN_SIZE")"

    if [ "$PLAN_EXTRA" -gt 0 ]; then
        printf '\nФайлы есть на компьютере, но отсутствуют на сервере (%s).\n' "$PLAN_EXTRA"
        printf 'Ничего с ними не делается — решение за администратором:\n'
        sed 's/^/  /' "$extra_file"
    fi

    if [ "$PLAN_INVALID" -gt 0 ]; then
        printf '\nСтрок плана не разобрано: %s. Подробности в журнале.\n' "$PLAN_INVALID"
    fi

    printf '\n'
}

# Запоминает время и поколение последнего успешного обновления.
save_last_sync() {
    local generation="$1"

    ensure_dir "$STATE_DIR"
    {
        printf 'LAST_SYNC_AT=%s\n' "$(date '+%Y-%m-%d %H:%M:%S')"
        printf 'LAST_SYNC_GENERATION=%s\n' "$generation"
    } >"$STATE_DIR/last-sync" 2>/dev/null || log_warn "Не удалось записать отметку о синхронизации"
}

# Показывает состояние машины без обращения к серверу.
#
# Без обращения намеренно: команда должна отвечать и тогда, когда сервер
# недоступен, — именно в этот момент её и зовут.
command_status() {
    local client_id="не создан"
    [ -f "$CLIENT_ID_FILE" ] && client_id="$(tr -d ' \t\n\r' <"$CLIENT_ID_FILE")"

    printf 'Сервер:                 %s\n' "$SERVER_URL"
    printf 'Идентификатор:          %s\n' "$client_id"
    printf 'Отпечаток железа:       %s\n' "$(read_hardware_fingerprint)"
    printf 'Рабочая папка:          %s\n' "$DATA_DIR"
    printf 'Временный каталог:      %s\n' "$TEMP_DIR"

    if [ -d "$DATA_DIR" ]; then
        local count size
        count="$(find "$DATA_DIR" -type f 2>/dev/null | wc -l | tr -d ' ')"
        size="$(du -sb "$DATA_DIR" 2>/dev/null | awk '{print $1}')"
        printf 'Своих файлов:           %s (%s)\n' "$count" "$(human_size "${size:-0}")"
    else
        printf 'Своих файлов:           рабочая папка не создана\n'
    fi

    printf 'Свободно на диске:      %s\n' "$(human_size "$(free_space_bytes "$TEMP_DIR")")"

    if [ -f "$STATE_DIR/last-sync" ]; then
        # shellcheck disable=SC1091
        . "$STATE_DIR/last-sync"
        printf 'Последнее обновление:   %s (поколение %s)\n' "${LAST_SYNC_AT:-?}" "${LAST_SYNC_GENERATION:-?}"
    else
        printf 'Последнее обновление:   не выполнялось\n'
    fi

    if [ -f "$(tokens_file)" ]; then
        printf 'Вход:                   токены сохранены\n'
    else
        printf 'Вход:                   не выполнялся\n'
    fi
}

# Выполняет начальную заливку из каталога на съёмном носителе.
#
# Шесть гигабайт по каналу 2 Мбит/с идут около семи часов, поэтому первое
# наполнение делается с флешки, а сервер потом лишь досылает разницу.
# Файлы копируются, а не переносятся: носитель может понадобиться для
# следующей машины.
#
# Аргументы: $1 — каталог на носителе.
command_seed() {
    local source_dir="${1:-}"

    [ -n "$source_dir" ] || die "Укажите каталог: updatehub seed /media/usb/updatehub"
    [ -d "$source_dir" ] || die "Каталог не найден: $source_dir"

    ensure_dir "$DATA_DIR"

    local total size
    total="$(find "$source_dir" -type f 2>/dev/null | wc -l | tr -d ' ')"
    size="$(du -sb "$source_dir" 2>/dev/null | awk '{print $1}')"

    [ "$total" -gt 0 ] || die "В каталоге $source_dir нет файлов"

    printf 'Заливка из %s: файлов %s (%s)\n' "$source_dir" "$total" "$(human_size "${size:-0}")"

    local available
    available="$(free_space_bytes "$DATA_DIR")"
    if [ -n "$available" ] && [ -n "$size" ] && [ "$available" -lt "$size" ]; then
        UH_EXIT_CODE=75 die "Недостаточно места в $DATA_DIR: свободно $(human_size "$available"), нужно $(human_size "$size")"
    fi

    # Копирование с сохранением структуры каталогов. cp -a переносит и время
    # изменения: сервер сравнивает файлы по сумме, но одинаковое время
    # избавляет от лишнего пересчёта на стороне клиента при следующем обходе.
    (cd "$source_dir" && tar -cf - .) | (cd "$DATA_DIR" && tar -xf -) \
        || die "Не удалось скопировать файлы из $source_dir"

    printf 'Файлы скопированы.\n'

    # Если носитель принесли вместе с манифестом сервера, сумма проверяется
    # сразу: разбираться с испорченной флешкой лучше здесь, а не через семь
    # часов докачки.
    local manifest="$source_dir/manifest.md5"
    if [ -f "$manifest" ]; then
        printf 'Проверка контрольных сумм по %s\n' "$manifest"
        if (cd "$DATA_DIR" && md5sum -c --quiet -- "$manifest"); then
            printf 'Все суммы совпали.\n'
        else
            log_warn "Часть файлов не совпала с манифестом носителя — их дошлёт сервер"
        fi
    fi

    printf '\nТеперь выполните: updatehub sync\n'
}

# Проверяет окружение перед первым запуском.
command_selftest() {
    local problems=0

    printf 'Проверка окружения\n\n'

    local command_name
    for command_name in curl md5sum find xargs awk sed df tar flock; do
        if command -v "$command_name" >/dev/null 2>&1; then
            printf '  [ок]    %s\n' "$command_name"
        else
            printf '  [нет]   %s\n' "$command_name"
            problems=$((problems + 1))
        fi
    done

    printf '\nПроверка настроек\n\n'
    printf '  Файл настроек:      %s\n' "$UH_CONFIG_FILE"
    printf '  Сервер:             %s\n' "$SERVER_URL"
    printf '  Рабочая папка:      %s\n' "$DATA_DIR"

    local path
    for path in "$DATA_DIR" "$TEMP_DIR" "$STATE_DIR"; do
        if mkdir -p "$path" 2>/dev/null && [ -w "$path" ]; then
            printf '  [ок]    доступен на запись: %s\n' "$path"
        else
            printf '  [нет]   недоступен на запись: %s\n' "$path"
            problems=$((problems + 1))
        fi
    done

    printf '\nПроверка связи\n\n'
    local response status
    response="$(http_request GET /health)"
    status="$(http_status "$response")"

    if [ "$status" = "200" ]; then
        printf '  [ок]    сервер отвечает\n'
    else
        printf '  [нет]   сервер не отвечает (код %s)\n' "$status"
        problems=$((problems + 1))
    fi

    printf '\n'
    if [ "$problems" -eq 0 ]; then
        printf 'Замечаний нет.\n'
        return 0
    fi

    printf 'Замечаний: %s\n' "$problems"
    return 1
}
