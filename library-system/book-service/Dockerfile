FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["LibrarySystem.BookService.csproj", "./"]
RUN dotnet restore "./LibrarySystem.BookService.csproj"

COPY . .
RUN dotnet publish "./LibrarySystem.BookService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:3001

EXPOSE 3001

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LibrarySystem.BookService.dll"]
