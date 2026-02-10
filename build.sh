#!/bin/bash

echo "========================================"
echo "Building KitsuneEngine Modules"
echo "========================================"
echo ""

echo "[1/6] Building KitsuneEngine.Core..."
dotnet build KitsuneEngine.Core/KitsuneEngine.Core.csproj -c Release
if [ $? -ne 0 ]; then
    echo ""
    echo "========================================"
    echo "Build Failed!"
    echo "========================================"
    exit 1
fi

echo "[2/6] Building KitsuneEngine.Graphics..."
dotnet build KitsuneEngine.Graphics/KitsuneEngine.Graphics.csproj -c Release
if [ $? -ne 0 ]; then
    echo ""
    echo "========================================"
    echo "Build Failed!"
    echo "========================================"
    exit 1
fi

echo "[3/6] Building KitsuneEngine.Audio..."
dotnet build KitsuneEngine.Audio/KitsuneEngine.Audio.csproj -c Release
if [ $? -ne 0 ]; then
    echo ""
    echo "========================================"
    echo "Build Failed!"
    echo "========================================"
    exit 1
fi

echo "[4/6] Building KitsuneEngine.Input..."
dotnet build KitsuneEngine.Input/KitsuneEngine.Input.csproj -c Release
if [ $? -ne 0 ]; then
    echo ""
    echo "========================================"
    echo "Build Failed!"
    echo "========================================"
    exit 1
fi

echo "[5/6] Building KitsuneEngine.Assets..."
dotnet build KitsuneEngine.Assets/KitsuneEngine.Assets.csproj -c Release
if [ $? -ne 0 ]; then
    echo ""
    echo "========================================"
    echo "Build Failed!"
    echo "========================================"
    exit 1
fi

echo "[6/6] Building KitsuneEngine.Network..."
dotnet build KitsuneEngine.Network/KitsuneEngine.Network.csproj -c Release
if [ $? -ne 0 ]; then
    echo ""
    echo "========================================"
    echo "Build Failed!"
    echo "========================================"
    exit 1
fi

echo ""
echo "========================================"
echo "Build Complete!"
echo "========================================"
echo ""
echo "Output locations:"
echo "  - KitsuneEngine.Core/bin/Release/net8.0/"
echo "  - KitsuneEngine.Graphics/bin/Release/net8.0/"
echo "  - KitsuneEngine.Audio/bin/Release/net8.0/"
echo "  - KitsuneEngine.Input/bin/Release/net8.0/"
echo "  - KitsuneEngine.Assets/bin/Release/net8.0/"
echo "  - KitsuneEngine.Network/bin/Release/net8.0/"
echo ""
