#!/usr/bin/env bash
#
# Тесты клиента.
#
# Сторонних средств не требуется: на машине в закрытом контуре их взять
# неоткуда, а тесты должны запускаться и там. Проверки двух видов:
#
#   1. Подпрограммы по отдельности — разбор ответов, построение манифеста,
#      разбор плана, проверка путей и настроек.
#   2. Весь ход обновления против подставного сервера — вход, сравнение
#      манифестов, загрузка с докачкой, проверка сумм, перенос файлов.
#
# Второй вид проверяет именно клиент: соответствие протокола проверяют
# тесты сервера на C#, где поднимается настоящее приложение.

set -uo pipefail

CLIENT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK_DIR="$(mktemp -d)"
FAKE_PORT="${FAKE_PORT:-8099}"
FAKE_PID=""

TESTS_RUN=0
TESTS_FAILED=0
CURRENT_TEST=""

# ---------- средства проверки ----------

start_test() {
    CURRENT_TEST="$1"
    TESTS_RUN=$((TESTS_RUN + 1))
}

fail_test() {
    TESTS_FAILED=$((TESTS_FAILED + 1))
    printf '  [ПРОВАЛ] %s\n' "$CURRENT_TEST"
    printf '           %s\n' "$1"
}

pass_test() {
    printf '  [ок]     %s\n' "$CURRENT_TEST"
}

assert_equals() {
    local expected="$1" actual="$2"

    if [ "$expected" = "$actual" ]; then
        pass_test
    else
        fail_test "ожидалось «$expected», получено «$actual»"
    fi
}

assert_contains() {
    local haystack="$1" needle="$2"

    case "$haystack" in
        *"$needle"*) pass_test ;;
        *) fail_test "в результате нет «$needle»; результат: $haystack" ;;
    esac
}

assert_not_contains() {
    local haystack="$1" needle="$2"

    case "$haystack" in
        *"$needle"*) fail_test "в результате не должно быть «$needle»" ;;
        *) pass_test ;;
    esac
}

assert_success() {
    if [ "$1" -eq 0 ]; then
        pass_test
    else
        fail_test "ожидался успех, код возврата $1"
    fi
}

assert_failure() {
    if [ "$1" -ne 0 ]; then
        pass_test
    else
        fail_test "ожидалась ошибка, но код возврата 0"
    fi
}

assert_file_content() {
    local path="$1" expected="$2"

    if [ ! -f "$path" ]; then
        fail_test "файл не найден: $path"
        return
    fi

    local actual
    actual="$(cat "$path")"
    assert_equals "$expected" "$actual"
}

section() {
    printf '\n%s\n' "$1"
}

# ---------- подготовка ----------

cleanup() {
    [ -n "$FAKE_PID" ] && kill "$FAKE_PID" 2>/dev/null
    rm -rf "$WORK_DIR"
}
trap cleanup EXIT

# Подпрограммы подключаются напрямую: проверять их через запуск всей
# программы значило бы каждый раз поднимать сервер и заводить настройки.
# shellcheck source=../lib/common.sh
. "$CLIENT_DIR/lib/common.sh"
# shellcheck source=../lib/config.sh
. "$CLIENT_DIR/lib/config.sh"
# shellcheck source=../lib/manifest.sh
. "$CLIENT_DIR/lib/manifest.sh"
# shellcheck source=../lib/identity.sh
. "$CLIENT_DIR/lib/identity.sh"

# Журнал в тестах не нужен: он мешает читать вывод.
UH_VERBOSITY=0
UH_LOG_FILE=""

# ---------- разбор текстовых ответов ----------

section "Запуск через символьную ссылку"

# Установщик кладёт клиент в /opt/updatehub и делает ссылку в /usr/local/bin,
# поэтому обычный запуск по имени идёт через ссылку. Проверка появилась после
# того, как сквозная проверка нашла отказ установленного клиента: каталог
# модулей вычислялся от ссылки, и подключение падало на первой строке.
# Остальные проверки этого увидеть не могли — они звали клиент по настоящему пути.
LINK_DIR="$WORK_DIR/ssylka"
mkdir -p "$LINK_DIR"
ln -sf "$CLIENT_DIR/updatehub" "$LINK_DIR/updatehub"

LINK_OUTPUT="$("$LINK_DIR/updatehub" help 2>&1)" && LINK_CODE=0 || LINK_CODE=$?

start_test "клиент, вызванный через ссылку, находит свои модули"
assert_not_contains "$LINK_OUTPUT" "No such file or directory"

