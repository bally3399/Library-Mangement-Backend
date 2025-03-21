# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081



# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["fortunae.api/fortunae.api.csproj", "fortunae.api/"]
COPY ["fortunae.Domain/fortunae.Domain.csproj", "fortunae.Domain/"]
COPY ["fortunae.Service/fortunae.Service.csproj", "fortunae.Service/"]
COPY ["fortunae.Infrastructure/fortunae.Infrastructure.csproj", "fortunae.Infrastructure/"]
RUN dotnet restore "./fortunae.api/fortunae.api.csproj"
COPY . .
WORKDIR "/src/fortunae.api"
RUN dotnet build "./fortunae.api.csproj" -c $BUILD_CONFIGURATION -o /app/build

ENV ASPNETCORE_ENVIRONMENT=Production

# Copy the .env file into the container
COPY .env .env

# Use the env variables in the entrypoint
CMD [ "bash", "-c", "source .env && dotnet fortunae.api.dll" ]

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./fortunae.api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "fortunae.api.dll"]