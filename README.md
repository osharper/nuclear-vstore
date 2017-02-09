# CI starter kit

CI starter kit - это проект для быстрой настройки continuous integration (CI) / continuous delivery (CD) в любом проекте.
Особенности реализации:
- pipeline основан на gitlab-ci и целиком использует shared ресурсы gitlab-а для выполнения CI job
- в качестве артефакта сервиса используется docker образ
- образ деплоится в web платформу deis

# Быстрый старт

## Первичное подключение ci-starter-kit ко проекту

```
git remote add ci-starter-kit git@gitlab.2gis.ru:cd/ci-starter-kit.git
```

## Узнаем последнюю стабильную версию 

`git ls-remote --tags ci-starter-kit | sort -k2 -V -r`

## Подтягиваем исходные файлы из ci-starter-kit в проект

```
ci_version="v0.7.6"; git subtree add --squash --prefix=ci-starter-kit --message="ci $ci_version" ci-starter-kit $ci_version
```

## Настройка переменных

```
cp ci-starter-kit/Makefile.template Makefile
cp ci-starter-kit/Makefile.docker-application-template Makefile.docker-application
cp ci-starter-kit/.gitlab-ci.yml .gitlab-ci.yml
```

В файле Makefile нужно будет указать несколько переменных:
- APPLICATION
- VENDOR
- APPLICATION_PATH

В файле .gitlab-ci.yml поправить переменные окружения под себя и дописать нужные job-ы.

# Дальнейшая работа с ci-starter-kit

## Обновление версии ci-starter-kit в проекте

```
ci_version="v0.7.6"; git subtree pull --squash --prefix=ci-starter-kit --message="ci $ci_version" ci-starter-kit $ci_version
```

## Пробрасывание локальных правок в проекте в ci-starter-kit

```
git subtree push --prefix=ci-starter-kit ci-starter-kit some_branch_name
```

# Основные команды


### Установка сервиса (локально)

```sh
make build
```

### Запуск (локально)

```sh
make run
```

### Сборка сервиса в docker
```sh
make docker-app-install TAG=test
make docker-build-prod-image TAG=test
```

### Запуск образа в docker
```sh
make docker-app-run TAG=test
```

### Сборка сервиса и запуск образа в docker
```sh
make docker-app-run-dev TAG=test
```

## Плезные ссылки

- Официальная документация по `git subtree`: https://github.com/git/git/blob/master/contrib/subtree/git-subtree.txt
- Статья для сравнения подходов по управлению зависимостями: https://andrey.nering.com.br/2016/git-submodules-vs-subtrees/
- Статья от разработчиков Atlassian https://developer.atlassian.com/blog/2015/05/the-power-of-git-subtree/
