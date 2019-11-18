FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY ./OCP.Msal.Proxy.Web/OCP.Msal.Proxy.Web.csproj ./OCP.Msal.Proxy.Web/OCP.Msal.Proxy.Web.csproj
COPY ./Microsoft.Identity.Web/Microsoft.Identity.Web.csproj ./Microsoft.Identity.Web/Microsoft.Identity.Web.csproj
COPY ./OCP.Msal.Proxy.sln ./OCP.Msal.Proxy.sln
RUN dotnet restore ./OCP.Msal.Proxy.Web/OCP.Msal.Proxy.Web.csproj
RUN dotnet restore ./Microsoft.Identity.Web/Microsoft.Identity.Web.csproj
# Copy everything else and build
COPY ./OCP.Msal.Proxy.Web ./OCP.Msal.Proxy.Web
COPY ./Microsoft.Identity.Web ./Microsoft.Identity.Web
RUN dotnet publish ./OCP.Msal.Proxy.sln -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:2.2
WORKDIR /app
COPY --from=build-env /app/OCP.Msal.Proxy.Web/out .
ENTRYPOINT ["dotnet", "OCP.Msal.Proxy.Web.dll"]