#! /bin/sh
export TAG="test"
export TIME_LIFE_IMAGE=2592000 # 7 days

#PARALLELIZE="-j 3"
PARALLELIZE=""

#make docker-build-dev-image
make $PARALLELIZE docker-app-test docker-app-lint docker-app-bench
make docker-app-build
make docker-build-prod-image
make docker-run-app
make docker-run-deis-client \
    deis-config-create DEIS_PROFILE=staging DEIS_USERNAME=${DEIS_STAGE_USERNAME} DEIS_TOKEN=${DEIS_STAGE_TOKEN} \
    deis-create deis-config-push deis-pull deis-scale deis-info DEIS_PROFILE=staging DEIS_ENV_FILE=env/staging TAG=${TAG}
make docker-images-gc TAG="master-"
make docker-images-gc TAG="branch-"
make docker-images-gc TAG="dev-"
