VERSION 0.8
FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app/MyMusic

ARG --global projects = "MyMusic.Common MyMusic.Server"

install:
    COPY MyMusic.sln ./

    FOR proj IN $projects
        RUN mkdir -p ./$proj/

        COPY ./$proj/$proj.csproj ./$proj/

        RUN dotnet restore ./$proj/$proj.csproj
    END

build:
    FROM +install

    ARG configuration='Release'

    FOR proj IN $projects
        RUN mkdir -p ./$proj/

        COPY ./$proj/ ./$proj/
    END

    RUN dotnet publish ./MyMusic.Server -o publish --configuration $configuration

    SAVE ARTIFACT publish publish

docker:
    FROM mcr.microsoft.com/dotnet/aspnet:10.0
    WORKDIR /app

    ARG REGISTRY='gitea.home'
    ARG IMAGE='silvas/my-music'
    ARG TAG='dev'

    RUN apt-get update && apt-get install -y libgdiplus curl && \
        rm -rf /var/lib/apt/lists/*

    COPY +build/publish ./bin

    COPY MyMusic.Server/scripts /usr/local/bin
    RUN chmod +x /usr/local/bin/mymusic-create-user.sh

    ENV DOTNET_NOLOGO=true
    ENV ASPNETCORE_URLS=http://+:8080

    WORKDIR /app/bin
    ENTRYPOINT ["dotnet", "MyMusic.Server.dll"]
    EXPOSE 8080

    HEALTHCHECK CMD curl -f "http://localhost:8080/ping" || exit 1

    SAVE IMAGE $IMAGE:$TAG
    SAVE IMAGE --push --insecure $REGISTRY/$IMAGE:$TAG

docker-all:
    BUILD +docker
    BUILD ./MyMusic.Client+docker
