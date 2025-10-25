/* =======================================
   🌍 Universal Inline Stat Loader
   ======================================= */

const StatLoader = {
  // Start loading animation on a list of elements
  start(selector) {
    document.querySelectorAll(selector).forEach(el => {
      // Skip if already loading
      if (el.dataset.loading === "true") return;

      el.dataset.originalText = el.textContent;
      el.dataset.loading = "true";

      // Replace content with shimmer
      el.textContent = "";
      const shimmer = document.createElement("span");
      shimmer.classList.add("stat-loading");
      el.appendChild(shimmer);
    });
  },

  // Stop loading and restore content
  stop(selector, values = {}) {
    document.querySelectorAll(selector).forEach(el => {
      const key = el.dataset.stat;
      const value = values[key] ?? el.dataset.originalText ?? "—";

      el.dataset.loading = "false";
      el.innerHTML = value;
    });
  }
};



// 🔹 Show a universal loader inside any container
function showUniversalLoader(container, type = "default") {
  if (!container) return;

  // Clear existing content
  container.innerHTML = "";

  // If it's a table body, show a single centered row
  if (type === "table") {
    const tr = document.createElement("tr");
    tr.classList.add("table-loader");
    const td = document.createElement("td");
    td.colSpan = 10; // cover all columns
    td.innerHTML = `<div class="universal-loader"><div class="spinner"></div></div>`;
    tr.appendChild(td);
    container.appendChild(tr);
  } else {
    container.innerHTML = `<div class="universal-loader"><div class="spinner"></div></div>`;
  }
}

// 🔹 Hide the loader and clear it
function hideUniversalLoader(container) {
  if (container) container.innerHTML = "";
}




// 🔄 Universal Map Loader Utilities
function showMapLoader(containerId) {
  const container = document.getElementById(containerId);
  if (!container) return;
  
  // Ensure parent is positioned for overlay
  container.style.position = "relative";

  // Create loader overlay if it doesn’t exist
  let loader = container.querySelector(".map-loading-overlay");
  if (!loader) {
    loader = document.createElement("div");
    loader.className = "map-loading-overlay";
    loader.innerHTML = `<div class="spinner"></div>`;
    container.appendChild(loader);
  }
  loader.style.display = "flex";
}

function hideMapLoader(containerId) {
  const container = document.getElementById(containerId);
  if (!container) return;
  const loader = container.querySelector(".map-loading-overlay");
  if (loader) loader.style.display = "none";
}





// 🔄 Universal Dropdown Loader Utilities
function showDropdownLoader(selectId) {
  const select = document.getElementById(selectId);
  if (!select) return;
  const wrapper = select.closest('.select-loading');
  if (wrapper) wrapper.classList.add('loading');
  select.disabled = true;
}

function hideDropdownLoader(selectId) {
  const select = document.getElementById(selectId);
  if (!select) return;
  const wrapper = select.closest('.select-loading');
  if (wrapper) wrapper.classList.remove('loading');
  select.disabled = false;
}
