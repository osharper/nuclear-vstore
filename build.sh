#!/bin/bash
set -e

dotnet restore
rm -rf $(pwd)/publish/vstore
dotnet publish VStore.Host/project.json -c release -o $(pwd)/publish/vstore
cp -vr .aws $(pwd)/publish/vstore
