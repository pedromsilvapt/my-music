FROM pedromsilvapt/devcontainer:browser

USER root

RUN apt-get update && apt-get install -y \
    curl \
    wget \
    gnupg \
    ca-certificates \
    python3 \
    python3-pip \
    pipx \
    libgdiplus \
    libchromaprint-tools \
    libnss3 \
    libnspr4 \
    libatk1.0-0t64 \
    libatk-bridge2.0-0t64 \
    libatspi2.0-0t64 \
    libxcomposite1 \
    libxdamage1 \
    libxrandr2 \
    libgbm1 \
    libxkbcommon0 \
    libasound2t64 \
    clang \
    zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

USER vscode