start_test "клиент, вызванный через ссылку, завершается успешно"
assert_equals "0" "$LINK_CODE"

start_test "клиент, вызванный через ссылку, печатает подсказку"
assert_contains "$LINK_OUTPUT" "Использование: updatehub"

section "Разбор ответов «ключ=значение»"

start_test "значение по ключу"
assert_equals "abc" "$(text_pair_value $'access_token=abc\nrole=Admin' 'access_token')"

start_test "знак равенства внутри значения сохраняется"
assert_equals "Ожидалось a=b" "$(text_pair_value 'error=Ожидалось a=b' 'error')"

start_test "отсутствующий ключ даёт пустую строку"
assert_equals "" "$(text_pair_value 'role=Admin' 'access_token')"

start_test "берётся первое вхождение ключа"
assert_equals "первый" "$(text_pair_value $'x=первый\nx=второй' 'x')"

start_test "пустое значение остаётся пустым"
assert_equals "" "$(text_pair_value $'client_id=\nrole=Admin' 'client_id')"

start_test "сообщение об ошибке достаётся из ответа"
assert_equals "Неверный пароль" "$(text_error_message 'error=Неверный пароль' 'запасной')"

start_test "без сообщения возвращается запасной текст"
assert_equals "запасной" "$(text_error_message 'status=ok' 'запасной')"

# ---------- размеры ----------

section "Размеры"

start_test "байты"
assert_equals "512 Б" "$(human_size 512)"

start_test "мегабайты"
assert_equals "1.0 МБ" "$(human_size 1048576)"

start_test "шестигигабайтный образ"
assert_equals "6.0 ГБ" "$(human_size 6442450944)"

start_test "дробная часть не теряется"
assert_equals "5.5 ГБ" "$(human_size 5905580032)"

start_test "ноль"
assert_equals "0 Б" "$(human_size 0)"

# ---------- передача пароля ----------

section "Подготовка полей формы для curl"

# Подключается отдельно: подпрограмма нужна только здесь, а вся остальная
# работа с входом требует поднятого сервера.
# shellcheck source=../lib/auth.sh
. "$CLIENT_DIR/lib/auth.sh"

start_test "обычное значение берётся в кавычки"
assert_equals '--data-urlencode "username=ivanov"' "$(write_curl_field username ivanov)"

start_test "кавычка в пароле экранируется"
assert_equals '--data-urlencode "password=parol\"s-kavychkoy"' \
    "$(write_curl_field password 'parol"s-kavychkoy')"

start_test "обратный слэш в пароле экранируется"
assert_equals '--data-urlencode "password=parol\\slesh"' \
    "$(write_curl_field password 'parol\slesh')"

start_test "пробел в значении не разрывает строку"
assert_equals '--data-urlencode "cpu_info=Intel Core i5"' \
    "$(write_curl_field cpu_info 'Intel Core i5')"

# ---------- безопасность путей ----------

section "Проверка путей, присланных сервером"

for safe_path in "docs/file.txt" "file.txt" "a/b/c/d.iso" "имя с пробелом.txt" "a..b/c.txt"; do
    start_test "принимается: $safe_path"
    is_safe_relative_path "$safe_path"
    assert_success $?
done

for unsafe_path in "/etc/passwd" "../etc/passwd" ".." "docs/../../etc/passwd" "docs/.." ""; do
    start_test "отклоняется: ${unsafe_path:-пустой путь}"
    is_safe_relative_path "$unsafe_path"
    assert_failure $?
done

# ---------- построение манифеста ----------

section "Построение манифеста своей папки"

DATA_SAMPLE="$WORK_DIR/data-sample"
mkdir -p "$DATA_SAMPLE/docs/vnutri"
printf 'hello' >"$DATA_SAMPLE/privet.txt"
printf 'hello' >"$DATA_SAMPLE/docs/vnutri/glubzhe.txt"
printf 'other' >"$DATA_SAMPLE/docs/imya s probelom.txt"

MANIFEST="$(build_manifest "$DATA_SAMPLE")"

start_test "сумма совпадает с выводом md5sum"
assert_contains "$MANIFEST" "5d41402abc4b2a76b9719d911017c592  privet.txt"

start_test "вложенные каталоги перечисляются относительным путём"
assert_contains "$MANIFEST" "docs/vnutri/glubzhe.txt"

start_test "имя с пробелом не разбивает строку"
assert_contains "$MANIFEST" "docs/imya s probelom.txt"

