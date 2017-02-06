APPLICATION ?= $$(basename "$(CURDIR)")
DOCKER_FILE ?= Dockerfile
DOCKER_BUILD_CONTEXT ?= .
DOCKER_BUILD_ARG ?=
EDITOR ?= vim

REGISTRY ?= docker-hub.2gis.ru
VENDOR ?= ams
IMAGE ?= $(VENDOR)/$(APPLICATION)
TAG ?= latest

export DEIS_PROFILE

DEIS_CONTROLLER ?= http://deis.web.2gis.local
DEIS_SCALE ?= cmd=1
DEIS_ENV_FILE ?= .env
DEIS_PROFILE ?= client
DEIS_CLIENT_CONFIG ?= $(DEIS_PROFILE).json
DEIS_APPLICATION ?= $(APPLICATION)
DEIS_MEMORY_LIMITS ?= Unlimited

DEIS ?= deis2

# В deis есть ограничение на название приложения, поэтому пофильтруем его
DEIS_APPLICATION_FILTER = $(shell echo $(DEIS_APPLICATION) | tr '[:upper:]' '[:lower:]' | sed 's/_/-/g' | sed -e 's/[^a-z0-9-]//g' )

TAG_FILTER = $(shell echo $(TAG) | tr '[:upper:]' '[:lower:]' | sed -e 's/\//-/g' )


.PHONY: help
help:
	@echo "Specify target explicitly"


#
# Docker commands
#

.PHONY: docker-build
docker-build:
	docker build --pull --rm $(DOCKER_BUILD_ARG) --tag "$(REGISTRY)/$(IMAGE):$(TAG_FILTER)" -f "$(DOCKER_FILE)" $(DOCKER_BUILD_CONTEXT)

.PHONY: docker-clean-containers
docker-clean-containers:
	docker rm --force $$(docker ps --all --filter name="$(APPLICATION)" --quiet)

.PHONY: docker-clean-images
docker-clean-images:
	docker rmi --force $$(docker images --quiet "$(REGISTRY)/$(IMAGE):$(TAG_FILTER)")

.PHONY: docker-push
docker-push:
	docker push "$(REGISTRY)/$(IMAGE):$(TAG_FILTER)"


#
# Deis commands
#

.PHONY: deis-create
deis-create:
	@echo "Check application for create..."
	@IS_APP_EXIST=`$(DEIS) apps:list | grep '^$(DEIS_APPLICATION_FILTER)$$'`; \
	if [ -z "$${IS_APP_EXIST}" ]; then \
		echo "Create app $(DEIS_APPLICATION_FILTER)..." ; \
		$(DEIS) create $(DEIS_APPLICATION_FILTER) --no-remote || exit 1; \
	else \
		echo "Application $(DEIS_APPLICATION_FILTER) exist!" ; \
	fi;

.PHONY: deis-pull
deis-pull:
	$(DEIS) pull "$(REGISTRY)/$(IMAGE):$(TAG_FILTER)" -a $(DEIS_APPLICATION_FILTER)

.PHONY: deis-info
deis-info:
	$(DEIS) info -a $(DEIS_APPLICATION_FILTER)

.PHONY: deis-apps-check
deis-apps-check:
	@echo "Check application '$(DEIS_APPLICATION_FILTER)' ..."
	@APP_INFO=`$(DEIS) apps:info -a $(DEIS_APPLICATION_FILTER)`; \
	IS_APP_DESTROYED=`echo $$APP_INFO | grep ' destroyed '`; \
	if [ ! -z "$${IS_APP_DESTROYED}" ]; then \
		echo "Application '$(DEIS_APPLICATION_FILTER)' is destroyed!" ; \
		exit 1; \
	fi; \
	IS_APP_DOWN=`echo $$APP_INFO | grep ' down '`; \
	if [ ! -z "$${IS_APP_DOWN}" ]; then \
		echo "Application '$(DEIS_APPLICATION_FILTER)' is down!" ; \
		exit 1; \
	fi; \
	IS_APP_ALIVE=`echo $$APP_INFO | grep ' up '`;\
	if [ -z "$${IS_APP_ALIVE}" ]; then \
		echo "Application '$(DEIS_APPLICATION_FILTER)' is not running!" ; \
		exit 1; \
	fi; \
	echo "ok"

.PHONY: deis-config-create
deis-config-create:
	@mkdir -p ~/.deis/
	@echo "{ \"username\": \"$(DEIS_USERNAME)\", \"ssl_verify\": false, \"controller\":\"$(DEIS_CONTROLLER)\", \"token\": \"$(DEIS_TOKEN)\" }" > ~/.deis/$(DEIS_CLIENT_CONFIG)

.PHONY: deis-config-push
deis-config-push:
	@echo "Check config..."
	@NEW_CONFIG=`cat $(DEIS_ENV_FILE) | sort | md5sum -` ; \
	OLD_CONFIG=`cd /tmp && $(DEIS) config:pull -o -a $(DEIS_APPLICATION_FILTER) && cat .env | sort | md5sum - && rm -f .env` ; \
	if [ "$${NEW_CONFIG}" != "$${OLD_CONFIG}" ]; then \
		echo "Push config..." ; \
		$(DEIS) config:push -a $(DEIS_APPLICATION_FILTER) -p $(DEIS_ENV_FILE) > /dev/null || exit 1; \
	else \
		echo "Config not changed!" ; \
	fi;

.PHONY: deis-memory-limits-set
deis-memory-limits-set:
	@echo "Check limits..."
	@DEIS_MEMORY_LIMITS_FORMATTED=`echo $(DEIS_MEMORY_LIMITS) | tr '=' ' '`;\
	OLD_LIMITS=`$(DEIS) limits:list -a $(DEIS_APPLICATION_FILTER) | sed '1,/Memory/d' | sed '/CPU/,/-End/d' | sed '/./!d' | tr '\n' ' ' | tr -s ' '`;\
	IS_NEW=`echo "$${OLD_LIMITS}" | grep "$${DEIS_MEMORY_LIMITS_FORMATTED}"`; \
	if [ -z "$${IS_NEW}" ]; then \
		echo "Push memory limits..."; \
		echo "Old limits are $${OLD_LIMITS}"; \
		echo "New limit will contain $${DEIS_MEMORY_LIMITS_FORMATTED}"; \
		$(DEIS) limits:set $(DEIS_MEMORY_LIMITS) --memory -a $(DEIS_APPLICATION_FILTER) || exit 1; \
		echo "deis limits '$(DEIS_MEMORY_LIMITS)' changed!" ; \
	else \
		echo "deis limits '$(DEIS_MEMORY_LIMITS)' not changed!" ; \
	fi;

.PHONY: deis-scale
deis-scale:
	@echo "Check scale..."
	@NEW_CONFIG=`echo $(DEIS_SCALE) | tr " " "\n" | sort | md5sum -` ; \
	OLD_CONFIG=`$(DEIS) ps:list -a $(DEIS_APPLICATION_FILTER) | grep -e ' up ' | awk -F '.' '{ print $$1 }' | sort | uniq -c | awk '{print $$2"="$$1}' | sort | md5sum -` ; \
	if [ "$${NEW_CONFIG}" != "$${OLD_CONFIG}" ]; then \
		echo "Change scale..." ; \
		$(DEIS) scale $(DEIS_SCALE) -a $(DEIS_APPLICATION_FILTER) || exit 1; \
	else \
		echo "Scale not changed!" ; \
	fi;

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
