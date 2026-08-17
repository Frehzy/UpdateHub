#!/usr/bin/env bash
# Опознание машины: постоянный идентификатор и отпечаток железа.

# Возвращает идентификатор компьютера, создавая его при первом обращении.
#
# Идентификатор постоянный: по нему привязаны выданные права и вся история
# обращений. Переустановка системы его теряет — на этот случай существует
# отпечаток железа, по которому администратор узнаёт ту же машину.
read_client_id() {
    if [ -f "$CLIENT_ID_FILE" ]; then
        local existing
        existing="$(tr -d ' \t\n\r' <"$CLIENT_ID_FILE")"

        if [ -n "$existing" ]; then
            printf '%s\n' "$existing"
            return 0
        fi

        log_warn "Файл идентификатора пуст, создаётся новый: $CLIENT_ID_FILE"
    fi

    local generated
    generated="$(generate_uuid)"

    ensure_dir "$(dirname "$CLIENT_ID_FILE")"
    printf '%s\n' "$generated" >"$CLIENT_ID_FILE" \
        || die "Не удалось записать идентификатор в $CLIENT_ID_FILE"
    chmod 644 "$CLIENT_ID_FILE" 2>/dev/null || true

    log_info "Создан идентификатор компьютера: $generated"
    printf '%s\n' "$generated"
}

# Создаёт UUID.
#
# Ядро выдаёт его само через /proc — это избавляет от зависимости от uuidgen,
# которого в урезанной установке может не оказаться. Запасной путь на случай,
# если /proc недоступен, собирает значение из случайных байт.
generate_uuid() {
    if [ -r /proc/sys/kernel/random/uuid ]; then
        cat /proc/sys/kernel/random/uuid
        return 0
    fi

    od -An -tx1 -N16 /dev/urandom 2>/dev/null | awk '{
        gsub(/ /, "")
        printf "%s-%s-%s-%s-%s\n", substr($0,1,8), substr($0,9,4), substr($0,13,4), substr($0,17,4), substr($0,21,12)
    }'
}

# Возвращает отпечаток железа.
#
# Собирается из тех сведений, которые переживают переустановку системы:
# серийные номера материнской платы и корпуса. Часть из них читается только
# от root, поэтому используется всё, что удалось прочитать, а при полной
# неудаче — идентификатор установки системы. Значение хэшируется: серийные
# номера сами по себе передавать незачем.
read_hardware_fingerprint() {
    local parts=""

    local source
    for source in \
        /sys/class/dmi/id/product_uuid \
        /sys/class/dmi/id/product_serial \
        /sys/class/dmi/id/board_serial \
        /sys/class/dmi/id/chassis_serial
    do
        if [ -r "$source" ]; then
            local value
            value="$(tr -d ' \t\n\r' <"$source" 2>/dev/null || true)"

            # Производители нередко оставляют в этих полях заглушки, и одинаковая
            # для всех строка «To be filled by O.E.M.» опознанием не является.
            case "$value" in
                "" | 0 | [Nn]one | *[Tt]o*[Bb]e*[Ff]illed* | *[Dd]efault*string* | *[Nn]ot*[Ss]pecified*) continue ;;
            esac

            parts="$parts$value|"
        fi
    done

    if [ -z "$parts" ] && [ -r /etc/machine-id ]; then
        parts="$(cat /etc/machine-id)"
    fi

    [ -n "$parts" ] || return 0

    printf '%s' "$parts" | md5sum | awk '{print $1}'
}

# Собирает сведения о машине для передачи серверу.
#
# Печатает строки вида «ключ=значение». Пустые сведения пропускаются:
# передавать пустое поле незачем, а разбирать его на сервере — тем более.
collect_machine_facts() {
    local hostname_value os_version kernel_version architecture cpu_info memory_gb disk_gb mac_address

    hostname_value="$(hostname 2>/dev/null || cat /etc/hostname 2>/dev/null || true)"
    kernel_version="$(uname -r 2>/dev/null || true)"
    architecture="$(uname -m 2>/dev/null || true)"

    if [ -r /etc/os-release ]; then
        os_version="$(awk -F'"' '/^PRETTY_NAME=/ { print $2 }' /etc/os-release)"
    fi

    if [ -r /proc/cpuinfo ]; then
        cpu_info="$(awk -F': ' '/^model name/ { print $2; exit }' /proc/cpuinfo)"
    fi

    if [ -r /proc/meminfo ]; then
        # MemTotal приходит в килобайтах; округление вверх, чтобы 1,9 ГБ
        # не превращались в 1 и не выглядели как машина вдвое слабее.
        memory_gb="$(awk '/^MemTotal:/ { printf "%d\n", ($2 + 1048575) / 1048576 }' /proc/meminfo)"
    fi

    disk_gb="$(df -Pk "$DATA_DIR" 2>/dev/null | awk 'NR == 2 { printf "%d\n", $2 / 1048576 }')"
    mac_address="$(read_primary_mac)"

    print_fact "hostname" "$hostname_value"
    print_fact "os_version" "$os_version"
    print_fact "kernel_version" "$kernel_version"
    print_fact "architecture" "$architecture"
    print_fact "cpu_info" "$cpu_info"
    print_fact "memory_gb" "$memory_gb"
    print_fact "disk_gb" "$disk_gb"
    print_fact "mac_address" "$mac_address"
}

# Печатает одно сведение, если оно непустое.
print_fact() {
    local key="$1" value="${2:-}"
    [ -n "$value" ] || return 0
    printf '%s=%s\n' "$key" "$value"
}

# Возвращает MAC-адрес того интерфейса, через который машина ходит в сеть.
#
# Перебор всех интерфейсов дал бы и виртуальные адреса контейнеров, поэтому
# берётся интерфейс маршрута по умолчанию.
read_primary_mac() {
    local interface=""

    if [ -r /proc/net/route ]; then
        interface="$(awk '$2 == "00000000" { print $1; exit }' /proc/net/route)"
    fi

    if [ -n "$interface" ] && [ -r "/sys/class/net/$interface/address" ]; then
        cat "/sys/class/net/$interface/address"
        return 0
    fi

    local device
    for device in /sys/class/net/*; do
        case "$(basename "$device")" in
            lo | docker* | veth* | br-*) continue ;;
        esac

        if [ -r "$device/address" ]; then
            cat "$device/address"
            return 0
        fi
    done
}
