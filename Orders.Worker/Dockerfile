FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Orders.Domain/*.csproj Orders.Domain/
COPY Orders.Contracts/*.csproj Orders.Contracts/
COPY Orders.Infrastructure/*.csproj Orders.Infrastructure/
COPY Orders.Worker/*.csproj Orders.Worker/
RUN dotnet restore Orders.Worker/Orders.Worker.csproj

COPY . .
RUN dotnet publish Orders.Worker/Orders.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Orders.Worker.dll"]