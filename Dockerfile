FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /action

ENV TZ="America/Chicago"

# Copy everything
COPY src/PlotGitHubAction/*.cs .
COPY src/PlotGitHubAction/Utils/*.cs .
COPY src/PlotGitHubAction/PlotGitHubAction.csproj .

# Restore as distinct layers
RUN dotnet restore

# Build and publish a release
# Runtime IDs: https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.NETCore.Platforms/src/runtime.json
RUN dotnet publish \
  --configuration Release \
  --runtime linux-x64 \
  --no-self-contained \
  -o built

# Check size
RUN du -sh .
RUN du -sh built

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0
COPY --from=build-env /action/built/ /action

# required for fonts
RUN apt update && \
    apt install -y libfontconfig1

ENTRYPOINT ["/action/PlotGitHubAction"]
