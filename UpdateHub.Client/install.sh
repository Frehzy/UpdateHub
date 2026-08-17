#!/usr/bin/env bash
#
# Установка клиента UpdateHub.
#
# Запускается с носителя, на который скопирован каталог UpdateHub.Client.
# Настройки, если они уже есть, не перезаписываются: при обновлении клиента
# заново вводить адрес сервера и пароль незачем.

set -euo pipefail

INSTALL_DIR="${INSTALL_DIR:-/opt/updatehub}"
CONFIG_DIR="${CONFIG_DIR:-/etc/updatehub}"
BIN_LINK="${BIN_LINK:-/usr/local/bin/updatehub}"
SYSTEMD_DIR="${SYSTEMD_DIR:-/etc/systemd/system}"
LOGROTATE_DIR="${LOGROTATE_DIR:-/etc/logrotate.d}"

SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$(id -u)" -ne 0 ]; then
    printf 'Установка требует прав root. Запустите через sudo.\n' >&2
    exit 1
fi

printf 'Установка UpdateHub в %s\n' "$INSTALL_DIR"

install -d -m 755 "$INSTALL_DIR" "$INSTALL_DIR/lib" "$CONFIG_DIR"

install -m 755 "$SOURCE_DIR/updatehub" "$INSTALL_DIR/updatehub"
install -m 644 "$SOURCE_DIR"/lib/*.sh "$INSTALL_DIR/lib/"
install -m 644 "$SOURCE_DIR/README.md" "$INSTALL_DIR/README.md" 2>/dev/null || true

ln -sf "$INSTALL_DIR/updatehub" "$BIN_LINK"

# Права 600 на файл настроек: в нём может лежать пароль.
if [ -f "$CONFIG_DIR/updatehub.conf" ]; then
    printf 'Файл настроек уже существует, оставлен без изменений: %s\n' "$CONFIG_DIR/updatehub.conf"
    install -m 644 "$SOURCE_DIR/etc/updatehub.conf.example" "$CONFIG_DIR/updatehub.conf.example"
else
    install -m 600 "$SOURCE_DIR/etc/updatehub.conf.example" "$CONFIG_DIR/updatehub.conf"
    printf 'Создан файл настроек: %s — заполните SERVER_URL\n' "$CONFIG_DIR/updatehub.conf"
fi

install -d -m 755 /var/lib/updatehub

# Ротация журнала. Без неё файл растёт без предела: клиент дописывает в него
# при каждом запуске, а запусков по одному в сутки годами. На машине с малым
# диском это однажды кончится нехваткой места — на скачивании обновления.
if [ -d "$LOGROTATE_DIR" ]; then
    install -m 644 "$SOURCE_DIR/etc/updatehub.logrotate" "$LOGROTATE_DIR/updatehub"
    printf 'Установлено правило ротации журнала: %s/updatehub\n' "$LOGROTATE_DIR"
else
    printf 'Каталог %s отсутствует, ротация журнала не настроена\n' "$LOGROTATE_DIR" >&2
fi

if [ -d "$SYSTEMD_DIR" ] && command -v systemctl >/dev/null 2>&1; then
    install -m 644 "$SOURCE_DIR/systemd/updatehub.service" "$SYSTEMD_DIR/updatehub.service"
    install -m 644 "$SOURCE_DIR/systemd/updatehub.timer" "$SYSTEMD_DIR/updatehub.timer"
    systemctl daemon-reload

    printf '\nЕжедневное обновление включается командой:\n'
    printf '  systemctl enable --now updatehub.timer\n'
fi

printf '\nУстановлено. Дальше:\n'
printf '  1. Заполните %s/updatehub.conf\n' "$CONFIG_DIR"
printf '  2. updatehub selftest\n'
printf '  3. updatehub enroll\n'
printf '  4. После одобрения администратором: updatehub sync\n'
