#!/bin/bash

# Static HTML Page Converter for docs-site-static
# This script helps convert React JSX pages to static HTML

DOCS_SITE="/Users/mashrul/Desktop/dotnet/EasyAppDev.Blazor.Store/docs-site"
DOCS_STATIC="/Users/mashrul/Desktop/dotnet/EasyAppDev.Blazor.Store/docs-site-static"

echo "======================================"
echo "Static HTML Conversion Helper"
echo "======================================"
echo ""
echo "This script will help convert React pages to static HTML."
echo ""
echo "Due to the complexity of JSX-to-HTML conversion,"
echo "we recommend using the React app build output instead:"
echo ""
echo "1. Build the React app: cd $DOCS_SITE && npm run build"
echo "2. Copy build output to docs-site-static"
echo "3. Update paths from /docs-site/ to ./"
echo ""
echo "Alternatively, for pure static HTML:"
echo "- Use a tool like react-snap or react-static"
echo "- Or manually convert each page using the template pattern"
echo ""
echo "Template location: $DOCS_STATIC/index.html"
echo "Source pages: $DOCS_SITE/src/pages/"
echo ""
