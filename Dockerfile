FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine AS build-env
WORKDIR /app

COPY ./OCP.Msal.Proxy.sln ./OCP.Msal.Proxy.sln
COPY ./OCP.Msal.Proxy.Web ./OCP.Msal.Proxy.Web

RUN dotnet publish ./OCP.Msal.Proxy.Web/OCP.Msal.Proxy.Web.csproj -c Release -r linux-musl-x64 --self-contained true /p:PublishTrimmed=true -o out 

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/runtime-deps:3.1-alpine
WORKDIR /app
COPY --from=build-env /app/out .

ENTRYPOINT ["./OCP.Msal.Proxy.Web"]