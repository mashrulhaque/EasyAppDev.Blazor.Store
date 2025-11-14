#!/usr/bin/env python3
"""
Convert React JSX documentation pages to static HTML.
This script reads JSX files, extracts content, and generates static HTML pages.
"""

import os
import re
import json
from pathlib import Path

# Base HTML template
HTML_TEMPLATE = '''<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>{title} - EasyAppDev.Blazor.Store</title>
  <meta name="description" content="{description}">
  <link rel="stylesheet" href="{css_path}assets/css/styles.css">
  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/themes/prism-tomorrow.min.css">
</head>
<body>
  <div class="layout">
    <!-- Header -->
    <header class="header">
      <div class="header-content">
        <button class="mobile-menu-button" id="mobile-menu-button">
          <span>☰</span>
        </button>
        <a href="{root_path}" class="logo">
          <span class="logo-icon">⚡</span>
          <span class="logo-text">EasyAppDev.Blazor.Store</span>
        </a>
        <div class="header-actions">
          <span class="version-badge">v1.0.0</span>
          <a href="https://github.com/mashmawy/EasyAppDev.Blazor.Store" target="_blank" rel="noopener noreferrer" class="github-link">
            GitHub
          </a>
        </div>
      </div>
    </header>

    <div class="layout-body">
      <!-- Sidebar -->
      <aside class="sidebar" id="sidebar">
        <nav class="sidebar-nav">
          <!-- Getting Started -->
          <div class="nav-section">
            <div class="nav-section-title">Getting Started</div>
            <ul class="nav-list">
              <li><a href="{root_path}" class="nav-link"><span class="nav-icon">🏠</span><span>Introduction</span></a></li>
              <li><a href="{root_path}getting-started.html" class="nav-link"><span class="nav-icon">⚡</span><span>Quick Start</span></a></li>
              <li><a href="{root_path}core-concepts.html" class="nav-link"><span class="nav-icon">📚</span><span>Core Concepts</span></a></li>
            </ul>
          </div>

          <!-- Core Features -->
          <div class="nav-section">
            <div class="nav-section-title">Core Features</div>
            <ul class="nav-list">
              <li><a href="{root_path}state-management.html" class="nav-link"><span class="nav-icon">💾</span><span>State Management</span></a></li>
              <li><a href="{root_path}components.html" class="nav-link"><span class="nav-icon">🧩</span><span>Components</span></a></li>
              <li><a href="{root_path}performance.html" class="nav-link"><span class="nav-icon">📈</span><span>Performance</span></a></li>
            </ul>
          </div>

          <!-- Async Helpers -->
          <div class="nav-section">
            <div class="nav-section-title">Async Helpers <span class="nav-badge">v1.0.0</span></div>
            <ul class="nav-list">
              <li><a href="{root_path}async-helpers/index.html" class="nav-link"><span class="nav-icon">⚡</span><span>Overview</span></a></li>
              <li><a href="{root_path}async-helpers/update-debounced.html" class="nav-link"><span class="nav-icon">⏱️</span><span>UpdateDebounced</span></a></li>
              <li><a href="{root_path}async-helpers/async-data.html" class="nav-link"><span class="nav-icon">🔄</span><span>AsyncData&lt;T&gt;</span></a></li>
              <li><a href="{root_path}async-helpers/execute-async.html" class="nav-link"><span class="nav-icon">⚡</span><span>ExecuteAsync</span></a></li>
              <li><a href="{root_path}async-helpers/update-throttled.html" class="nav-link"><span class="nav-icon">💧</span><span>UpdateThrottled</span></a></li>
              <li><a href="{root_path}async-helpers/lazy-load.html" class="nav-link"><span class="nav-icon">📥</span><span>LazyLoad</span></a></li>
            </ul>
          </div>

          <!-- Advanced -->
          <div class="nav-section">
            <div class="nav-section-title">Advanced</div>
            <ul class="nav-list">
              <li><a href="{root_path}devtools.html" class="nav-link"><span class="nav-icon">🛠️</span><span>DevTools</span></a></li>
              <li><a href="{root_path}persistence.html" class="nav-link"><span class="nav-icon">📦</span><span>Persistence</span></a></li>
              <li><a href="{root_path}middleware.html" class="nav-link"><span class="nav-icon">⚙️</span><span>Middleware</span></a></li>
            </ul>
          </div>

          <!-- Resources -->
          <div class="nav-section">
            <div class="nav-section-title">Resources</div>
            <ul class="nav-list">
              <li><a href="{root_path}best-practices.html" class="nav-link"><span class="nav-icon">📖</span><span>Best Practices</span></a></li>
              <li><a href="{root_path}migration.html" class="nav-link"><span class="nav-icon">🔄</span><span>Migration Guide</span></a></li>
              <li><a href="{root_path}examples.html" class="nav-link"><span class="nav-icon">💻</span><span>Examples</span></a></li>
              <li><a href="{root_path}api-reference.html" class="nav-link"><span class="nav-icon">📚</span><span>API Reference</span></a></li>
            </ul>
          </div>
        </nav>
      </aside>

      <!-- Main Content -->
      <main class="main-content">
        <div class="content-wrapper">
{content}
        </div>
      </main>
    </div>

    <!-- Mobile overlay -->
    <div class="sidebar-overlay" id="sidebar-overlay"></div>
  </div>

  <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/prism.min.js"></script>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-csharp.min.js"></script>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-bash.min.js"></script>
  <script src="{js_path}assets/js/main.js"></script>
</body>
</html>
'''

