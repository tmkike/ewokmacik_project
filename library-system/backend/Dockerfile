FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /src

COPY ["LibrarySystem.Api.csproj", "./"]
RUN dotnet restore "./LibrarySystem.Api.csproj"

COPY . .
RUN dotnet build "./LibrarySystem.Api.csproj" -c Release --no-restore

ENV ASPNETCORE_URLS=http://+:3000

EXPOSE 3000

ENTRYPOINT ["dotnet", "bin/Release/net8.0/LibrarySystem.Api.dll"]
