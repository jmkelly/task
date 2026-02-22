# Use the official .NET 8 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY Task.Api.csproj .
COPY ../Task.Core/Task.Core.csproj ../Task.Core/
RUN dotnet restore Task.Api.csproj

# Copy everything else and build
COPY . .
COPY ../Task.Core ../Task.Core
RUN dotnet publish Task.Api.csproj -c Release -o /app/publish

# Use the official .NET 8 runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Expose port 80
EXPOSE 80

# Run the app
ENTRYPOINT ["dotnet", "Task.Api.dll"]