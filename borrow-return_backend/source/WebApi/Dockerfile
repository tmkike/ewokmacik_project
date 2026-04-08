FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["source/WebApi/WebApi.csproj", "source/WebApi/"]
COPY ["source/Domain/Domain.csproj", "source/Domain/"]
COPY ["source/DataAccess/DataAccess.csproj", "source/DataAccess/"]
COPY ["NuGet.Config", "./"]

RUN dotnet restore "source/WebApi/WebApi.csproj" --configfile /src/NuGet.Config

COPY . .
RUN dotnet publish "source/WebApi/WebApi.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "WebApi.dll"]
