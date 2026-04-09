#!/bin/bash
# HoloCade Version Release Script
# Updates package.json and creates a git tag for the release

set -e

echo "========================================"
echo "HoloCade Version Release Helper"
echo "========================================"
echo

read -p "Enter version number (e.g., 0.1.0): " VERSION
if [ -z "$VERSION" ]; then
    echo "Error: Version cannot be empty"
    exit 1
fi

echo
echo "Step 1: Updating package.json version to $VERSION..."
if command -v jq &> /dev/null; then
    # Use jq if available (more reliable)
    jq ".version = \"$VERSION\"" Packages/com.ajcampbell.holocade/package.json > Packages/com.ajcampbell.holocade/package.json.tmp
    mv Packages/com.ajcampbell.holocade/package.json.tmp Packages/com.ajcampbell.holocade/package.json
else
    # Fallback to sed
    sed -i.bak "s/\"version\": \".*\"/\"version\": \"$VERSION\"/" Packages/com.ajcampbell.holocade/package.json
    rm Packages/com.ajcampbell.holocade/package.json.bak 2>/dev/null || true
fi
echo "  [OK] package.json updated"

echo
echo "Step 2: Staging package.json..."
git add Packages/com.ajcampbell.holocade/package.json

echo
read -p "Commit version change? (y/n): " COMMIT_CHANGES
if [ "$COMMIT_CHANGES" = "y" ] || [ "$COMMIT_CHANGES" = "Y" ]; then
    git commit -m "Bump version to $VERSION"
    echo "  [OK] Version change committed"
fi

echo
read -p "Create git tag v$VERSION? (y/n): " CREATE_TAG
if [ "$CREATE_TAG" = "y" ] || [ "$CREATE_TAG" = "Y" ]; then
    read -p "Tag message (or press Enter for default): " TAG_MESSAGE
    if [ -z "$TAG_MESSAGE" ]; then
        TAG_MESSAGE="Release version $VERSION"
    fi
    
    git tag -a "v$VERSION" -m "$TAG_MESSAGE"
    echo "  [OK] Tag v$VERSION created"
fi

echo
echo "========================================"
echo "Version Release Complete!"
echo "========================================"
echo
echo "Next steps:"
echo "  1. Review changes: git log -1"
if [ "$CREATE_TAG" = "y" ] || [ "$CREATE_TAG" = "Y" ]; then
    echo "  2. Review tag: git show v$VERSION"
    echo "  3. Push commits: git push origin main"
    echo "  4. Push tag: git push origin v$VERSION"
else
    echo "  1. Push commits: git push origin main"
fi
echo



