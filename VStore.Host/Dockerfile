FROM microsoft/aspnetcore:1.1.0
RUN apt-get -qq update
RUN apt-get -qq -y install libgdiplus
RUN ln -s /usr/lib/libgdiplus.so /usr/lib/gdiplus.dll
ARG source=.
WORKDIR /app
ENV ASPNETCORE_URLS http://*:5000
EXPOSE 5000
COPY $source .
COPY .aws /root/.aws
ENTRYPOINT dotnet VStore.Host.dll