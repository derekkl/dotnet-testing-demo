# ---- Build & test stage ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore dotnet-testing-demo.sln
RUN dotnet build dotnet-testing-demo.sln -c Release --no-restore

# Unit and integration tests gate the image build: a failure here stops the
# image from ever being produced, the same way `npm audit` gates the build
# in npm-scan-demo -- just enforced at the container-build layer instead of
# a separate CI step. Functional/E2E tests are NOT run here; they need a
# real deployed instance to test against, which doesn't exist yet at build
# time -- see Dockerfile.functional-tests and the README for that layer.
RUN dotnet test tests/OrderApi.UnitTests -c Release --no-build --logger "console;verbosity=normal"
RUN dotnet test tests/OrderApi.IntegrationTests -c Release --no-build --logger "console;verbosity=normal"

RUN dotnet publish src/OrderApi -c Release --no-build -o /app/publish

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# OpenShift runs containers under an arbitrary, randomly-assigned non-root
# UID (not whatever USER the base image sets) with group 0 (root group).
# This app only reads its own published files at runtime and writes nothing
# to disk, so no extra permission changes are needed for that to work under
# OpenShift's restricted SCC.

ENTRYPOINT ["dotnet", "OrderApi.dll"]
