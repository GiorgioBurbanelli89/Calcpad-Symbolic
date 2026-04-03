# Use the official .NET runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the official .NET SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the entire CalcpadVM directory to access all dependencies
COPY . .

# Build the Calcpad.Server Linux project
WORKDIR /src/Calcpad.Server
RUN dotnet restore "Linux/Calcpad.Server.Linux.csproj"
RUN dotnet build "Linux/Calcpad.Server.Linux.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "Linux/Calcpad.Server.Linux.csproj" -c Release -o /app/publish

# Final stage - runtime image
FROM base AS final
WORKDIR /app

# Install chromium for PDF generation with PuppeteerSharp
# Also install dependencies needed for headless browser operation
RUN apt-get update && \
    apt-get install -y \
        chromium \
        fonts-liberation \
        libasound2 \
        libatk-bridge2.0-0 \
        libdrm2 \
        libgtk-3-0 \
        libnspr4 \
        libnss3 \
        libxcomposite1 \
        libxdamage1 \
        libxrandr2 \
        xvfb && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Copy the published application
COPY --from=publish /app/publish .

# Copy necessary files from Calcpad.Cli for templates and resources
COPY --from=build /src/Calcpad.Cli/doc ./doc
COPY --from=build /src/Calcpad.Cli/Fonts ./Fonts

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV CALCPAD_PORT=8080
ENV CALCPAD_HOST=*
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV XDG_CONFIG_HOME=/tmp/.chromium
ENV XDG_CACHE_HOME=/tmp/.chromium

# Create a non-root user and set up directories
RUN groupadd -r calcpad && useradd -r -g calcpad calcpad
RUN mkdir -p /tmp/chrome-crashpad /tmp/.chromium && chown -R calcpad:calcpad /tmp/chrome-crashpad /tmp/.chromium
RUN chown -R calcpad:calcpad /app
USER calcpad

ENTRYPOINT ["dotnet", "Calcpad.Server.dll"]