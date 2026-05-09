// Minimal HTML partial loader
// Usage: <div data-include="/partials/header.html"></div>

(async function loadPartials() {
  const slots = document.querySelectorAll('[data-include]');
  await Promise.all(Array.from(slots).map(async (el) => {
    const url = el.getAttribute('data-include');
    try {
      const res = await fetch(url, { cache: 'no-cache' });
      if (!res.ok) throw new Error('Failed ' + res.status);
      el.innerHTML = await res.text();
    } catch (err) {
      el.innerHTML = '<p style="color:#c00">Partial failed: ' + url + '</p>';
      console.error(err);
    }
  }));

  // Highlight current nav link
  const path = window.location.pathname.replace(/\/index\.html$/, '/');
  document.querySelectorAll('.nav-links a').forEach((a) => {
    const href = a.getAttribute('href');
    if (href === path || (href !== '/' && path.startsWith(href))) {
      a.style.color = 'var(--color-accent-text)';
      a.style.fontWeight = '600';
    }
  });
})();
