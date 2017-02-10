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

DEIS2_PERMS_CREATE_USERS = 

-include $(APPLICATION_PATH)/$(CI_STARTER_KIT_DIR)/Makefile.ci
