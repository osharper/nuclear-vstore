FROM docker-hub.2gis.ru/microsoft/dotnet:2.0.3-runtime-deps-jessie
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS http://*:5000

ENTRYPOINT ["./VStore.Renderer"]

COPY . .