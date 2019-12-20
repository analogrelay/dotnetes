FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS builder
WORKDIR /src

COPY Dotnetes.Operator.csproj /src
RUN dotnet restore

COPY . /src
RUN dotnet publish --runtime linux-x64 --output /app

FROM mcr.microsoft.com/dotnet/core/runtime-deps:3.1
ENV ASPNETCORE_URLS http://*:80
WORKDIR /app
COPY --from=builder /app .
ENTRYPOINT [ "./Dotnetes.Operator" ]