# Building
FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build

WORKDIR /source

# Add project
COPY *.csproj ./
RUN dotnet restore

# Add source folders
ADD Controllers/ Controllers/
ADD Database/ Database/
ADD Model/ Model/
ADD Properties/ Properties/
ADD Service/ Service/
Add Transfer/ Transfer/

# Add configurations
COPY *.json ./

# Add start files
COPY *.cs ./

# Build
RUN dotnet publish -c Production -o /app --no-restore

# Deploy
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1

# AWS Credentials
RUN mkdir -p ~/.aws
COPY awscredentials /root/.aws/credentials
COPY awsconfig /root/.aws/config

WORKDIR /app
COPY --from=build /app .

# Expose default port
EXPOSE 80

ENTRYPOINT ["dotnet", "csharp_api.dll"] 