start_test "перечислены все три файла"
assert_equals "3" "$(printf '%s\n' "$MANIFEST" | wc -l | tr -d ' ')"

start_test "ведущего «./» в путях нет"
assert_not_contains "$MANIFEST" "  ./"

start_test "пустой каталог даёт пустой манифест"
mkdir -p "$WORK_DIR/pusto"
assert_equals "" "$(build_manifest "$WORK_DIR/pusto")"

start_test "несуществующий каталог не приводит к ошибке"
assert_equals "" "$(build_manifest "$WORK_DIR/takogo-net")"

# ---------- разбор плана ----------

section "Разбор плана обновления"

DOWNLOAD_LIST="$WORK_DIR/plan-download"
EXTRA_LIST="$WORK_DIR/plan-extra"

PLAN_TEXT="@GENERATION 7
@STATUS update
@COUNT 2
@SIZE 6400000000
@WARN строка 3: путь должен быть относительным
!starye/lishniy.txt
5d41402abc4b2a76b9719d911017c592  docs/privet.txt
098f6bcd4621d373cade4e832627b4f6  astra176.iso"

parse_plan "$PLAN_TEXT" "$DOWNLOAD_LIST" "$EXTRA_LIST"
PARSE_CODE=$?

start_test "план разобран"
assert_success "$PARSE_CODE"

start_test "поколение прочитано"
assert_equals "7" "$PLAN_GENERATION"

start_test "состояние прочитано"
assert_equals "update" "$PLAN_STATUS"

start_test "количество прочитано"
assert_equals "2" "$PLAN_COUNT"

start_test "объём прочитан"
assert_equals "6400000000" "$PLAN_SIZE"

start_test "замечание сервера учтено"
assert_equals "1" "$PLAN_WARNINGS"

start_test "лишний файл учтён"
assert_equals "1" "$PLAN_EXTRA"

start_test "лишний файл записан без восклицательного знака"
assert_file_content "$EXTRA_LIST" "starye/lishniy.txt"

start_test "список загрузки годится для md5sum -c"
assert_contains "$(cat "$DOWNLOAD_LIST")" "5d41402abc4b2a76b9719d911017c592  docs/privet.txt"

start_test "в списке загрузки две строки"
assert_equals "2" "$(wc -l <"$DOWNLOAD_LIST" | tr -d ' ')"

start_test "состояние «обновлять нечего» разбирается"
parse_plan "@GENERATION 7
@STATUS ok
@COUNT 0
@SIZE 0" "$DOWNLOAD_LIST" "$EXTRA_LIST"
assert_equals "ok" "$PLAN_STATUS"

start_test "опасный путь от сервера отбрасывается"
parse_plan "@STATUS update
@COUNT 1
@SIZE 5
5d41402abc4b2a76b9719d911017c592  ../../etc/passwd" "$DOWNLOAD_LIST" "$EXTRA_LIST"
assert_equals "1" "$PLAN_INVALID"

start_test "опасный путь не попал в список загрузки"
assert_equals "0" "$(wc -l <"$DOWNLOAD_LIST" | tr -d ' ')"

start_test "строка с неверной длиной суммы отбрасывается"
parse_plan "@STATUS update
@COUNT 1
@SIZE 5
korotkaya  docs/file.txt" "$DOWNLOAD_LIST" "$EXTRA_LIST"
assert_equals "1" "$PLAN_INVALID"

start_test "ответ без состояния признаётся неразобранным"
parse_plan "совсем не план" "$DOWNLOAD_LIST" "$EXTRA_LIST"
assert_failure $?

# ---------- проверка настроек ----------

section "Проверка настроек"

check_config_in_subshell() {
    # Проверка вызывает die, а он завершает процесс: запуск в подоболочке
    # позволяет посмотреть на код возврата, не прерывая прогон тестов.
    (
        SERVER_URL="$1"
        DATA_DIR="$2"
        TEMP_DIR="$3"
        STATE_DIR="/var/lib/updatehub"
        validate_config
    ) >/dev/null 2>&1
}

start_test "правильные настройки принимаются"
check_config_in_subshell "http://server:8080" "/home/data" "/var/tmp/updatehub"
assert_success $?

start_test "адрес без схемы отклоняется"
check_config_in_subshell "server:8080" "/home/data" "/var/tmp/updatehub"
assert_failure $?

start_test "пустой адрес отклоняется"
check_config_in_subshell "" "/home/data" "/var/tmp/updatehub"
assert_failure $?

