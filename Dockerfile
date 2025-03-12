# ===========================
# Build Stage
# ===========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env

# Set the working directory in the container
WORKDIR /app

# Copy the project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the entire project directory into the container
COPY . ./

# Build the application
RUN dotnet publish -c Release -o out

# ===========================
# Runtime Stage
# ===========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Set the working directory in the container
WORKDIR /app

# Copy the published output from the build stage
COPY --from=build-env /app/out .

# Explicitly copy bot_config.json into the runtime environment
COPY bot_config.json /app/bot_config.json
RUN chmod 644 /app/bot_config.json

# Explicitly copy serviceAccountKey.json into the runtime environment
COPY serviceAccountKey.json /app/serviceAccountKey.json
RUN chmod 644 /app/serviceAccountKey.json

# Create the languages directory
RUN mkdir -p /app/languages

# Explicitly copy /languages/en.json into the runtime environment
COPY languages/en.json /app/languages/en.json
RUN chmod 644 /app/languages/en.json

# Explicitly copy /languages/am.json into the runtime environment
COPY languages/am.json /app/languages/am.json
RUN chmod 644 /app/languages/am.json

# Expose ports (optional)
EXPOSE 80 443

# Run the bot
ENTRYPOINT ["dotnet", "Yoda_Bot.dll"]