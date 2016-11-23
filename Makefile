APPLICATION ?= $$(basename "$(CURDIR)")
DOCKER_FILE ?= Dockerfile
EDITOR ?= vim
SCALE ?= web=1 cmd=0 worker=3

REGISTRY ?= docker-hub.2gis.ru
VENDOR ?= ams
IMAGE ?= $(VENDOR)/$(APPLICATION)
TAG ?= latest

DEIS_ENV_FILE ?= .env
DEIS_PROFILE ?= client
DEIS_CLIENT_CONFIG ?= client.json
DEIS_APPLICATION ?= $(APPLICATION)


.PHONY: help
help:
	@echo "Specify target explicitly"


#
# Docker commands
#

.PHONY: docker-build
docker-build:
	docker build --pull --rm --tag "$(IMAGE):$(TAG)" -f "$(DOCKER_FILE)" .

.PHONY: docker-clean-containers
docker-clean-containers:
	docker rm --force $$(docker ps --all --filter name="$(APPLICATION)" --quiet)

.PHONY: docker-clean-images
docker-clean-images:
	docker rmi --force $$(docker images --quiet "$(REGISTRY)/$(IMAGE):$(TAG)")

.PHONY: docker-push
docker-push:
	docker tag "$(IMAGE):$(TAG)" "$(REGISTRY)/$(IMAGE):$(TAG)"
	docker push "$(REGISTRY)/$(IMAGE):$(TAG)"


#
# Deis commands
#

.PHONY: deis-pull
deis-pull:
	DEIS_PROFILE=$(DEIS_PROFILE) deis config:push -a $(DEIS_APPLICATION) -p $(DEIS_ENV_FILE) | sed 's/\(.*password.*\)\([\=[:space:]]\+\)\(.*\)/\1\2**********/i'
	DEIS_PROFILE=$(DEIS_PROFILE) deis pull "$(REGISTRY)/$(IMAGE):$(TAG)" -a $(DEIS_APPLICATION)
	DEIS_PROFILE=$(DEIS_PROFILE) deis scale $(SCALE) -a $(DEIS_APPLICATION)
	DEIS_PROFILE=$(DEIS_PROFILE) deis info -a $(DEIS_APPLICATION)

.PHONY: deis-config
deis-config:
	@mkdir -p ~/.deis/
	@echo "{ \"username\": \"$(DEIS_USERNAME)\", \"ssl_verify\": false, \"controller\":\"$(DEIS_CONTROLLER)\", \"token\": \"$(DEIS_TOKEN)\" }" > ~/.deis/$(DEIS_CLIENT_CONFIG)


#
# Encryption commands
#

.PHONY: passphrase
passphrase:
	$(eval PASSPHRASE ?= $(shell bash -c '[ -z "$$PASSPHRASE" ] && read -s -p "Password: " pwd && echo $$pwd'))

.PHONY: check-gpg-input
check-gpg-input:
ifndef INPUT
	$(error INPUT is undefined)
endif
ifndef OUTPUT
	$(error OUTPUT is undefined)
endif

.PHONY: gpg-decrypt
gpg-decrypt: passphrase check-gpg-input
	@echo $(PASSPHRASE) | gpg --output $(OUTPUT) --batch --no-tty --yes --passphrase-fd 0 --decrypt $(INPUT)
	@echo ""

.PHONY: gpg-encrypt
gpg-encrypt: passphrase check-gpg-input
	@echo $(PASSPHRASE) | gpg --output $(OUTPUT) --batch --no-tty --yes --passphrase-fd 0 --symmetric $(INPUT)
	@echo ""

.PHONY: gpg-edit
gpg-edit:
ifndef TARGET
	$(error TARGET is undefined)
endif
	$(eval TMPFILE = $(shell mktemp))
	INPUT=$(TARGET) OUTPUT=$(TMPFILE) $(MAKE) gpg-decrypt && \
	$(EDITOR) $(TMPFILE) && \
	INPUT=$(TMPFILE) OUTPUT=$(TARGET) $(MAKE) gpg-encrypt
	rm -f $(TMPFILE)