start_test "относительная рабочая папка отклоняется"
check_config_in_subshell "http://server:8080" "data" "/var/tmp/updatehub"
assert_failure $?

start_test "корень в качестве рабочей папки отклоняется"
check_config_in_subshell "http://server:8080" "/" "/var/tmp/updatehub"
assert_failure $?

start_test "совпадение временного каталога с рабочей папкой отклоняется"
check_config_in_subshell "http://server:8080" "/home/data" "/home/data"
assert_failure $?

start_test "временный каталог внутри рабочей папки отклоняется"
check_config_in_subshell "http://server:8080" "/home/data" "/home/data/tmp"
assert_failure $?

start_test "замыкающая косая черта в адресе убирается"
assert_equals "http://server:8080" "$(
    SERVER_URL="http://server:8080/"
    DATA_DIR="/home/data"
    TEMP_DIR="/var/tmp/updatehub"
    STATE_DIR="/var/lib/updatehub"
    validate_config
    printf '%s' "$SERVER_URL"
)"

# ---------- идентификатор компьютера ----------

section "Идентификатор компьютера"

start_test "создаётся значение вида UUID"
UUID_VALUE="$(generate_uuid)"
if printf '%s' "$UUID_VALUE" | grep -Eq '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'; then
    pass_test
else
    fail_test "не похоже на UUID: $UUID_VALUE"
fi

start_test "идентификатор сохраняется и не меняется при повторном чтении"
CLIENT_ID_FILE="$WORK_DIR/client-id"
FIRST_ID="$(read_client_id)"
SECOND_ID="$(read_client_id)"
assert_equals "$FIRST_ID" "$SECOND_ID"

start_test "пустой файл идентификатора заменяется новым значением"
: >"$CLIENT_ID_FILE"
NEW_ID="$(read_client_id 2>/dev/null)"
if [ -n "$NEW_ID" ] && [ "$NEW_ID" != "$FIRST_ID" ]; then
    pass_test
else
    fail_test "ожидался новый идентификатор, получено «$NEW_ID»"
fi

# ---------- свободное место ----------

section "Свободное место"

start_test "определяется для существующего каталога"
SPACE="$(free_space_bytes "$WORK_DIR")"
if [ -n "$SPACE" ] && [ "$SPACE" -gt 0 ]; then
    pass_test
else
    fail_test "не удалось определить: «$SPACE»"
fi

start_test "определяется и для ещё не созданного подкаталога"
SPACE="$(free_space_bytes "$WORK_DIR/net/takogo/puti")"
if [ -n "$SPACE" ] && [ "$SPACE" -gt 0 ]; then
    pass_test
else
    fail_test "не удалось определить: «$SPACE»"
fi

# ---------- весь ход обновления ----------

section "Обновление целиком против подставного сервера"

if ! command -v python3 >/dev/null 2>&1; then
    printf '  [пропуск] python3 не найден, проверки против сервера пропущены\n'
