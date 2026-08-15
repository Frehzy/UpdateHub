# UpdateHub Server Makefile
# Использование:
#   make build      - Собрать Docker образ
#   make save        - Сохранить образ в tar-файл
#   make load        - Загрузить образ из tar-файла
#   make run         - Запустить контейнер
#   make stop        - Остановить контейнер
#   make clean       - Очистить временные файлы

APP_NAME = updatehub-server
IMAGE_NAME = $(APP_NAME):latest
TAR_FILE = $(APP_NAME).tar
CONTAINER_NAME = $(APP_NAME)
PORT = 8080
FILES_DIR = ./files
DATA_DIR = ./data

.PHONY: build save load run stop clean

# Сборка Docker образа
build:
	@echo "Building Docker image..."
	docker build -t $(IMAGE_NAME) .

# Сохранение образа в tar-файл
save: build
	@echo "Saving image to $(TAR_FILE)..."
	docker save -o $(TAR_FILE) $(IMAGE_NAME)
	@echo "Image saved. Size: $$(du -h $(TAR_FILE) | cut -f1)"

# Загрузка образа из tar-файла
load:
	@echo "Loading image from $(TAR_FILE)..."
	docker load -i $(TAR_FILE)
	@echo "Image loaded successfully"

# Запуск контейнера
run:
	@echo "Running container..."
	mkdir -p $(FILES_DIR) $(DATA_DIR)
	docker run -d \
		--name $(CONTAINER_NAME) \
		-p $(PORT):8080 \
		-v $$(pwd)/$(FILES_DIR):/app/files \
		-v $$(pwd)/$(DATA_DIR):/app/data \
		-e ASPNETCORE_ENVIRONMENT=Production \
		-e UpdateHub__FilesPath=/app/files \
		-e UpdateHub__DatabasePath=/app/data/updatehub.db \
		--restart unless-stopped \
		$(IMAGE_NAME)
	@echo "Container started on port $(PORT)"
	@echo "Health check: http://localhost:$(PORT)/health"

# Остановка контейнера
stop:
	@echo "Stopping container..."
	docker stop $(CONTAINER_NAME) || true
	docker rm $(CONTAINER_NAME) || true
	@echo "Container stopped"

# Очистка
clean: stop
	@echo "Cleaning up..."
	rm -f $(TAR_FILE)
	docker rmi $(IMAGE_NAME) || true
	@echo "Cleanup complete"

# Полный цикл: сборка, сохранение, перенос
package: clean build save
	@echo "Package created: $(TAR_FILE)"
	@echo "To deploy on another machine:"
	@echo "  1. Copy $(TAR_FILE) to target machine"
	@echo "  2. Run 'make load' on target machine"
	@echo "  3. Run 'make run' on target machine"

# Информация о командах
help:
	@echo "Available commands:"
	@echo "  make build    - Build Docker image"
	@echo "  make save     - Save image to tar file"
	@echo "  make load     - Load image from tar file"
	@echo "  make run      - Run container"
	@echo "  make stop     - Stop container"
	@echo "  make clean    - Clean up"
	@echo "  make package  - Full build + save"
	@echo "  make help     - Show this help"