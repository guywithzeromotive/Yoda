# Use the official .NET SDK as the base image
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env

# Set the working directory in the container
WORKDIR /app

# Copy the csproj file and restore the dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the entire project directory into the container
COPY . ./

# Add this after copying the file
RUN chmod 644 /app/bot_config.json

# Explicitly copy bot_config.json to the correct location
COPY bot_config.json /app/bot_config.json

# List files in /app to verify bot_config.json is present
RUN ls -l /app

# Build the application
RUN dotnet publish -c Release -o out

# Use the official .NET runtime as the base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Set the working directory in the container
WORKDIR /app

# Copy the published application from the build environment into the runtime environment
COPY --from=build-env /app/out .

# Set the entry point to the application's DLL
ENTRYPOINT ["dotnet", "Yoda_Bot.dll"]