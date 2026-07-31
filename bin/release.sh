#!/bin/bash
set -e

if [ -z "$1" ]; then
    echo "Usage: $0 <tag> [--testing]"
    exit 1
fi

TAG="$1"
IS_TESTING=false

if [ "$2" == "--testing" ]; then
    IS_TESTING=true
fi

echo "Tag: $TAG"
echo "Testing release: $IS_TESTING"

# Create tag and release
git tag "$TAG"
git push origin master "$TAG"
dotnet build -c Release

if [ "$IS_TESTING" = true ]; then
   gh release create "$TAG" --title "$TAG" --generate-notes --prerelease
else
   gh release create "$TAG" --title "$TAG" --generate-notes
fi

gh release upload "$TAG" BOCCHI/bin/Release/BOCCHI/latest.zip --clobber

# Update plugin manifest
rm -rf plugins
gh repo clone plugins
cd plugins

# Generate manifest
cd manifest-generator
npm install

manifest_output=$(npx tsx src/index.ts)
commit_message=$(echo "$manifest_output" | awk '/^Suggested commit message:/{getline; print}')
if [ -z "$commit_message" ]; then
  commit_message="Update Manifest"
fi

cd ..
git add manifest.json

if ! git diff --cached --quiet; then
  git commit -m "$commit_message"
  git push origin master
else
  echo "No changes to commit."
fi

cd discord-message-generator
npm install

echo "------------------------"
npx tsx src/index.ts

cd ..
rm -rf plugins
