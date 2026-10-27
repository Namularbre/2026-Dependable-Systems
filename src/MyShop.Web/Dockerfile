FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Restore first, from project files only, so the layer is cached until dependencies change.
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["src/MyShop.Domain/MyShop.Domain.csproj", "src/MyShop.Domain/"]
COPY ["src/MyShop.Application/MyShop.Application.csproj", "src/MyShop.Application/"]
COPY ["src/MyShop.Infrastructure.Persistence/MyShop.Infrastructure.Persistence.csproj", "src/MyShop.Infrastructure.Persistence/"]
COPY ["src/MyShop.Web/MyShop.Web.csproj", "src/MyShop.Web/"]
RUN dotnet restore "src/MyShop.Web/MyShop.Web.csproj"
COPY src/ src/
RUN dotnet publish "src/MyShop.Web/MyShop.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyShop.Web.dll"]
