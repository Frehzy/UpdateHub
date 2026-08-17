#!/usr/bin/env python3
"""Подставной сервер обновлений для тестов клиента.

Отвечает тем же, чем настоящий: текстом в формате md5sum и строками
«ключ=значение». Нужен, чтобы проверить весь ход обновления — вход,
сравнение манифестов, загрузку с докачкой, проверку сумм и перенос
файлов, — не поднимая настоящий сервер и не имея .NET.

Проверяется здесь именно клиент. Соответствие самого протокола проверяют
тесты сервера на C#: там поднимается настоящее приложение.

Управление поведением идёт через переменные окружения:
    FAKE_FILES_DIR      каталог, который сервер раздаёт
    FAKE_PORT           порт
    FAKE_PASSWORD       правильный пароль
    FAKE_EXPIRE_AFTER   через сколько запросов access-токен «протухает»
    FAKE_BREAK_FILE     путь, для которого сервер отдаёт испорченное содержимое
    FAKE_DROP_AFTER     сколько байт отдать, прежде чем оборвать выдачу файла
"""

import hashlib
import os
import re
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

FILES_DIR = os.environ.get("FAKE_FILES_DIR", "/tmp/fake-files")
PASSWORD = os.environ.get("FAKE_PASSWORD", "parol12345")
EXPIRE_AFTER = int(os.environ.get("FAKE_EXPIRE_AFTER", "0"))
BREAK_FILE = os.environ.get("FAKE_BREAK_FILE", "")
DROP_AFTER = int(os.environ.get("FAKE_DROP_AFTER", "0"))

STATE = {"access": "access-1", "refresh": "refresh-1", "requests": 0, "issued": 1}
LOCK = threading.Lock()


def file_md5(path):
    digest = hashlib.md5()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def server_manifest():
    """Собирает манифест раздаваемого каталога: путь -> (сумма, размер)."""
    entries = {}
    for root, _, names in os.walk(FILES_DIR):
        for name in names:
            full = os.path.join(root, name)
            relative = os.path.relpath(full, FILES_DIR)
            entries[relative] = (file_md5(full), os.path.getsize(full))
    return entries


