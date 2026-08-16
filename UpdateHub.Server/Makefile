# UpdateHub Server
#
# Порядок работы: образ собирается на машине с интернетом, сохраняется в tar,
# переносится в закрытый контур и разворачивается там в Docker Desktop.
#
#   make package  - собрать образ и сохранить в tar (на машине с интернетом)
#   make load     - загрузить образ из tar (на целевой машине)
#   make run      - запустить контейнер
#   make logs     - показать журнал
#   make stop     - остановить и удалить контейнер
#   make clean    - удалить контейнер, образ и tar-файл

APP_NAME       = updatehub-server
IMAGE_NAME     = $(APP_NAME):latest
TAR_FILE       = $(APP_NAME).tar
CONTAINER_NAME = $(APP_NAME)
PORT           = 8080

# Каталог с раздаваемыми файлами. На Windows укажите путь в формате
# /c/Users/имя/updatehub/files либо задайте переменную при запуске:
#   make run FILES_DIR=/c/updatehub/files
FILES_DIR = $(CURDIR)/files

# База данных живёт в именованном томе Docker, а НЕ в проброшенной папке Windows:
# блокировки файлов через 9p/virtiofs работают неправильно и портят базу SQLite.
DATA_VOLUME = updatehub-data

# Ключ подписи токенов. Сгенерировать: openssl rand -base64 48
JWT_SECRET ?=

.PHONY: build save load run stop restart logs clean package help

build:
	@echo "Сборка образа..."
	docker build -t $(IMAGE_NAME) .

save: build
	@echo "Сохранение образа в $(TAR_FILE)..."
	docker save -o $(TAR_FILE) $(IMAGE_NAME)
	@echo "Готово. Размер: $$(du -h $(TAR_FILE) | cut -f1)"

load:
	@echo "Загрузка образа из $(TAR_FILE)..."
	docker load -i $(TAR_FILE)

run:
	@if [ -z "$(JWT_SECRET)" ]; then \
		echo "ОШИБКА: не задан JWT_SECRET."; \
		echo "Сгенерируйте ключ и передайте его:"; \
		echo "  make run JWT_SECRET=\"$$(openssl rand -base64 48)\""; \
		exit 1; \
	fi
	@mkdir -p "$(FILES_DIR)"
	docker volume create $(DATA_VOLUME) >/dev/null
	docker run -d \
		--name $(CONTAINER_NAME) \
		--user 1000:1000 \
		-p $(PORT):8080 \
		-v "$(FILES_DIR)":/app/files:ro \
		-v $(DATA_VOLUME):/app/data \
		-e ASPNETCORE_ENVIRONMENT=Production \
		-e Jwt__SecretKey="$(JWT_SECRET)" \
		-e UpdateHub__FilesPath=/app/files \
		-e UpdateHub__DatabasePath=/app/data/updatehub.db \
		--restart unless-stopped \
		$(IMAGE_NAME)
	@echo "Контейнер запущен на порту $(PORT)."
	@echo "Пароль администратора при первом запуске: make logs"

logs:
	docker logs -f $(CONTAINER_NAME)

stop:
	-docker stop $(CONTAINER_NAME)
	-docker rm $(CONTAINER_NAME)

restart: stop run

clean: stop
	-docker rmi $(IMAGE_NAME)
	rm -f $(TAR_FILE)
	@echo "Том $(DATA_VOLUME) с базой данных сохранён."
	@echo "Чтобы удалить и его: docker volume rm $(DATA_VOLUME)"

package: build save
	@echo ""
	@echo "Образ упакован в $(TAR_FILE). Перенос в закрытый контур:"
	@echo "  1. Скопируйте $(TAR_FILE) и Makefile на целевую машину"
	@echo "  2. make load"
	@echo "  3. make run JWT_SECRET=\"<ключ>\" FILES_DIR=<путь к папке с файлами>"

help:
	@grep -E '^#   ' Makefile | sed 's/^#   //'
