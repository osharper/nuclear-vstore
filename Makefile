# Имя приложения, например bss-buffer
APPLICATION = vstore
# Группа, в которую входит приложение, обычно название команды, например, webapi
VENDOR = ams
# Путь к корню проекта относительно главного Makefile-а, в котором он определяется
# abspath обязателен
APPLICATION_PATH ?= $(abspath .)
# Текущий путь - если пустой, то это путь из APPLICATION_PATH
WORKDIR =
BUILD_PATH ?= bin

CI_STARTER_KIT_DIR ?= ci-starter-kit

SHELL := env PATH=$(PATH) /bin/bash

DOCKER_RUN_PORT = 5000

HEALTHCHECK_PATH = /swagger/1.0/swagger.json
DEIS2_HEALTHCHECK_PATH = $(HEALTHCHECK_PATH)
DEIS2_HEALTHCHECK_PORT = $(DOCKER_RUN_PORT)
DEIS2_PERMS_CREATE_USERS =

INSTALL_ARGS ?=
DOCKER_INSTALL_ARGS ?= $(INSTALL_ARGS)

-include $(APPLICATION_PATH)/$(CI_STARTER_KIT_DIR)/Makefile.ci
