VERSION 0.8
FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app/MyMusic

ARG --global projects = "MyMusic.Common MyMusic.OpenTelemetry MyMusic.Server"

# Optional repository of the prebuilt base images, e.g. <dockerhub-user>/my-music-base (one tag per base image)
# A `<target>-base` target holds what is slow to install and rarely changes for `<target>` (system packages, toolchains,
# package caches). When the arg is set, the base targets are not built: `<repository>:<name>` is pulled instead (see +BASE)
# Republish the images with +base-images-all (Base Images workflow) whenever a `*-base` target changes
ARG --global BASE_IMAGES

# Starts the calling target from its base image: the prebuilt `${BASE_IMAGES}:<name>` when the arg is set, the
# local `+<name>-base` target otherwise. The arg has to be handed over explicitly, functions do not see globals:
#   DO +BASE --name=install --BASE_IMAGES=$BASE_IMAGES
BASE:
    FUNCTION

    ARG --required name
    ARG BASE_IMAGES

    IF [ -n "$BASE_IMAGES" ]
        FROM ${BASE_IMAGES}:$name
    ELSE
        FROM +$name-base
    END

# Publishes one base image as `<PREFIX>:<image>`
base-image:
    ARG --required target
    ARG --required image
    ARG --required PREFIX

    FROM $target

    SAVE IMAGE --push $PREFIX:$image

# Publishes the base images of all the Earthfiles (run with --push); pass the same PREFIX as BASE_IMAGES to use them:
#   earthly --push +base-images-all --PREFIX=<dockerhub-user>/my-music-base
base-images-all:
    ARG --required PREFIX

    BUILD +base-image --PREFIX=$PREFIX --target=+install-base --image=install
    BUILD +base-image --PREFIX=$PREFIX --target=+docker-base --image=docker
    BUILD +base-image --PREFIX=$PREFIX --target=+docker-unit-tests-base --image=docker-unit-tests
    BUILD +base-image --PREFIX=$PREFIX --target=+docker-integration-tests-base --image=docker-integration-tests
    BUILD +base-image --PREFIX=$PREFIX --target=./MyMusic.CLI+install-base --image=cli-install
    BUILD +base-image --PREFIX=$PREFIX --target=./MyMusic.CLI+package-base --image=cli-package
    BUILD +base-image --PREFIX=$PREFIX --target=./MyMusic.Mobile+build-base --image=mobile-build

packages-props:
    COPY Directory.Packages.props ./
    SAVE ARTIFACT Directory.Packages.props

# Compiles the .NET projects; shared by every target that builds them (+install, +unit-tests, +integration-tests)
install-base:
    FROM mcr.microsoft.com/dotnet/sdk:10.0

    # Warm up the NuGet cache; the targets still restore, but only download what changed since the image was built
    WORKDIR /tmp/nuget-warmup

    COPY Directory.Packages.props ./

    FOR proj IN "MyMusic.Common" "MyMusic.OpenTelemetry" "MyMusic.OpenTelemetry.XUnit" "MyMusic.Server" "MyMusic.CLI" "MyMusic.Common.Tests" "MyMusic.CLI.Tests" "MyMusic.IntegrationTests"
        COPY ./$proj/$proj.csproj ./$proj/
    END

    FOR proj IN "MyMusic.Server" "MyMusic.Common.Tests" "MyMusic.CLI.Tests" "MyMusic.IntegrationTests"
        RUN dotnet restore ./$proj/$proj.csproj
    END

    RUN rm -rf /tmp/nuget-warmup

    WORKDIR /app/MyMusic

install:
    DO +BASE --name=install --BASE_IMAGES=$BASE_IMAGES

    COPY MyMusic.sln Directory.Packages.props ./

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
    DO +BASE --name=install --BASE_IMAGES=$BASE_IMAGES

    COPY Directory.Packages.props ./

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

