FROM microsoft/aspnetcore:1.0.1
ARG source=.
WORKDIR /app
ENV ASPNETCORE_URLS http://*:5000
EXPOSE 5000
COPY $source .
ENTRYPOINT dotnet VStore.Host.dll
