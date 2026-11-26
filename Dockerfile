# Multi-stage build for .NET 10 Web App
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["StudentManagement.csproj", "./"]
RUN dotnet restore "StudentManagement.csproj"

# Copy all source code and publish
COPY . .
RUN dotnet publish "StudentManagement.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose port 80 for production web traffic
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "StudentManagement.dll"]
