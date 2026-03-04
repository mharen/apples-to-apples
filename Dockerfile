FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Install build tools required for AOT compilation + static OpenSSL libs + UPX
RUN apk add --no-cache clang build-base zlib-dev openssl-libs-static upx

# Detect target architecture and set RID
ARG TARGETARCH
RUN case ${TARGETARCH} in \
    amd64) echo "linux-musl-x64" > /tmp/rid ;; \
    arm64) echo "linux-musl-arm64" > /tmp/rid ;; \
    *) echo "linux-musl-x64" > /tmp/rid ;; \
    esac && \
    echo "Building for: $(cat /tmp/rid)"

# Copy project file and restore dependencies (with RID for AOT)
COPY src/Console/Console.csproj Console/
RUN dotnet restore Console/Console.csproj -r $(cat /tmp/rid)

COPY src/Console/ Console/

# Publish with AOT, then UPX-compress the binary
# upx reduces image size from 13.2MB to 8.9MB (33%)
WORKDIR /src/Console
RUN dotnet publish -c Release \
    -r $(cat /tmp/rid) \
    -o /app \
    /p:PublishAot=true \
    /p:StripSymbols=true && \
    upx --best /app/Console 

# Minimal runtime stage
FROM scratch AS final
WORKDIR /app

# Copy runtime dependencies
COPY --from=build /lib/ld-musl-*.so.1 /lib/
COPY --from=build /usr/lib/libssl.so.* /usr/lib/
COPY --from=build /usr/lib/libcrypto.so.* /usr/lib/
COPY --from=build /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/

COPY --from=build /app/Console .
COPY --from=build /app/appsettings.json .

ENV CACHE_DIR=/cache
VOLUME ["/cache"]

ENTRYPOINT ["./Console"] 
