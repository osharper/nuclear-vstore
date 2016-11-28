FROM docker-hub.2gis.ru/microsoft/aspnetcore:1.1.0
WORKDIR /app
COPY . .
ENV ASPNETCORE_URLS http://*:5000
EXPOSE 5000
ENTRYPOINT dotnet VStore.Host.dll