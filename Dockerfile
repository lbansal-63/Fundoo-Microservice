# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT_PATH
WORKDIR /src
COPY . .
RUN dotnet publish "${PROJECT_PATH}" -c Release -o /app/publish /p:UseAppHost=false
# Gateway ke liye ocelot*.json bhi saath lo (baaki services mein kuch copy nahi hota, theek hai)
RUN cp "$(dirname "${PROJECT_PATH}")"/ocelot*.json /app/publish/ 2>/dev/null || true

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
ARG DLL_NAME
ENV DLL_NAME=${DLL_NAME}
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
USER app
ENTRYPOINT ["sh", "-c", "exec dotnet \"$DLL_NAME\""]