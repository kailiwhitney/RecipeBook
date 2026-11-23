# Build + runtime image for the Recipe Book app.
# Build context must be the repo root (recipebook-csharp/), not RecipeBook/.

FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /src
COPY RecipeBook/RecipeBook.csproj ./RecipeBook/
RUN dotnet restore RecipeBook/RecipeBook.csproj
COPY . .
RUN dotnet publish RecipeBook/RecipeBook.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble
WORKDIR /app
COPY --from=build /app .
# launchSettings.json only applies to `dotnet run`; in the container we set Kestrel's
# bind via ASPNETCOREURLS, so nothing extra is needed here.
EXPOSE 8080
ENTRYPOINT ["dotnet", "RecipeBook.dll"]
