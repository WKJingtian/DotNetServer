FROM registry.cn-shenzhen.aliyuncs.com/cr_dockerhub/sdk:9.0-noble AS build
WORKDIR /src

COPY .deploy/ca.crt /usr/local/share/ca-certificates/cr-ca.crt
RUN update-ca-certificates

COPY *.csproj NuGet.config ./
RUN dotnet restore

COPY . .
RUN dotnet publish --no-restore -c Release -o /app

FROM registry.cn-shenzhen.aliyuncs.com/cr_dockerhub/aspnet:9.0-noble

COPY .deploy/ca.crt /usr/local/share/ca-certificates/cr-ca.crt
RUN update-ca-certificates

WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "GameOutside.dll"]
