#!/usr/bin/env bash
# Построение манифеста своей папки и разбор плана обновления.
#
# shellcheck disable=SC2034
# Переменные PLAN_* заполняет parse_plan, а читают их вызывающие из другого
# файла: статический разбор видит только присваивание и считает их лишними.

# Строит манифест каталога в формате md5sum.
#
# Формат тот же, что выдаёт сама команда md5sum: сумма, два пробела, путь.
# Сервер разбирает его как есть, а клиент потом проверяет им же скачанное
# через 'md5sum -c' — никакого другого формата в обмене не участвует.
#
# Пути перечисляются относительно каталога, поэтому md5sum вызывается с
# рабочим каталогом внутри него. Список сортируется: порядок сам по себе
# ничего не значит, но одинаковый порядок делает сравнение двух манифестов
# глазами возможным.
#
# Аргументы: $1 — каталог.
build_manifest() {
    local dir="$1"

    [ -d "$dir" ] || return 0

    # find печатает пути через нулевой байт, xargs так же их и читает:
    # пробелы и прочие знаки в именах при этом не разбивают строку.
    # Ограничение на длину командной строки xargs берёт на себя сам.
    (
        cd "$dir" || exit 0
        find . -type f -printf '%P\0' 2>/dev/null \
            | LC_ALL=C sort -z \
            | xargs -0 --no-run-if-empty md5sum -- 2>/dev/null
    )
}

# Разбирает план обновления, полученный от сервера.
#
# План приходит строками:
#   @GENERATION n   поколение манифеста сервера
#   @STATUS ok      обновлять нечего
#   @STATUS update  есть что скачать
#   @COUNT n        сколько файлов скачать
#   @SIZE n         сколько байт скачать
#   @WARN текст     замечание к манифесту, отправленному клиентом
#   !путь           файл есть на клиенте, но не на сервере
#   сумма  путь     файл нужно скачать
#
# Результат раскладывается по переменным PLAN_* и файлу PLAN_DOWNLOAD_FILE
# в формате md5sum: он же потом идёт на вход 'md5sum -c'.
#
# Аргументы: $1 — текст плана, $2 — файл для списка загрузки,
# $3 — файл для списка лишних.
parse_plan() {
    local text="$1" download_file="$2" extra_file="$3"

    PLAN_GENERATION=""
    PLAN_STATUS=""
    PLAN_COUNT=0
    PLAN_SIZE=0
    PLAN_WARNINGS=0
    PLAN_EXTRA=0
    PLAN_INVALID=0

    : >"$download_file"
    : >"$extra_file"

    local line
    while IFS= read -r line; do
        case "$line" in
            "") continue ;;
            "@GENERATION "*) PLAN_GENERATION="${line#@GENERATION }" ;;
            "@STATUS "*) PLAN_STATUS="${line#@STATUS }" ;;
            "@COUNT "*) PLAN_COUNT="${line#@COUNT }" ;;
            "@SIZE "*) PLAN_SIZE="${line#@SIZE }" ;;
            "@WARN "*)
                PLAN_WARNINGS=$((PLAN_WARNINGS + 1))
                log_warn "Сервер о манифесте: ${line#@WARN }"
                ;;
            "!"*)
                PLAN_EXTRA=$((PLAN_EXTRA + 1))
                printf '%s\n' "${line#!}" >>"$extra_file"
                ;;
            *)
                # Строка загрузки: сумма, два пробела, путь.
                local hash="${line%%  *}" path="${line#*  }"

                if [ "$hash" = "$line" ] || [ ${#hash} -ne 32 ]; then
                    PLAN_INVALID=$((PLAN_INVALID + 1))
                    log_warn "Строка плана не разобрана: $line"
                    continue
                fi

                if ! is_safe_relative_path "$path"; then
                    # Сервер такие пути и сам не выдаёт. Если он их всё же
                    # прислал, значит либо он не тот, за кого себя выдаёт,
                    # либо в нём ошибка; писать по такому пути нельзя.
                    PLAN_INVALID=$((PLAN_INVALID + 1))
                    log_error "Сервер прислал недопустимый путь, файл пропущен: $path"
                    continue
                fi

                printf '%s  %s\n' "$hash" "$path" >>"$download_file"
                ;;
        esac
    done <<<"$text"

    [ -n "$PLAN_STATUS" ] || return 1
    return 0
}
