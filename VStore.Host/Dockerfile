FROM docker-hub.2gis.ru/microsoft/aspnetcore:1.1.0
ARG source
RUN pwd
RUN ls -al
RUN printenv
WORKDIR /app
RUN pwd
RUN ls -al
RUN printenv
RUN ls -al publish/
RUN ls -al publish/vstore/
COPY $source .
COPY $source/.aws /root/.aws
ENV ASPNETCORE_URLS http://*:5000
EXPOSE 5000
ENTRYPOINT dotnet VStore.Host.dll