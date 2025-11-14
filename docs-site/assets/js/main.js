// Mobile menu toggle
document.addEventListener('DOMContentLoaded', function() {
  const menuButton = document.getElementById('mobile-menu-button');
  const sidebar = document.getElementById('sidebar');
  const overlay = document.getElementById('sidebar-overlay');

  if (menuButton && sidebar && overlay) {
    menuButton.addEventListener('click', function() {
      sidebar.classList.toggle('sidebar-open');
      overlay.classList.toggle('show');
    });

    overlay.addEventListener('click', function() {
      sidebar.classList.remove('sidebar-open');
      overlay.classList.remove('show');
    });

    // Close sidebar when clicking a link on mobile
    const navLinks = sidebar.querySelectorAll('.nav-link');
    navLinks.forEach(link => {
      link.addEventListener('click', function() {
        sidebar.classList.remove('sidebar-open');
        overlay.classList.remove('show');
      });
    });
  }

  // Set active nav link based on current page
  const currentPath = window.location.pathname;
  const navLinks = document.querySelectorAll('.nav-link');

  navLinks.forEach(link => {
    const linkPath = new URL(link.href).pathname;

    // Exact match for home page
    if (currentPath === linkPath ||
        (linkPath.endsWith('/') && currentPath === linkPath.slice(0, -1)) ||
        (currentPath.endsWith('/') && linkPath === currentPath.slice(0, -1))) {
      link.classList.add('nav-link-active');
    }
    // Partial match for subpages
    else if (linkPath !== '/' && linkPath !== '/index.html' && currentPath.startsWith(linkPath)) {
      link.classList.add('nav-link-active');
    }
  });
});
