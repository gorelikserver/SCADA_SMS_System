#!/bin/bash
echo "Starting IAA AFCON SMS Mock Server..."
echo ""

dotnet run --project . --environment Production --urls http://localhost:5555