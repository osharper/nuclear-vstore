FROM docker-hub.2gis.ru/microsoft/aspnetcore:1.1.0
ARG source
WORKDIR /app
ENV ASPNETCORE_URLS http://*:5000
EXPOSE 5000
RUN pwd
RUN ls -al
RUN ls -al publish/
RUN ls -al publish/vstore/
RUN printenv
COPY $source .
COPY $source/.aws /root/.aws
ENTRYPOINT dotnet VStore.Host.dll