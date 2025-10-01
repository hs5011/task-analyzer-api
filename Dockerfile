# ===== Build stage =====
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app

# ===== Runtime stage =====
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app ./

# Render đặt biến PORT (thường = 10000). Lắng nghe đúng PORT đó.
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}
# (tuỳ chọn) nếu muốn mặc định local 8080 khi PORT chưa set:
ENV PORT=8080

# (không bắt buộc) expose để bạn chạy local
EXPOSE 8080

ENTRYPOINT ["dotnet", "TaskAnalyzerApi.dll"]