# Runs the server
docker-base:
    FROM mcr.microsoft.com/dotnet/aspnet:10.0

    RUN apt-get update && apt-get install -y libgdiplus curl libchromaprint-tools && \
        rm -rf /var/lib/apt/lists/*

docker:
    DO +BASE --name=docker --BASE_IMAGES=$BASE_IMAGES
    WORKDIR /app

    ARG REGISTRY='gitea.home'
    ARG IMAGE='silvas/my-music'
    ARG TAG='dev'

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

unit-tests:
    DO +BASE --name=install --BASE_IMAGES=$BASE_IMAGES

    COPY Directory.Packages.props ./

    FOR proj IN "MyMusic.Common" "MyMusic.OpenTelemetry" "MyMusic.OpenTelemetry.XUnit" "MyMusic.Server" "MyMusic.CLI" "MyMusic.Common.Tests" "MyMusic.CLI.Tests"
        RUN mkdir -p ./$proj/

        COPY ./$proj/$proj.csproj ./$proj/
    END

    RUN dotnet restore MyMusic.Common.Tests/MyMusic.Common.Tests.csproj
    RUN dotnet restore MyMusic.CLI.Tests/MyMusic.CLI.Tests.csproj

    FOR proj IN "MyMusic.Common" "MyMusic.OpenTelemetry" "MyMusic.OpenTelemetry.XUnit" "MyMusic.Server" "MyMusic.CLI" "MyMusic.Common.Tests" "MyMusic.CLI.Tests"
        COPY ./$proj/ ./$proj/
    END

    COPY MyMusic.sln .
    RUN dotnet publish MyMusic.Common.Tests \
        --configuration Release \
        -o /app/publish/common-tests
    RUN dotnet publish MyMusic.CLI.Tests \
        --configuration Release \
        -o /app/publish/cli-tests

    SAVE ARTIFACT /app/publish publish

# Runs the integration tests: Node, pnpm and the Playwright browsers
docker-integration-tests-base:
    FROM mcr.microsoft.com/dotnet/sdk:10.0

    RUN curl -fsSL https://deb.nodesource.com/setup_24.x | bash - \
        && apt-get install -y nodejs \
        && rm -rf /var/lib/apt/lists/*

    RUN corepack enable && corepack prepare pnpm@latest --activate
    RUN corepack enable && corepack install --global pnpm@latest
    ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
    ENV PNPM_HOME="/pnpm"
    ENV PATH="$PNPM_HOME/bin:$PATH"

    ENV PLAYWRIGHT_BROWSERS_PATH=/home/vscode/.cache/ms-playwright
    RUN pnpm install -g playwright@1.59 && \
        pnpx playwright@1.59 install chromium --with-deps && \
        pnpm uninstall -g playwright

docker-integration-tests:
    DO +BASE --name=docker-integration-tests --BASE_IMAGES=$BASE_IMAGES
    WORKDIR /app

    ARG REGISTRY='gitea.home'
    ARG IMAGE='silvas/my-music-integrations-tests'
    ARG TAG='dev'

    COPY (./MyMusic.CLI+package/. --BASE_IMAGES=$BASE_IMAGES) /tmp/
    RUN dpkg -i /tmp/my-music-cli_0.0.0_amd64.deb && rm /tmp/my-music-cli_0.0.0_amd64.deb

    COPY ./MyMusic.Mobile+test-cli/mobile-cli /app/mobile-cli

    COPY +integration-tests/publish ./bin

    COPY MyMusic.IntegrationTests/integration.runsettings /app/bin/integration.runsettings

    COPY MyMusic.IntegrationTests/run-tests.sh /app/bin/run-tests.sh
    RUN chmod +x /app/bin/run-tests.sh

    WORKDIR /app/bin
    ENTRYPOINT ["/app/bin/run-tests.sh"]

    SAVE IMAGE --push --insecure $REGISTRY/$IMAGE:$TAG

# Runs the unit tests
docker-unit-tests-base:
    FROM mcr.microsoft.com/dotnet/sdk:10.0

    RUN apt-get update && apt-get install -y libgdiplus curl libchromaprint-tools && \
        rm -rf /var/lib/apt/lists/*

docker-unit-tests:
    DO +BASE --name=docker-unit-tests --BASE_IMAGES=$BASE_IMAGES
    WORKDIR /app

    ARG REGISTRY='gitea.home'
    ARG IMAGE='silvas/my-music-unit-tests'
    ARG TAG='dev'

    COPY +unit-tests/publish ./publish

    WORKDIR /app/publish
    ENTRYPOINT ["sh", "-c", "dotnet vstest common-tests/MyMusic.Common.Tests.dll cli-tests/MyMusic.CLI.Tests.dll"]

    SAVE IMAGE --push --insecure $REGISTRY/$IMAGE:$TAG
