# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Install build tools required for AOT compilation
RUN apk add --no-cache clang build-base zlib-dev

# Detect target architecture and set RID
ARG TARGETARCH
RUN case ${TARGETARCH} in \
    amd64) echo "linux-musl-x64" > /tmp/rid ;; \
    arm64) echo "linux-musl-arm64" > /tmp/rid ;; \
    *) echo "linux-musl-x64" > /tmp/rid ;; \
    esac && \
    echo "Building for: $(cat /tmp/rid)"

# Copy solution and project files
COPY src/src.sln .
COPY src/Console/Console.csproj Console/
COPY src/Console.Tests/Console.Tests.csproj Console.Tests/

# Restore dependencies for the target RID
RUN dotnet restore Console/Console.csproj -r $(cat /tmp/rid)

# Copy source code
COPY src/Console/ Console/
COPY src/Console.Tests/ Console.Tests/

# Publish with AOT
WORKDIR /src/Console
RUN dotnet publish -c Release \
    -r $(cat /tmp/rid) \
    -o /app \
    /p:PublishAot=true \
    /p:StripSymbols=true

# Runtime stage - truly minimal with just musl and SSL
FROM scratch AS final
WORKDIR /app

# Copy minimal runtime dependencies from Alpine SDK build stage
COPY --from=build /lib/ld-musl-*.so.1 /lib/
COPY --from=build /usr/lib/libssl.so.* /usr/lib/
COPY --from=build /usr/lib/libcrypto.so.* /usr/lib/
COPY --from=build /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/

COPY --from=build /app/Console .
COPY --from=build /app/appsettings.json .

ENTRYPOINT ["./Console"] 
