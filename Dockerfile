# Build the React frontend
FROM node:20-alpine AS ui-build
WORKDIR /src
COPY recipeapp-ui/package*.json ./
RUN npm ci
COPY recipeapp-ui .
RUN npm run build

# Restore and publish the ASP.NET backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY RecipeApp.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
COPY --from=build /app/publish .
COPY --from=ui-build /src/dist ./wwwroot
ENTRYPOINT ["dotnet", "RecipeAppApp.dll"]
