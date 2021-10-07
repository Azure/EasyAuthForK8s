FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
WORKDIR /app

COPY ./OCP.Msal.Proxy.sln ./OCP.Msal.Proxy.sln
COPY ./OCP.Msal.Proxy.Web/OCP.Msal.Proxy.Web.csproj ./OCP.Msal.Proxy.Web/OCP.Msal.Proxy.Web.csproj
COPY ./OCP.Msal.Proxy.Tests/OCP.Msal.Proxy.Tests.csproj ./OCP.Msal.Proxy.Tests/OCP.Msal.Proxy.Tests.csproj

RUN dotnet restore

COPY ./OCP.Msal.Proxy.Web ./OCP.Msal.Proxy.Web
COPY ./OCP.Msal.Proxy.Tests ./OCP.Msal.Proxy.Tests

#test
LABEL test=true
RUN dotnet tool install dotnet-reportgenerator-globaltool --version 4.4.6 --tool-path /tools
RUN dotnet test --results-directory /testresults --logger "trx;LogFileName=test_results.xml" /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/testresults/coverage/ /p:Exclude="[xunit.*]*%2c[OCP.Msal.Proxy.Tests]" ./OCP.Msal.Proxy.Tests/OCP.Msal.Proxy.Tests.csproj

RUN /tools/reportgenerator "-reports:/testresults/coverage/coverage.cobertura.xml" "-targetdir:/testresults/coverage/reports" "-reporttypes:HTMLInline;HTMLChart"
RUN ls -la /testresults/coverage/reports
# RUN dotnet test ./OCP.Msal.Proxy.sln --configuration Release --no-restore

RUN dotnet publish ./OCP.Msal.Proxy.Web/OCP.Msal.Proxy.Web.csproj -c Release -r linux-musl-x64 --self-contained true /p:PublishTrimmed=true -o out 

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine
WORKDIR /app
COPY --from=build-env /app/out .

ENTRYPOINT ["./OCP.Msal.Proxy.Web"]