else
    SERVER_FILES="$WORK_DIR/server-files"
    mkdir -p "$SERVER_FILES/docs"
    printf 'hello' >"$SERVER_FILES/docs/privet.txt"
    printf 'soderzhimoe obraza' >"$SERVER_FILES/astra176.iso"
    printf 'tretiy fayl' >"$SERVER_FILES/docs/tretiy.txt"

    # Каждая проверка получает свой набор каталогов и свой файл настроек:
    # обновление меняет состояние на диске, и общий каталог сделал бы
    # порядок выполнения значимым.
    prepare_client() {
        local name="$1"
        CLIENT_HOME="$WORK_DIR/$name"
        mkdir -p "$CLIENT_HOME/data" "$CLIENT_HOME/state" "$CLIENT_HOME/etc"

        cat >"$CLIENT_HOME/etc/updatehub.conf" <<EOF
SERVER_URL="http://127.0.0.1:$FAKE_PORT"
DATA_DIR="$CLIENT_HOME/data"
TEMP_DIR="$CLIENT_HOME/temp"
STATE_DIR="$CLIENT_HOME/state"
CLIENT_ID_FILE="$CLIENT_HOME/etc/client-id"
LOG_FILE=""
USERNAME="ivanov"
PASSWORD="parol12345"
FREE_SPACE_MARGIN_MB="1"
DOWNLOAD_RETRIES="3"
DOWNLOAD_RETRY_DELAY="1"
REQUEST_TIMEOUT="15"
EOF
    }

    run_client() {
        "$CLIENT_DIR/updatehub" --config "$CLIENT_HOME/etc/updatehub.conf" "$@" 2>&1
    }

    start_fake_server() {
        FAKE_FILES_DIR="$SERVER_FILES" FAKE_PORT="$FAKE_PORT" "$@" \
            python3 "$CLIENT_DIR/tests/fake-server.py" >/dev/null 2>&1 &
        FAKE_PID=$!

        local attempt=0
        while [ "$attempt" -lt 50 ]; do
            if curl -s "http://127.0.0.1:$FAKE_PORT/health" >/dev/null 2>&1; then
                return 0
            fi
            attempt=$((attempt + 1))
            sleep 0.1
        done

        printf '  [ПРОВАЛ] подставной сервер не поднялся\n'
        return 1
    }

    stop_fake_server() {
        [ -n "$FAKE_PID" ] && kill "$FAKE_PID" 2>/dev/null
        wait "$FAKE_PID" 2>/dev/null
        FAKE_PID=""
    }

    if start_fake_server; then
        # --- заявка на регистрацию ---
        prepare_client "zayavka"
        OUTPUT="$(run_client enroll "проверка")"

        start_test "заявка принята"
        assert_contains "$OUTPUT" "Заявка подана"

        start_test "в заявке показан номер"
        assert_contains "$OUTPUT" "zayavka-1"

        start_test "идентификатор компьютера создан"
        if [ -s "$CLIENT_HOME/etc/client-id" ]; then pass_test; else fail_test "файл пуст"; fi

        # --- проверка без изменений ---
        prepare_client "proverka"
        OUTPUT="$(run_client check)"

        start_test "проверка показывает объём к загрузке"
        assert_contains "$OUTPUT" "Файлов к загрузке: 3"

        start_test "проверка ничего не скачивает"
        assert_equals "0" "$(find "$CLIENT_HOME/data" -type f | wc -l | tr -d ' ')"

        # --- полное обновление ---
        prepare_client "obnovlenie"
        OUTPUT="$(run_client sync)"

        start_test "обновление завершилось"
        assert_contains "$OUTPUT" "Обновление завершено"

        start_test "файл из подкаталога перенесён в рабочую папку"
        assert_file_content "$CLIENT_HOME/data/docs/privet.txt" "hello"

        start_test "файл из корня перенесён в рабочую папку"
        assert_file_content "$CLIENT_HOME/data/astra176.iso" "soderzhimoe obraza"

        start_test "перенесены все три файла"
        assert_equals "3" "$(find "$CLIENT_HOME/data" -type f | wc -l | tr -d ' ')"

        start_test "временный каталог очищен полностью"
        assert_equals "0" "$(find "$CLIENT_HOME/temp" -type f 2>/dev/null | wc -l | tr -d ' ')"

        start_test "отметка о синхронизации сохранена"
        assert_contains "$(cat "$CLIENT_HOME/state/last-sync" 2>/dev/null)" "LAST_SYNC_GENERATION=7"

        start_test "токены сохранены с правами 600"
        assert_equals "600" "$(stat -c '%a' "$CLIENT_HOME/state/tokens" 2>/dev/null)"

        # --- повторный запуск ---
        OUTPUT="$(run_client sync)"

        start_test "повторное обновление сообщает, что обновлять нечего"
        assert_contains "$OUTPUT" "Обновлять нечего"

        # --- лишние файлы ---
        printf 'staryy fayl' >"$CLIENT_HOME/data/lishniy.txt"
        OUTPUT="$(run_client sync)"

        start_test "лишний файл отмечен"
        assert_contains "$OUTPUT" "lishniy.txt"

        start_test "лишний файл не удалён"
        assert_file_content "$CLIENT_HOME/data/lishniy.txt" "staryy fayl"

        # --- изменение файла на сервере ---
        printf 'novoe soderzhimoe' >"$SERVER_FILES/docs/privet.txt"
        OUTPUT="$(run_client sync)"

        start_test "изменённый файл перекачан"
        assert_file_content "$CLIENT_HOME/data/docs/privet.txt" "novoe soderzhimoe"

        printf 'hello' >"$SERVER_FILES/docs/privet.txt"

        # --- неверный пароль ---
        prepare_client "parol"
        sed -i 's/PASSWORD="parol12345"/PASSWORD="ne-tot-parol"/' "$CLIENT_HOME/etc/updatehub.conf"
        OUTPUT="$(run_client sync)"
        CODE=$?

        start_test "неверный пароль отклонён"
        assert_contains "$OUTPUT" "Неверный логин или пароль"

        start_test "неверный пароль даёт код возврата «нет доступа»"
        assert_equals "77" "$CODE"

        # --- пароль со знаками, требующими экранирования ---
        stop_fake_server
        prepare_client "slozhnyy-parol"
        TRICKY_PASSWORD='parol "s kavychkoy" i \sleshem'

        # Значение дописывается в конец: файл настроек подключается оболочкой,
        # и последнее присваивание побеждает. Через sed этот пароль не провести —
        # обратный слэш в строке замены она истолкует по-своему и съест его,
        # и тест провалился бы по собственной вине.
        printf 'PASSWORD=%q\n' "$TRICKY_PASSWORD" >>"$CLIENT_HOME/etc/updatehub.conf"

        if start_fake_server env FAKE_PASSWORD="$TRICKY_PASSWORD"; then
            OUTPUT="$(run_client sync)"

            start_test "пароль с кавычкой и слэшем доходит до сервера неискажённым"
            assert_contains "$OUTPUT" "Обновление завершено"

            stop_fake_server
        fi

        start_fake_server || true

        # --- состояние без обращения к серверу ---
        prepare_client "sostoyanie"
        OUTPUT="$(run_client status)"

        start_test "состояние показывает адрес сервера"
        assert_contains "$OUTPUT" "127.0.0.1:$FAKE_PORT"

        start_test "состояние сообщает, что обновление не выполнялось"
        assert_contains "$OUTPUT" "не выполнялось"

        # --- начальная заливка с носителя ---
        prepare_client "zalivka"
        USB_DIR="$WORK_DIR/usb"
        mkdir -p "$USB_DIR/docs"
        printf 'hello' >"$USB_DIR/docs/privet.txt"
        printf 'soderzhimoe obraza' >"$USB_DIR/astra176.iso"

        OUTPUT="$(run_client seed "$USB_DIR")"

        start_test "заливка скопировала файлы"
        assert_file_content "$CLIENT_HOME/data/astra176.iso" "soderzhimoe obraza"

        start_test "после заливки сервер досылает только недостающее"
        OUTPUT="$(run_client check)"
        assert_contains "$OUTPUT" "Файлов к загрузке: 1"

        stop_fake_server

        # --- испорченная передача ---
        prepare_client "isporcheno"
        if start_fake_server env FAKE_BREAK_FILE="docs/tretiy.txt"; then
            OUTPUT="$(run_client sync)"

            start_test "несовпавшая сумма прерывает обновление"
            assert_contains "$OUTPUT" "не прошло проверку контрольных сумм"

            start_test "при несовпавшей сумме рабочая папка не тронута"
            assert_equals "0" "$(find "$CLIENT_HOME/data" -type f | wc -l | tr -d ' ')"

            start_test "временный каталог очищен и после неудачи"
            assert_equals "0" "$(find "$CLIENT_HOME/temp" -type f 2>/dev/null | wc -l | tr -d ' ')"

            stop_fake_server
        fi

        # --- протухший токен ---
        prepare_client "token"
        if start_fake_server env FAKE_EXPIRE_AFTER=1; then
            OUTPUT="$(run_client sync)"

            start_test "протухший токен обновляется, обновление доходит до конца"
            assert_contains "$OUTPUT" "Обновление завершено"

            start_test "файлы получены несмотря на смену токена"
            assert_equals "3" "$(find "$CLIENT_HOME/data" -type f | wc -l | tr -d ' ')"

            stop_fake_server
        fi

        # --- обрыв связи и докачка ---
        prepare_client "dokachka"
        if start_fake_server env FAKE_DROP_AFTER=5; then
            OUTPUT="$(run_client sync)"

            start_test "оборванная загрузка докачивается"
            assert_contains "$OUTPUT" "Обновление завершено"

            start_test "докачанный файл совпадает с исходным"
            assert_file_content "$CLIENT_HOME/data/astra176.iso" "soderzhimoe obraza"

            stop_fake_server
        fi

        # --- недоступный сервер ---
        prepare_client "net-svyazi"
        OUTPUT="$(run_client sync)"
        CODE=$?

        start_test "недоступный сервер даёт понятное сообщение"
        assert_contains "$OUTPUT" "Сервер недоступен"

        start_test "недоступный сервер даёт код возврата «временная неполадка»"
        assert_equals "75" "$CODE"
    fi
fi

# ---------- итог ----------

printf '\n'
printf 'Проверок выполнено: %s, провалов: %s\n' "$TESTS_RUN" "$TESTS_FAILED"

[ "$TESTS_FAILED" -eq 0 ]
