VERSION 0.8
FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app/MyMusic

ARG --global projects = "MyMusic.Common MyMusic.OpenTelemetry MyMusic.Server"

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

integration-tests:
    FROM mcr.microsoft.com/dotnet/sdk:10.0

    WORKDIR /app/MyMusic

    FOR proj IN "MyMusic.Common" "MyMusic.OpenTelemetry" "MyMusic.OpenTelemetry.XUnit" "MyMusic.IntegrationTests"
        RUN mkdir -p ./$proj/

        COPY ./$proj/$proj.csproj ./$proj/
    END

    RUN dotnet restore MyMusic.IntegrationTests/MyMusic.IntegrationTests.csproj

    FOR proj IN "MyMusic.Common" "MyMusic.OpenTelemetry" "MyMusic.OpenTelemetry.XUnit" "MyMusic.IntegrationTests"
        COPY ./$proj/ ./$proj/
    END

    COPY MyMusic.sln .
    RUN dotnet publish MyMusic.IntegrationTests \
        --configuration Release \
        -o /app/publish

    SAVE ARTIFACT /app/publish publish

docker:
    FROM mcr.microsoft.com/dotnet/aspnet:10.0
    WORKDIR /app

    ARG REGISTRY='gitea.home'
    ARG IMAGE='silvas/my-music'
    ARG TAG='dev'

    RUN apt-get update && apt-get install -y libgdiplus curl libchromaprint-tools && \
        rm -rf /var/lib/apt/lists/*Earthfile

    COPY +build/publish ./bin

    COPY MyMusic.Server/scripts /usr/local/bin
    RUN chmod +x /usr/local/bin/mymusic-create-user.sh

    ENV DOTNET_NOLOGO=true
    ENV ASPNETCORE_URLS=http://+:8080
    ENV MYMUSIC_CONFIG_FOLDER=/app/config

    WORKDIR /app/bin
    ENTRYPOINT ["dotnet", "MyMusic.Server.dll"]
    EXPOSE 8080

    HEALTHCHECK CMD curl -f "http://localhost:8080/ping" || exit 1

    SAVE IMAGE --push --insecure $REGISTRY/$IMAGE:$TAG

docker-all:
    BUILD +docker
    BUILD ./MyMusic.Client+docker

docker-integration-tests:
    FROM mcr.microsoft.com/dotnet/sdk:10.0
    WORKDIR /app

    ARG REGISTRY='gitea.home'
    ARG IMAGE='silvas/my-music-integrations-tests'
    ARG TAG='dev'

    RUN apt-get update \
        && rm -rf /var/lib/apt/lists/*

    RUN curl -fsSL https://deb.nodesource.com/setup_24.x | bash - \
        && apt-get install -y nodejs \
        && rm -rf /var/lib/apt/lists/*

    RUN corepack enable && corepack prepare pnpm@latest --activate
    RUN corepack enable && corepack install --global pnpm@latest
    ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
    ENV PNPM_HOME="/pnpm"
    ENV PATH="$PNPM_HOME/bin:$PATH"

    ENV PLAYWRIGHT_BROWSERS_PATH=/home/vscode/.cache/ms-playwright
    RUN pnpm install -g playwright && \
        pnpx playwright install chromium --with-deps && \
        pnpm uninstall -g playwright

    COPY ./MyMusic.CLI+package/. /tmp/
    RUN dpkg -i /tmp/my-music-cli_0.0.0_amd64.deb && rm /tmp/my-music-cli_0.0.0_amd64.deb

    COPY +integration-tests/publish ./bin

    COPY MyMusic.IntegrationTests/integration.runsettings /app/bin/integration.runsettings

    WORKDIR /app/bin
    ENTRYPOINT ["dotnet", "vstest", "--Settings:/app/bin/integration.runsettings", "MyMusic.IntegrationTests.dll"]

    SAVE IMAGE --push --insecure $REGISTRY/$IMAGE:$TAG
