FROM mcr.microsoft.com/devcontainers/base:ubuntu

RUN apt-get update && apt-get install -y \
    curl \
    wget \
    gnupg \
    ca-certificates \
    python3 \
    python3-pip \
    pipx \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*

COPY certs/ /tmp/certs/
RUN if [ "$(ls -A /tmp/certs/*.crt 2>/dev/null)" ]; then \
        cp /tmp/certs/*.crt /usr/local/share/ca-certificates/ && \
        update-ca-certificates; \
    fi && \
    rm -rf /tmp/certs

RUN wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y dotnet-sdk-10.0 \
    && rm -rf /var/lib/apt/lists/

RUN curl -fsSL https://deb.nodesource.com/setup_24.x | bash - \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*

RUN corepack enable && corepack prepare pnpm@latest --activate
RUN corepack enable && corepack install --global pnpm@latest
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0

RUN curl -fsSL https://get.docker.com -o get-docker.sh \
    && sh get-docker.sh \
    && rm get-docker.sh \
    && usermod -aG docker vscode

RUN su - vscode -c "dotnet tool install --global dotnet-ef" \
    && su - vscode -c "dotnet tool install --global dotnet-aspnet-codegenerator"

RUN su - "vscode" -c "curl -fsSL https://opencode.ai/install | bash" && \
    mkdir -p /home/vscode/.local/share/opencode /home/vscode/.local/state/opencode  && \
    chown -R vscode:vscode /home/vscode/.local/share

RUN mkdir -p /app/data && chown vscode:vscode /app/data

RUN echo fs.inotify.max_user_instances=524288 | sudo tee -a /etc/sysctl.conf && sudo sysctl -p
