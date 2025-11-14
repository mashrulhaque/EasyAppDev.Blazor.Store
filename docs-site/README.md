# EasyAppDev.Blazor.Store Documentation Site

This is the static HTML documentation site for EasyAppDev.Blazor.Store.

## Overview

This documentation site is built with pure HTML, CSS, and vanilla JavaScript for:
- **Fast loading**: No React bundle, instant page loads
- **No routing issues**: Static HTML files work perfectly on GitHub Pages
- **Better SEO**: Search engines can easily crawl static pages
- **Simple maintenance**: No build step required for content updates

## Structure

```
docs-site/
├── index.html                    # Home page
├── getting-started.html          # Quick start guide
├── core-concepts.html            # Core concepts
├── state-management.html         # State management patterns
├── components.html               # Component documentation
├── performance.html              # Performance optimization
├── devtools.html                 # DevTools integration
├── persistence.html              # State persistence
├── middleware.html               # Middleware guide
├── best-practices.html           # Best practices
├── migration.html                # Migration guide
├── examples.html                 # Code examples
├── api-reference.html            # API reference
├── async-helpers/                # Async helper pages
│   ├── index.html
│   ├── update-debounced.html
│   ├── async-data.html
│   ├── execute-async.html
│   ├── update-throttled.html
│   └── lazy-load.html
├── assets/
│   ├── css/
│   │   └── styles.css           # All styles in one file
│   └── js/
│       └── main.js              # Mobile menu & navigation
├── convert.py                    # Python script for generating pages
└── .nojekyll                     # GitHub Pages configuration
```

## Development

### Local Development

Serve the site locally with any static server:

```bash
# Python 3
python3 -m http.server 8000

# Node.js
npx serve

# PHP
php -S localhost:8000
```

Then open http://localhost:8000

### Adding New Pages

1. Use `convert.py` to generate a new page template
2. Or manually create an HTML file using any existing page as a template
3. Update navigation links in all pages (or modify the template and regenerate)

### Syntax Highlighting

Code blocks use [Prism.js](https://prismjs.com/) loaded from CDN:
- Theme: `prism-tomorrow` (dark theme)
- Languages: C#, Bash, JSON

Example:
```html
<div class="code-block">
  <div class="code-block-title">CounterState.cs</div>
  <pre><code class="language-csharp">
public record CounterState(int Count);
  </code></pre>
</div>
```

### Styling

All styles are in `assets/css/styles.css`:
- CSS variables for easy theming
- Responsive design with mobile-first approach
- Dark code blocks with light content
- Accessible navigation with ARIA landmarks

## Deployment

The site is automatically deployed to GitHub Pages via GitHub Actions:
- Workflow: `.github/workflows/deploy-docs.yml`
- Trigger: Push to `main` branch with changes in `docs-site/**`
- URL: https://mashmawy.github.io/EasyAppDev.Blazor.Store/

### Manual Deployment

The site can be deployed to any static hosting:
- GitHub Pages
- Netlify
- Vercel
- AWS S3
- Azure Static Web Apps

Just copy the entire `docs-site` folder to your hosting provider.

## Key Features

✅ **No routing issues**: Direct HTML files, no SPA routing problems
✅ **Fast load times**: ~50KB total (HTML + CSS + JS)
✅ **Mobile responsive**: Works on all screen sizes
✅ **Syntax highlighting**: Beautiful code blocks with Prism.js
✅ **SEO friendly**: Static HTML is easily crawlable
✅ **No build step**: Edit HTML and deploy directly

## Migration from React

The site was previously built with React but converted to static HTML for:
1. Better performance (no JS bundle loading)
2. Simpler deployment (no build step)
3. No GitHub Pages routing issues
4. Better SEO and faster indexing

The old React version is backed up in `docs-site-react-backup/`.

## Browser Support

- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)
- Mobile browsers (iOS Safari, Chrome Mobile)

## Contributing

To update documentation:
1. Edit the HTML files directly
2. Test locally with a static server
3. Commit and push to trigger automatic deployment

For major structural changes, update `convert.py` and regenerate all pages for consistency.

## License

Same as the main EasyAppDev.Blazor.Store project.