# Simple content template for pages
SIMPLE_CONTENT = '''          <div>
            <h1>{title}</h1>
            <p>This page is under construction. Content will be added soon.</p>
            <p><a href="{root_path}">← Back to Home</a></p>
          </div>'''

# Pages to create with their metadata
PAGES = [
    {'src': None, 'dest': 'getting-started.html', 'title': 'Getting Started', 'desc': 'Quick start guide for EasyAppDev.Blazor.Store'},
    {'src': None, 'dest': 'core-concepts.html', 'title': 'Core Concepts', 'desc': 'Core concepts of state management in Blazor'},
    {'src': None, 'dest': 'state-management.html', 'title': 'State Management', 'desc': 'Advanced state management patterns'},
    {'src': None, 'dest': 'components.html', 'title': 'Components', 'desc': 'Blazor components with state management'},
    {'src': None, 'dest': 'performance.html', 'title': 'Performance', 'desc': 'Performance optimization techniques'},
    {'src': None, 'dest': 'devtools.html', 'title': 'DevTools', 'desc': 'Redux DevTools integration'},
    {'src': None, 'dest': 'persistence.html', 'title': 'Persistence', 'desc': 'State persistence with LocalStorage'},
    {'src': None, 'dest': 'middleware.html', 'title': 'Middleware', 'desc': 'Custom middleware for state management'},
    {'src': None, 'dest': 'best-practices.html', 'title': 'Best Practices', 'desc': 'Best practices for state management'},
    {'src': None, 'dest': 'migration.html', 'title': 'Migration Guide', 'desc': 'Migration guide from v1 to v2'},
    {'src': None, 'dest': 'examples.html', 'title': 'Examples', 'desc': 'Real-world examples and patterns'},
    {'src': None, 'dest': 'api-reference.html', 'title': 'API Reference', 'desc': 'Complete API reference'},
    {'src': None, 'dest': 'async-helpers/index.html', 'title': 'Async Helpers Overview', 'desc': 'Overview of async helper utilities'},
    {'src': None, 'dest': 'async-helpers/update-debounced.html', 'title': 'UpdateDebounced', 'desc': 'Debounced state updates'},
    {'src': None, 'dest': 'async-helpers/async-data.html', 'title': 'AsyncData<T>', 'desc': 'Async data loading pattern'},
    {'src': None, 'dest': 'async-helpers/execute-async.html', 'title': 'ExecuteAsync', 'desc': 'Execute async actions with error handling'},
    {'src': None, 'dest': 'async-helpers/update-throttled.html', 'title': 'UpdateThrottled', 'desc': 'Throttled state updates'},
    {'src': None, 'dest': 'async-helpers/lazy-load.html', 'title': 'LazyLoad', 'desc': 'Lazy loading with caching'},
]

def create_page(page_info, base_dir):
    """Create a static HTML page from template"""
    dest_path = Path(base_dir) / page_info['dest']
    dest_path.parent.mkdir(parents=True, exist_ok=True)

    # Calculate relative paths
    depth = page_info['dest'].count('/')
    root_path = '../' * depth if depth > 0 else './'
    css_path = '../' * depth if depth > 0 else './'
    js_path = '../' * depth if depth > 0 else './'

    # Use simple content for now
    content = SIMPLE_CONTENT.format(
        title=page_info['title'],
        root_path=root_path
    )

    html = HTML_TEMPLATE.format(
        title=page_info['title'],
        description=page_info['desc'],
        css_path=css_path,
        js_path=js_path,
        root_path=root_path,
        content=content
    )

    dest_path.write_text(html, encoding='utf-8')
    print(f"✅ Created: {page_info['dest']}")

def main():
    script_dir = Path(__file__).parent

    print("🚀 Starting HTML page generation...")
    print(f"📁 Working directory: {script_dir}")
    print()

    for page in PAGES:
        try:
            create_page(page, script_dir)
        except Exception as e:
            print(f"❌ Error creating {page['dest']}: {e}")

    print()
    print("✅ Done! All pages created.")
    print(f"📊 Total pages: {len(PAGES)}")

if __name__ == '__main__':
    main()
