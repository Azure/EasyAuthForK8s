FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

COPY ./src/EasyAuthForK8s.sln ./EasyAuthForK8s.sln

COPY ./src/EasyAuthForK8s.Web/EasyAuthForK8s.Web.csproj \
    ./EasyAuthForK8s.Web/EasyAuthForK8s.Web.csproj

COPY ./src/Tests/EasyAuthForK8s.Tests.Web/EasyAuthForK8s.Tests.Web.csproj \
    ./Tests/EasyAuthForK8s.Tests.Web/EasyAuthForK8s.Tests.Web.csproj

RUN dotnet restore

COPY ./src/EasyAuthForK8s.Web ./EasyAuthForK8s.Web

COPY ./src/Tests/EasyAuthForK8s.Tests.Web ./Tests/EasyAuthForK8s.Tests.Web

#test
LABEL test=true
RUN dotnet tool install dotnet-reportgenerator-globaltool \
    --version 5.0.0 \
    --tool-path /tools

RUN dotnet test \
    --results-directory /testresults \
    --logger "trx;LogFileName=test_results.xml" \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=cobertura \
    /p:CoverletOutput=/testresults/coverage/ \
    /p:Exclude="[xunit.*]*%2c[EasyAuthForK8s.Tests.Web]" \
    ./Tests/EasyAuthForK8s.Tests.Web/EasyAuthForK8s.Tests.Web.csproj

RUN /tools/reportgenerator \
    "-reports:/testresults/coverage/coverage.cobertura.xml" \
    "-targetdir:/testresults/coverage/reports" \
    "-reporttypes:HTMLInline;HTMLChart"

RUN ls -la /testresults/coverage/reports

RUN dotnet publish \
    ./EasyAuthForK8s.Web/EasyAuthForK8s.Web.csproj \
    -c Release \
    -r linux-musl-x64 \
    --self-contained true \
    /p:PublishTrimmed=true \
    -o out 

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine
WORKDIR /app
COPY --from=build-env /app/out .

ENTRYPOINT ["./EasyAuthForK8s.Web"]