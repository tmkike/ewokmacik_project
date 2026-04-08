FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["LibrarySystem.Api.csproj", "./"]
RUN dotnet restore "./LibrarySystem.Api.csproj"

COPY . .
RUN dotnet publish "./LibrarySystem.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:3000

COPY --from=build /app/publish .

EXPOSE 3000

ENTRYPOINT ["dotnet", "LibrarySystem.Api.dll"]