def parse_manifest(text):
    """Разбирает манифест клиента в формате md5sum."""
    entries = {}
    for line in text.split("\n"):
        match = re.match(r"^([0-9a-f]{32})  (.+)$", line)
        if match:
            entries[match.group(2)] = match.group(1)
    return entries


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, *args):
        """Журнал подавлен: он мешает читать вывод тестов."""

    # ---------- служебное ----------

    def send_text(self, code, body):
        payload = body.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "text/plain; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def read_form(self):
        length = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(length).decode("utf-8") if length else ""
        return {key: values[0] for key, values in parse_qs(raw, keep_blank_values=True).items()}

    def read_body(self):
        length = int(self.headers.get("Content-Length", "0"))
        return self.rfile.read(length).decode("utf-8") if length else ""

    def token_is_valid(self):
        header = self.headers.get("Authorization", "")
        if not header.startswith("Bearer "):
            return False

        token = header[7:]
        with LOCK:
            if token != STATE["access"]:
                return False

            # Изображаем протухание: через заданное число запросов сервер
            # перестаёт принимать выданный токен, и клиент обязан обновить его.
            if EXPIRE_AFTER:
                STATE["requests"] += 1
                if STATE["requests"] > EXPIRE_AFTER:
                    return False

        return True

    def reject_unauthorized(self):
        self.send_text(401, "error=Требуется действующий access-токен\n")

    # ---------- обработка ----------

    def do_GET(self):
        parsed = urlparse(self.path)

        if parsed.path == "/health":
            self.send_text(200, "Healthy")
            return

        if parsed.path == "/api/v1/files":
            self.serve_file(parse_qs(parsed.query))
            return

        self.send_text(404, "error=Адрес не найден\n")

    def do_POST(self):
        parsed = urlparse(self.path)

        if parsed.path == "/api/v1/auth/login":
            self.handle_login()
        elif parsed.path == "/api/v1/auth/refresh":
            self.handle_refresh()
        elif parsed.path == "/api/v1/auth/logout":
            self.send_response(204)
            self.send_header("Content-Length", "0")
            self.end_headers()
        elif parsed.path == "/api/v1/enroll":
            self.handle_enroll()
        elif parsed.path == "/api/v1/sync/diff":
            self.handle_diff()
        else:
            self.send_text(404, "error=Адрес не найден\n")

    def handle_login(self):
        form = self.read_form()

        if form.get("password") != PASSWORD:
            self.send_text(401, "error=Неверный логин или пароль\n")
            return

        with LOCK:
            STATE["requests"] = 0

        self.send_text(200, (
            f"access_token={STATE['access']}\n"
            f"refresh_token={STATE['refresh']}\n"
            "expires_in=3600\nuser_id=u-1\n"
            f"username={form.get('username', '')}\n"
            "role=Client\n"
            f"client_id={form.get('client_id', '')}\n"
            "must_change_password=0\n"
        ))

    def handle_refresh(self):
        form = self.read_form()

        if form.get("refresh_token") != STATE["refresh"]:
            self.send_text(401, "error=Токен не найден\n")
            return

        with LOCK:
            STATE["issued"] += 1
            STATE["access"] = f"access-{STATE['issued']}"
            STATE["requests"] = 0

        self.send_text(200, (
            f"access_token={STATE['access']}\n"
            f"refresh_token={STATE['refresh']}\n"
            "expires_in=3600\nuser_id=u-1\nusername=ivanov\nrole=Client\n"
        ))

    def handle_enroll(self):
        form = self.read_form()
        self.send_text(200, (
            "status=ok\nrequest_id=zayavka-1\nstate=Pending\n"
            f"message=Заявка на {form.get('client_id', '')} передана администратору\n"
        ))

    def handle_diff(self):
        if not self.token_is_valid():
            self.reject_unauthorized()
            return

        client = parse_manifest(self.read_body())
        server = server_manifest()

        to_download = {path: value for path, value in server.items()
                       if client.get(path) != value[0]}
        extra = [path for path in client if path not in server]

        total_size = sum(value[1] for value in to_download.values())
        status = "update" if to_download else "ok"

        lines = [
            "@GENERATION 7",
            f"@STATUS {status}",
            f"@COUNT {len(to_download)}",
            f"@SIZE {total_size}",
        ]
        lines += [f"!{path}" for path in sorted(extra)]
        lines += [f"{value[0]}  {path}" for path, value in sorted(to_download.items())]

        self.send_text(200, "\n".join(lines) + "\n")

    def serve_file(self, query):
        if not self.token_is_valid():
            self.reject_unauthorized()
            return

        relative = query.get("path", [""])[0]
        full = os.path.join(FILES_DIR, relative)

        if not relative or ".." in relative or not os.path.isfile(full):
            self.send_text(404, f"error=Файл '{relative}' не найден\n")
            return

        with open(full, "rb") as handle:
            content = handle.read()

        # Изображаем испорченную передачу: сумма не совпадёт, и клиент обязан
        # это заметить, а не разложить мусор по рабочей папке.
        if BREAK_FILE and relative == BREAK_FILE:
            content = content + b"-isporcheno"

        etag = f'"{hashlib.md5(content).hexdigest()}"'
        start = 0
        total = len(content)

        range_header = self.headers.get("Range", "")
        match = re.match(r"bytes=(\d+)-", range_header)
        if match:
            start = int(match.group(1))

            # Так настоящий сервер отвечает на докачку уже полного файла.
            if start >= total:
                self.send_response(416)
                self.send_header("Content-Range", f"bytes */{total}")
                self.send_header("Content-Length", "0")
                self.end_headers()
                return

        piece = content[start:]

        # Изображаем обрыв связи: отдаём часть и закрываем соединение.
        # Клиент обязан докачать остаток, а не начать сначала.
        if DROP_AFTER and start == 0 and len(piece) > DROP_AFTER:
            piece = piece[:DROP_AFTER]
            self.send_response(200)
            self.send_header("Content-Type", "application/octet-stream")
            self.send_header("Content-Length", str(total))
            self.send_header("Accept-Ranges", "bytes")
            self.send_header("ETag", etag)
            self.end_headers()
            self.wfile.write(piece)
            self.close_connection = True
            return

        if match:
            self.send_response(206)
            self.send_header("Content-Range", f"bytes {start}-{total - 1}/{total}")
        else:
            self.send_response(200)

        self.send_header("Content-Type", "application/octet-stream")
        self.send_header("Content-Length", str(len(piece)))
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("ETag", etag)
        self.end_headers()
        self.wfile.write(piece)


def main():
    port = int(os.environ.get("FAKE_PORT", "8099"))
    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    sys.stderr.write(f"подставной сервер на порту {port}, каталог {FILES_DIR}\n")
    sys.stderr.flush()
    server.serve_forever()


if __name__ == "__main__":
    main()
