#!/usr/bin/env bash
# Загрузка файлов во временный каталог и перенос их в рабочую папку.

# Проверяет, что во временном каталоге хватит места.
#
# Файлы складываются во временный каталог целиком и лишь потом переносятся:
# оборванное обновление не должно оставить рабочую папку наполовину новой.
# Плата за это — двойной расход места на время загрузки, и проверять его
# нужно до начала, а не на пятом гигабайте.
#
# Аргументы: $1 — требуемый объём в байтах.
check_free_space() {
    local required="$1"
    local margin=$((FREE_SPACE_MARGIN_MB * 1024 * 1024))
    local needed=$((required + margin))

    ensure_dir "$TEMP_DIR"

    local available
    available="$(free_space_bytes "$TEMP_DIR")"

    if [ -z "$available" ]; then
        log_warn "Не удалось определить свободное место в $TEMP_DIR, проверка пропущена"
        return 0
    fi

    log_debug "Свободно $(human_size "$available"), требуется $(human_size "$needed")"

    if [ "$available" -lt "$needed" ]; then
        UH_EXIT_CODE=75 die "Недостаточно места в $TEMP_DIR: свободно $(human_size "$available"), нужно $(human_size "$needed")"
    fi
}

# Скачивает один файл во временный каталог.
#
# Докачка включена всегда: обрыв на семичасовой передаче — обычное дело,
# и начинать шесть гигабайт заново из-за него нельзя. Сервер поддерживает
# докачку по диапазонам и подтверждает неизменность файла через ETag.
#
# Аргументы: $1 — идентификатор компьютера, $2 — путь файла.
download_file() {
    local client_id="$1" path="$2"
    local target="$TEMP_DIR/$path"

    ensure_dir "$(dirname "$target")"

    local rate_option=()
    [ -n "$DOWNLOAD_RATE_LIMIT" ] && rate_option=(--limit-rate "$DOWNLOAD_RATE_LIMIT")

    local attempt=1
    while [ "$attempt" -le "$DOWNLOAD_RETRIES" ]; do
        local code
        code="$(curl --silent --show-error \
            --continue-at - \
            --location \
            --fail \
            --header "Authorization: Bearer $ACCESS_TOKEN" \
            --write-out '%{http_code}' \
            --output "$target" \
            "${rate_option[@]}" \
            --get \
            --data-urlencode "client_id=$client_id" \
            --data-urlencode "path=$path" \
            "$SERVER_URL/api/v1/files" 2>/dev/null)" && {
            log_debug "Скачан $path"
            return 0
        }

        # Файл уже скачан целиком: сервер отвечает «диапазон недостижим».
        # Для curl это ошибка, для нас — успех, и проверка суммы всё равно
        # впереди.
        if [ "$code" = "416" ]; then
            log_debug "Файл $path уже был скачан целиком"
            return 0
        fi

        # Токен мог протухнуть за время долгой загрузки: она идёт часами,
        # а access-токен живёт час.
        if [ "$code" = "401" ] && refresh_access_token; then
            log_debug "Токен обновлён, загрузка $path продолжается"
            continue
        fi

        case "$code" in
            403 | 404)
                log_error "Сервер отказал в файле $path (код $code), файл пропущен"
                rm -f "$target"
                return 1
                ;;
        esac

        log_warn "Загрузка $path прервана (код $code), попытка $attempt из $DOWNLOAD_RETRIES"
        attempt=$((attempt + 1))
        [ "$attempt" -le "$DOWNLOAD_RETRIES" ] && sleep "$DOWNLOAD_RETRY_DELAY"
    done

    log_error "Не удалось скачать $path за $DOWNLOAD_RETRIES попыток"
    return 1
}

# Скачивает все файлы из плана.
#
# Аргументы: $1 — идентификатор компьютера, $2 — файл со списком в формате md5sum.
# Возвращает 0, если скачаны все файлы.
download_all() {
    local client_id="$1" list_file="$2"
    local total failed=0 done_count=0

    total="$(wc -l <"$list_file" | tr -d ' ')"

    local hash path line
    while IFS= read -r line; do
        [ -n "$line" ] || continue
        hash="${line%%  *}"
        path="${line#*  }"

        done_count=$((done_count + 1))
        log_info "[$done_count/$total] $path"

        if ! download_file "$client_id" "$path"; then
            failed=$((failed + 1))
        fi
    done <"$list_file"

    [ "$failed" -eq 0 ]
}

# Проверяет контрольные суммы скачанного.
#
# Проверяется всё разом командой md5sum -c по тому же списку, который прислал
# сервер: формат для того и выбран. Файлы с несовпавшей суммой удаляются —
# переносить в рабочую папку заведомо испорченное нельзя.
#
# Аргументы: $1 — файл со списком в формате md5sum.
verify_downloads() {
    local list_file="$1"
    local report

    report="$(cd "$TEMP_DIR" && md5sum -c --quiet -- "$list_file" 2>&1)" && {
        log_info "Контрольные суммы совпали"
        return 0
    }

    local broken=0
    local line
    while IFS= read -r line; do
        [ -n "$line" ] || continue
        log_error "Проверка: $line"

        case "$line" in
            *": FAILED"* | *": ПОВРЕЖДЁН"*)
                broken=$((broken + 1))
                rm -f "$TEMP_DIR/${line%%:*}"
                ;;
        esac
    done <<<"$report"

    log_error "Файлов с несовпавшей суммой: $broken"
    return 1
}

# Переносит скачанное в рабочую папку.
#
# Перенос идёт по одному файлу и с заменой: рабочая папка не очищается,
# файлы, которых нет на сервере, остаются на месте — решение об их судьбе
# принимает человек, а не скрипт.
#
# Аргументы: $1 — файл со списком в формате md5sum.
apply_downloads() {
    local list_file="$1"
    local applied=0 failed=0

    ensure_dir "$DATA_DIR"

    local line path source target
    while IFS= read -r line; do
        [ -n "$line" ] || continue
        path="${line#*  }"
        source="$TEMP_DIR/$path"
        target="$DATA_DIR/$path"

        if [ ! -f "$source" ]; then
            log_error "Файл не найден во временном каталоге, пропущен: $path"
            failed=$((failed + 1))
            continue
        fi

        ensure_dir "$(dirname "$target")"

        # Перенос внутри одной файловой системы происходит мгновенно, между
        # разными — копированием. mv справляется с обоими случаями, а вот
        # частично перенесённый файл при обрыве останется испорченным:
        # его подхватит следующий обход по несовпавшей сумме.
        if mv -f "$source" "$target"; then
            applied=$((applied + 1))
        else
            log_error "Не удалось перенести файл: $path"
            failed=$((failed + 1))
        fi
    done <"$list_file"

    log_info "Обновлено файлов: $applied"
    [ "$failed" -eq 0 ]
}

# Полностью очищает временный каталог.
#
# Именно полностью: остатки прерванной загрузки — это куски файлов, которые
# при следующем запуске сервер уже может отдавать другими. Место на диске
# машины дороже сэкономленного трафика.
clear_temp_dir() {
    [ -d "$TEMP_DIR" ] || return 0

    # Проверка от опечатки в настройках: rm -rf по корню или по рабочей папке
    # закончился бы потерей всего, ради чего эта программа существует.
    case "$TEMP_DIR" in
        "" | "/" | "$DATA_DIR") die "Отказ очищать каталог '$TEMP_DIR'" ;;
    esac

    rm -rf -- "${TEMP_DIR:?}"/* "${TEMP_DIR:?}"/.[!.]* 2>/dev/null || true
    log_debug "Временный каталог очищен: $TEMP_DIR"
}
