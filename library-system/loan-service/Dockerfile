FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["LibrarySystem.LoanService.csproj", "./"]
RUN dotnet restore "./LibrarySystem.LoanService.csproj"

COPY . .
RUN dotnet publish "./LibrarySystem.LoanService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:3002

EXPOSE 3002

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LibrarySystem.LoanService.dll"]
