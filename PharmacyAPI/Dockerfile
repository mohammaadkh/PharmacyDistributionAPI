# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# نسخ ملف المشروع وعمل restore للحزم
COPY ["PharmacyAPI.csproj", "./"]
RUN dotnet restore "PharmacyAPI.csproj"

# نسخ بقية ملفات المشروع وبنائه
COPY . .
RUN dotnet publish "PharmacyAPI.csproj" -c Release -o /app/publish

# Stage 2: Run the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# إعداد الـ Port ليتوافق مع السيرفر المجاني
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "PharmacyAPI.dll"]