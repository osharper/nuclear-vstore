# CI starter kit

**CI starter kit** - это проект для быстрой настройки continuous delivery (CD) в любом проекте на базе инфраструктуры 2ГИС.

Особенности реализации:
- pipeline полностью основан на gitlab-ci
- используется готовая инфраструктура 2ГИС от команды io (shared runners) для запуска job
- в качестве артефакта сервиса используется docker образ
- docker образ деплоится в web платформу deis v2 ( kubernetes )
- проект может быть использован для любого языка программирования (проверено на golang, nodejs, scala, python, php, c#)
- ci поддерживает возможность работы с несколькими приложениями в одном репозитории, типа инверсного cmd в golang или модули в scala:
  само приложение, специфичные моки, специфичные интеграционные тесты. Через ci можно собрать образы для любого из этих компонентов.
- все команды ci воспроизводимы на локальной машине в изолированном окружении через вызовы типа `make docker-app-*`

# Почему мне нужен ci starter kit ?

  * Есть полностью готовый pipeline и команды по созданию docker образа
  * Налажен и проверен процесс деплоя docker образа в deis v1 | v2
  * Не нужно самому задаваться вопросом, что такое shared ranner и как его настраиваить и потом поддерживать
  * Есть стандартизированный набор cli команд для работы с приложением
  * Учтен опыт 15+ проектов по настройке ci. Это наработки, проверенные временем
  * Есть rollback из коробки без перезапуска всего pipeline через UI gitlab-а
  * Настроенное тегированние production docker образов для возможности на локальной машине поднять в любой момент времени требуемую версию приложения
  * Вывод deis info после деплоя в web платформу
  * Есть docker healthcheck для собранного production контейнера, который вызывается до деплоя в web платформу (удобно при разработке в ветке)
  * Есть ограничение времени вызова каждой job-ы в .gitlab-ci.yml

# Быстрый старт

1. **Первое подключение ci-starter-kit к проекту.**

    Добавляем remote
    ```bash
    git remote add ci-starter-kit git@gitlab.2gis.ru:cd/ci-starter-kit.git
    ```
    На странице [https://gitlab.2gis.ru/cd/ci-starter-kit/tags](tags) уточняем последнюю стабильную версию ci и подключаем исходные файлы из ci-starter-kit в проект командой
    ```bash
    ci_version="v0.8.1"; git subtree add --squash --prefix=ci-starter-kit --message="ci $ci_version" ci-starter-kit $ci_version
    ```

2. **Настройка главного Makefile-а проекта**

    Копируем шаблоны для проекта
    ```
    cp ci-starter-kit/Makefile.template ./Makefile
    cp ci-starter-kit/Makefile.docker-application-template ./Makefile.docker-application
    cp ci-starter-kit/.gitlab-ci.yml.template ./.gitlab-ci.yml
    cp ci-starter-kit/Dockerfile.dev.template ./Dockerfile.dev
    cp ci-starter-kit/Dockerfile.template ./Dockerfile
    cp ci-starter-kit/.dockerignore.template ./.dockerignore
    cp ci-starter-kit/Procfile.template ./Procfile
    ```

3. **Настройка переменных в Makefile-е проекта**

    В Makefile-е нужно будет указать несколько переменных (их описание в шаблоне):
    ```
    * APPLICATION
    * VENDOR
    * APPLICATION_PATH
    * WORKDIR
    * BUILD_PATH
    * DOCKER_RUN_PORT
    * DEIS2_HEALTHCHECK_PATH
    ```
    А также заменить тело стандартных cli команд (install, test, lint, run) на реальные вызовы на языке приложения.

4. **Настройка .gitlab-ci.yml**

    В файле .gitlab-ci.yml нужно дописать дополнительные job-ы, специфичные для сервиса.

5. **Настройка Dockerfile-а для приложения**

    Фактически должно быть два Dockerfile-а:
      1. Dockerfile.dev - для сборки приложения
      2. Dockerfile - для сборки docker контейнера, который будет в дальнейшем тестироваться и деплоиться в web платформу.

    Разница между ними чаще всего в том, что во втором есть только ОС, папка ${BUILD_PATH}, т.е. все то, что нужно для запуска приложения в контейнере и ничего лишнего. Он очень простой и размер самого контейнера минимален. 
    Нужно поправить Dockerfile-ы под свое приложение.

6. **Настройка деплоя в deis v2**
  1. Получаем права доступа (один ci пользователь для команды) к платформе deis в команде io:
      * stage deis - заводим тикет в JIRA в [проект io](https://jira.2gis.ru/projects/IO/)
      * production deis (получается после прохождения выходного ревью) - описано в статье [деплой приложений в продакшн веб-платформу](https://confluence.2gis.ru/pages/viewpage.action?pageId=196345927)

  2. Устанавливаем deis cli [(подробнее в статье Первые шаги с платформой)](https://confluence.2gis.ru/pages/viewpage.action?pageId=154110802):

    ```
    curl -sSL http://gitlab.2gis.ru/continuous-delivery/utilities/raw/master/setup-clients.sh | sh
    ### Use next aliases to access to clusters
    alias deis-prod-m1='DEIS_PROFILE=production-m1 ~/.config/2gis/deis-client-prod-m1'
    alias deis2='DEIS_PROFILE=v2-staging ~/.config/2gis/deis-client-deis-v2'
    alias deis2-prod-m1='DEIS_PROFILE=v2-production-m1 ~/.config/2gis/deis-client-deis-v2'
    ```

  3. Генерируем token пользователей (авторизуемся и берем его из файла конфига)

    ```
    deis login http://deis.web-staging.2gis.ru --username=k.sidenko
    password: ***
    Logged in as k.sidenko
    #
    cat ~/.deis/v2-staging.json
    {"username":"k.sidenko","ssl_verify":true,"controller":"http://deis.web-staging.2gis.ru","token":"72e037b48d39632cb637cfd46b62f7f395227c03","response_limit":0}
    ```

  4. В настройках проекта [https://gitlab.2gis.ru/cd/ci-starter-kit/variables](в UI gitlab-а) указываем переменные:
      * DEIS2_STAGE_USERNAME - имя пользователя для stage deis
      * DEIS2_STAGE_TOKEN - токен пользователя для stage deis
      * DEIS_PROD_M1_USERNAME - имя пользователя для prod_m1 deis
      * DEIS_PROD_M1_TOKEN - токен пользователя для prod_m1 deis

---

# Основные cli команды для работы с приложением

1. Сборка сервиса (локально и в docker контейнере)
```make
make install
make docker-app-install TAG=test
```
Локальная команда отдает результат в корень проекта, тогда как docker команда в папку из переменной BUILD_PATH

2. Прогон unit тестов (локально и в docker контейнере)
```make
make test
make docker-app-test TAG=test
```

3. Прогон тестов на производительность (локально и в docker контейнере)
```make
make bench
make docker-app-bench TAG=test
```

4. Прогон линтеров (локально и в docker контейнере)
```make
make lint
make docker-app-lint TAG=test
```

5. Запуск сервиса (локально и в docker контейнере)
```make
make run
make docker-app-run TAG=test
```

6. Сборка production docker образа
```make
make docker-build-prod-image TAG=test
```

7. Сборка сервиса и запуск образа в docker
```make
make docker-app-run-dev TAG=test
```

---

# FAQ

1. Как мне сбрать docker образ приложения и запустить его локально?
```make
make docker-app-install TAG=test
make docker-build-prod-image TAG=test
make docker-app-run TAG=test
```

2. Как мне ограничить сборку по времени выполнения?
```make
# source ci-starter-kit/utils.sh
# 300 sec = 5 min - максимальное время выполнения команды, после которого процесс будет убит.
time-limit 300 make docker-build-prod-image TAG=test
```

3. Как обновить версию ci-starter-kit в проекте?
```
ci_version="v0.8.1"; git subtree pull --squash --prefix=ci-starter-kit --message="ci $ci_version" ci-starter-kit $ci_version
```

4. Как пробросить локальные правки в проекте в ci-starter-kit?
```
git subtree push --prefix=ci-starter-kit ci-starter-kit some_branch_name
```

5. Что такое subtree и где можно почитать про него?

  - Официальная документация по `git subtree`: https://github.com/git/git/blob/master/contrib/subtree/git-subtree.txt
  - Статья для сравнения подходов по управлению зависимостями: https://andrey.nering.com.br/2016/git-submodules-vs-subtrees/
  - Статья от разработчиков Atlassian https://developer.atlassian.com/blog/2015/05/the-power-of-git-subtree/
