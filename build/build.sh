#!/bin/bash
set -e

dotnet restore
dotnet test VStore.UnitTests -c release -r ubuntu.14.04-x64
rm -rf $(pwd)/publish/vstore
dotnet publish VStore.Host/project.json -c release -r ubuntu.14.04-x64 -o $(pwd)/publish/vstore
