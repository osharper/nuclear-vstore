APPLICATION = ci-starter-kit
VENDOR = cd
APPLICATION_PATH = $(abspath ..)
WORKDIR = test
BUILD_PATH = bin
CI_STARTER_KIT_DIR =

SHELL := env PATH=$(PATH) /bin/bash

DEIS2_MEMORY_LIMITS = cmd=25M/50M worker=25M/50M
DEIS2_CPU_LIMITS = cmd=10m/20m worker=10m/20m
DEIS2_SCALE = cmd=1 worker=1

DOCKER_RUN_PORT = 5000
DOCKER_RUN_APP_PATH = /go/src/gitlab.2gis.ru/$(VENDOR)/$(APPLICATION)
DOCKER_RUN_VOLUMES := -v /cache/$(APPLICATION):/cache

DEIS2_HEALTHCHECK_PATH = /healthcheck
DEIS2_HEALTHCHECK_PORT = $(DOCKER_RUN_PORT)

DEIS2_PERMS_CREATE_USERS = k.sidenko p.fomin

-include $(APPLICATION_PATH)/$(CI_STARTER_KIT_DIR)/Makefile.ci
-include $(APPLICATION_PATH)/Makefile.docker-application-template

.PHONY: install
install:
	go install .

.PHONY: test
test:
	go test -v .

.PHONY: lint
lint:
	$(info Run '$@' command)
	@exit 0

.PHONY: bench
bench:
	$(info Run '$@' command)
	@exit 0

.PHONY: build
build:
	CGO_ENABLED=0 GOOS=linux go build -a -installsuffix cgo -o $(APPLICATION_PATH)/$(WORKDIR)/$(BUILD_PATH)/$(APPLICATION) .

.PHONY: run
run:
	$(APPLICATION_PATH)/$(WORKDIR)/$(BUILD_PATH)/$(APPLICATION)
