  // ======================= FIREBASE SETUP ===========================
  import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
  import {
    getFirestore, collection, getDocs, query, orderBy, limit, doc, getDoc
  } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";
  import { firebaseConfig } from "../firebaseConfig.js";

  const app = initializeApp(firebaseConfig);
  const db = getFirestore(app);
  

async function updateDashboardStats() {
  try {
    // 🌀 Start loading animation
    StatLoader.start(".stats .card .info h2");

    let categories = [];
    let indoorInfras = [];
    let buildingsCount = 0;
    let categoriesCount = 0;
    let roomsCount = 0;
    let currentVersion = "—";

    if (navigator.onLine) {
      const [categoriesSnap, indoorSnap, mapVersionsSnap] = await Promise.all([
        getDocs(collection(db, "Categories")),
        getDocs(collection(db, "IndoorInfrastructure")),
        getDocs(collection(db, "MapVersions"))
      ]);

      categories = categoriesSnap.docs.map(d => d.data());
      indoorInfras = indoorSnap.docs.map(d => d.data());

      const mapVersions = mapVersionsSnap.docs.map(d => ({ id: d.id, ...d.data() }));
      const activeMap = mapVersions.find(m => m.current_active_map && m.current_version);

      if (activeMap) {
        const mapId = activeMap.map_id || activeMap.id;
        const activeVersion = activeMap.current_version;
        currentVersion = activeVersion || "—";

        if (mapId && activeVersion) {
          const versionRef = doc(db, "MapVersions", mapId, "versions", activeVersion);
          const versionSnap = await getDoc(versionRef);

          if (versionSnap.exists()) {
            const versionData = versionSnap.data();
            const nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
            const infraNodes = nodes.filter(n => n.type === "infrastructure" && !n.is_deleted);
            buildingsCount = infraNodes.length;
          }
        }
      }

      categoriesCount = categories.filter(c => !c.is_deleted).length;
      roomsCount = indoorInfras.filter(r => !r.is_deleted).length;

    } else {
      const [categoriesRes, indoorRes, mapsRes] = await Promise.all([
        fetch("../assets/firestore/Categories.json"),
        fetch("../assets/firestore/IndoorInfrastructure.json"),
        fetch("../assets/firestore/MapVersions.json")
      ]);

      categories = await categoriesRes.json();
      indoorInfras = await indoorRes.json();
      const mapVersions = await mapsRes.json();

      const activeMap = mapVersions.find(m => m.current_active_map && m.current_version);

      if (activeMap) {
        const activeVersion = activeMap.current_version;
        currentVersion = activeVersion || "—";

        if (activeVersion && activeMap.versions) {
          const version = activeMap.versions.find(v => v.id === activeVersion);
          if (version) {
            const nodes = Array.isArray(version.nodes) ? version.nodes : [];
            const infraNodes = nodes.filter(n => n.type === "infrastructure" && !n.is_deleted);
            buildingsCount = infraNodes.length;
          }
        }
      }

      categoriesCount = categories.filter(c => !c.is_deleted).length;
      roomsCount = indoorInfras.filter(r => !r.is_deleted).length;
    }

    // ✅ Stop loading and update stats
    StatLoader.stop(".stats .card .info h2", {
      buildings: buildingsCount,
      categories: categoriesCount,
      version: currentVersion,
      rooms: roomsCount
    });

    console.log(
      `🏢 Buildings: ${buildingsCount} | 🗂️ Categories: ${categoriesCount} | 🏷️ Version: ${currentVersion} | 🏠 Rooms: ${roomsCount}`
    );
  } catch (err) {
    console.error("Error updating dashboard stats:", err);
  }
}

document.addEventListener("DOMContentLoaded", updateDashboardStats);
window.addEventListener("online", updateDashboardStats);
window.addEventListener("offline", updateDashboardStats);







async function renderRecentActivity() {
  const tbody = document.querySelector(".recent-activity .activity-table tbody");
  if (!tbody) return;

  // 🔹 Show loading spinner inside the table
  showUniversalLoader(tbody, "table");

  try {
    let logs;

    if (navigator.onLine) {
      const q = query(collection(db, "ActivityLogs"), orderBy("timestamp", "desc"));
      const querySnapshot = await getDocs(q);
      logs = querySnapshot.docs.map(doc => ({ id: doc.id, ...doc.data() }));
      console.log("🌐 Online → Firestore");
    } else {
      logs = await fetch("../assets/firestore/ActivityLogs.json").then(res => res.json());
      logs.sort((a, b) => {
        const aTime = a.timestamp?.seconds ?? a.timestamp?._seconds ?? 0;
        const bTime = b.timestamp?.seconds ?? b.timestamp?._seconds ?? 0;
        return bTime - aTime;
      });
      console.log("📂 Offline → JSON fallback");
    }

    // 🔹 Once data is ready, remove loader
    hideUniversalLoader(tbody);

    let counter = 1;
    logs.forEach(data => {
      let formattedDate = "-";
      if (data.timestamp) {
        if (typeof data.timestamp.toDate === "function") {
          formattedDate = formatDate(data.timestamp.toDate());
        } else if (data.timestamp.seconds !== undefined) {
          const d = new Date(data.timestamp.seconds * 1000 + Math.floor(data.timestamp.nanoseconds / 1e6));
          formattedDate = formatDate(d);
        } else if (data.timestamp._seconds !== undefined) {
          const d = new Date(data.timestamp._seconds * 1000 + Math.floor((data.timestamp._nanoseconds || 0) / 1e6));
          formattedDate = formatDate(d);
        }
      }

      const activityText = data.item
        ? `${data.activity || "-"} <i>${data.item}</i>`
        : data.activity || "-";

      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${counter++}</td>
        <td>${activityText}</td>
        <td>${formattedDate}</td>
      `;
      tbody.appendChild(tr);
    });

  } catch (err) {
    console.error("Error loading recent activity:", err);
    tbody.innerHTML = `<tr><td colspan="3" style="text-align:center;color:red;">Failed to load activity</td></tr>`;
  }
}


// Helper function to format Date objects
function formatDate(d) {
    const dateStr = d.toLocaleDateString("en-CA"); // YYYY-MM-DD
    const timeStr = d.toLocaleTimeString("en-US", {
        hour: "numeric",
        minute: "2-digit",
        hour12: true
    });
    return `${dateStr} ${timeStr}`;
}

// Auto re-render on network changes
window.addEventListener("online", renderRecentActivity);
window.addEventListener("offline", renderRecentActivity);

// Run when page loads
document.addEventListener("DOMContentLoaded", renderRecentActivity);










async function populateMaps() {
  const mapSelect = document.getElementById("mapSelect");
  const campusSelect = document.getElementById("campusSelect");
  const versionSelect = document.getElementById("versionSelect");

  // Reset dropdowns
  mapSelect.innerHTML = '<option value="">Select Map</option>';
  campusSelect.innerHTML = '<option value="">Select Campus</option>';
  versionSelect.innerHTML = '<option value="">Select Version</option>';

  // 🟥 Show loading spinners
  showDropdownLoader("mapSelect");
  showDropdownLoader("campusSelect");
  showDropdownLoader("versionSelect");

  try {
    let maps;

    if (navigator.onLine) {
      const mapsSnap = await getDocs(collection(db, "MapVersions"));
      maps = mapsSnap.docs.map(doc => ({ id: doc.id, ...doc.data() }));
      console.log("🌐 Online → Firestore: MapVersions");
    } else {
      maps = await fetch("../assets/firestore/MapVersions.json").then(res => res.json());
      console.log("📂 Offline → JSON fallback: MapVersions");
    }

    // Populate map dropdown
    maps.forEach(mapData => {
      const mapId = mapData.id;
      const option = document.createElement("option");
      option.value = mapId;
      option.textContent = `${mapData.map_name} (${mapId})`;
      mapSelect.appendChild(option);
    });

    if (maps.length > 0) {
      const currentMap = maps.find(m => m.current_active_map === m.id) || maps[0];
      mapSelect.value = currentMap.id;

      await populateCampuses(currentMap.id, true, maps);
      await populateVersions(currentMap.id, true, maps);
      await loadMap(currentMap.id);
    }

    mapSelect.addEventListener("change", async () => {
      const selectedMapId = mapSelect.value;
      if (!selectedMapId) return;
      await populateCampuses(selectedMapId, false, maps);
      await populateVersions(selectedMapId, false, maps);
      await loadMap(selectedMapId);
    });

  } catch (err) {
    console.error("Error loading maps:", err);
  } finally {
    // 🟩 Hide loading spinners
    hideDropdownLoader("mapSelect");
    hideDropdownLoader("campusSelect");
    hideDropdownLoader("versionSelect");
  }
}



async function populateCampuses(mapId, selectCurrent = true, mapsData = null) {
    const campusSelect = document.getElementById("campusSelect");
    campusSelect.innerHTML = '<option value="">Select Campus</option>';

    let mapData;

    if (navigator.onLine) {
        // Online: fetch the speci  fic map document from Firestore
        const mapDocRef = doc(db, "MapVersions", mapId);
        const mapDocSnap = await getDoc(mapDocRef);
        if (!mapDocSnap.exists()) return;
        mapData = mapDocSnap.data();
        console.log(`🌐 Online → Firestore: Map ${mapId}`);
    } else {
        // Offline: find the map data in the JSON
        if (!mapsData) {
            // Load JSON if mapsData not provided
            mapsData = await fetch("../assets/firestore/MapVersions.json").then(res => res.json());
        }
        mapData = mapsData.find(m => m.id === mapId);
        if (!mapData) return;
        console.log(`📂 Offline → JSON fallback: Map ${mapId}`);
    }

    const campuses = mapData.campus_included || [];
    const currentCampus = mapData.current_active_campus || "";

    campuses.forEach(campusId => {
        const option = document.createElement("option");
        option.value = campusId;
        option.textContent = campusId;
        campusSelect.appendChild(option);
    });

    // Select the current campus by default if requested
    if (selectCurrent && currentCampus && campuses.includes(currentCampus)) {
        campusSelect.value = currentCampus;
    } else if (campuses.length > 0) {
        campusSelect.value = campuses[0];
    }

    // 🔹 When campus changes, only update view
    campusSelect.addEventListener("change", async () => {
        const selectedCampus = campusSelect.value;
        if (!selectedCampus) return;
        await loadMap(mapId, selectedCampus); // View selected campus
    });
}




async function populateVersions(mapId, selectCurrent = true) {
    const versionSelect = document.getElementById("versionSelect");
    versionSelect.innerHTML = '<option value="">Select Version</option>';

    let mapData, currentVersion, versions = [];

    if (navigator.onLine) {
        // -------- ONLINE --------
        const mapDocRef = doc(db, "MapVersions", mapId);
        const mapDocSnap = await getDoc(mapDocRef);
        if (!mapDocSnap.exists()) return;

        mapData = mapDocSnap.data();
        currentVersion = mapData.current_version || "";

        // Get versions subcollection
        const versionsSnap = await getDocs(collection(db, "MapVersions", mapId, "versions"));
        versions = versionsSnap.docs.map(docSnap => ({ id: docSnap.id, ...docSnap.data() }));

    } else {
        // -------- OFFLINE --------
        const mapsJson = await fetch("../assets/firestore/MapVersions.json").then(res => res.json());
        mapData = mapsJson.find(m => m.id === mapId);
        if (!mapData) return;

        currentVersion = mapData.current_version || "";
        versions = mapData.versions || []; // <- Use the versions array directly from JSON
        console.log(`📂 Offline → Loaded versions for map ${mapId} from JSON`);
    }

    // Populate dropdown
    versions.forEach(v => {
        const option = document.createElement("option");
        option.value = v.id;
        option.textContent = v.id + (v.id === currentVersion ? "  🟢" : "");
        versionSelect.appendChild(option);
    });

    if (selectCurrent && currentVersion) {
        versionSelect.value = currentVersion;
    } else if (versions.length > 0) {
        versionSelect.value = versions[0].id;
    }

    // 🔹 When version changes, update map
    versionSelect.addEventListener("change", async () => {
        const selectedVersion = versionSelect.value;
        if (!selectedVersion) return;
        await loadMap(mapId, document.getElementById("campusSelect").value, selectedVersion);
    });
}

// Call on page load
document.addEventListener("DOMContentLoaded", () => {
    populateMaps();
});







// -------- Map instances & toggle state (new) --------
let mapOverviewInstance = null;
let mapFullInstance = null;
let showAllCampuses = false;
let currentLoadedMapId = null;

// Toggle handler (id="customToggle") — update view when toggled
const customToggleEl = document.getElementById("customToggle");
if (customToggleEl) {
  customToggleEl.addEventListener("change", (e) => {
    showAllCampuses = !!e.target.checked;
    console.log(showAllCampuses ? "🟢 Showing ALL campuses" : "🔴 Showing active campus only");
    if (currentLoadedMapId) loadMap(currentLoadedMapId); // reload
  });
}

// Helper: destroy overview map safely
function destroyOverviewMap() {
  const container = document.getElementById("map-overview");
  if (!container) return;
  try {
    if (mapOverviewInstance) {
      mapOverviewInstance.off();
      mapOverviewInstance.remove();
    }
  } catch (err) {
    console.warn("Map cleanup warning (overview):", err);
  }
  mapOverviewInstance = null;
  if (container._leaflet_id) delete container._leaflet_id;
  container.innerHTML = "";
}

// Helper: destroy full/modal map safely
function destroyFullMap() {
  const container = document.getElementById("map-full");
  if (!container) return;
  try {
    if (mapFullInstance) {
      mapFullInstance.off();
      mapFullInstance.remove();
    }
  } catch (err) {
    console.warn("Map cleanup warning (full):", err);
  }
  mapFullInstance = null;
  if (container._leaflet_id) delete container._leaflet_id;
  container.innerHTML = "";
}

// Helper: compute campus center (special-case CAMP-02)
function computeCampusCenter(nodes, campusId) {
  if (campusId === "CAMP-02") return [6.9130, 122.0630];
  if (!nodes || nodes.length === 0) return [6.9130, 122.0630];
  const valid = nodes.filter(n => n.latitude && n.longitude);
  if (!valid.length) return [6.9130, 122.0630];
  const avgLat = valid.reduce((s, n) => s + Number(n.latitude), 0) / valid.length;
  const avgLng = valid.reduce((s, n) => s + Number(n.longitude), 0) / valid.length;
  return [avgLat, avgLng];
}

// -------- Revised loadMap (replaces previous implementation) --------
async function loadMap(mapId = null, campusId = null, versionId = null) {
  const containerId = "map-overview";
  showMapLoader(containerId);
  try {
    currentLoadedMapId = mapId; // remember for toggle reloads

    // --- Fetch map/version/nodes/edges (same behavior online/offline as before) ---
    let mapData = null;
    let versionData = null;
    let nodes = [];
    let edges = [];
    let infraMap = {}, campusMap = {};

    if (navigator.onLine) {
      // Online path (unchanged, but ensures mapId selection)
      const mapVersionsRef = collection(db, "MapVersions");
      const mapsSnapshot = await getDocs(mapVersionsRef);
      if (mapsSnapshot.empty) {
        console.error("No MapVersions found");
        hideMapLoader(containerId);
        return;
      }

      let mapDoc = mapId ? mapsSnapshot.docs.find(d => d.id === mapId) : null;
      if (!mapDoc) mapDoc = mapsSnapshot.docs[0];

      mapData = mapDoc.data();
      mapId = mapDoc.id;

      if (!campusId) {
        campusId = mapData.current_active_campus;
      }

      const currentVersion = versionId || mapData.current_version || "v1.0.0";
      const versionRef = doc(db, "MapVersions", mapId, "versions", currentVersion);
      const versionSnap = await getDoc(versionRef);
      if (!versionSnap.exists()) {
        console.error("Version not found:", currentVersion);
        hideMapLoader(containerId);
        return;
      }

      versionData = versionSnap.data();
      nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
      edges = Array.isArray(versionData.edges) ? versionData.edges : [];

      const [infraSnap, campusSnap] = await Promise.all([
        getDocs(collection(db, "Infrastructure")),
        getDocs(collection(db, "Campus"))
      ]);
      infraSnap.forEach(d => infraMap[d.data().infra_id] = d.data().name);
      campusSnap.forEach(d => campusMap[d.data().campus_id] = d.data().campus_name);

    } else {
      // Offline fallback (unchanged)
      const res = await fetch("../assets/firestore/MapVersions.json");
      const mapsJson = await res.json();
      mapData = mapsJson.find(m => m.map_id === mapId) || mapsJson[0];
      if (!mapData) {
        console.error("No maps found in JSON");
        hideMapLoader(containerId);
        return;
      }
      mapId = mapData.map_id;

      if (!campusId) {
        campusId = mapData.current_active_campus;
      }

      const currentVersion = versionId || mapData.current_version || (mapData.versions?.[0]?.id || "v1.0.0");
      versionData = mapData.versions.find(v => v.id === currentVersion);
      if (!versionData) {
        console.error("Version not found in JSON:", currentVersion);
        hideMapLoader(containerId);
        return;
      }

      nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
      edges = Array.isArray(versionData.edges) ? versionData.edges : [];
      const [infraRes, campusRes] = await Promise.all([
        fetch("../assets/firestore/Infrastructure.json"),
        fetch("../assets/firestore/Campus.json")
      ]);
      const infraJson = await infraRes.json();
      const campusJson = await campusRes.json();
      infraJson.forEach(i => infraMap[i.infra_id] = i.name);
      campusJson.forEach(c => campusMap[c.campus_id] = c.campus_name);
    }

    // --- Filter nodes/edges according to toggle and campusId ---
    if (showAllCampuses && Array.isArray(mapData.campus_included)) {
      // show nodes belonging to any included campus
      nodes = nodes.filter(n => !n.is_deleted && n.campus_id && mapData.campus_included.includes(n.campus_id));
    } else {
      // only show nodes for selected campus; if node has no campus, assign
      if (!campusId) {
        console.warn("No campusId specified — showing all nodes.");
      } else {
        nodes = nodes.filter(n => {
          if (n.is_deleted) return false;
          if (!n.campus_id) n.campus_id = campusId;
          return String(n.campus_id).trim() === String(campusId).trim();
        });
      }
    }

    const validNodeIds = new Set(nodes.map(n => n.node_id));
    edges = edges.filter(e => !e.is_deleted && validNodeIds.has(e.from_node) && validNodeIds.has(e.to_node));

    // attach readable names
    nodes.forEach(d => {
      d.infraName = d.related_infra_id ? (infraMap[d.related_infra_id] || d.related_infra_id) : "-";
      d.campusName = d.campus_id ? (campusMap[d.campus_id] || d.campus_id) : "-";
    });

    // convert lat/lng to numbers if strings
    nodes.forEach(n => {
      if (n.latitude !== undefined) n.latitude = Number(n.latitude);
      if (n.longitude !== undefined) n.longitude = Number(n.longitude);
    });

    // --- Clean up previous overview map safely ---
    destroyOverviewMap();

    // --- Compute center (supports CAMP-02 fixed center) ---
    const [centerLat, centerLng] = computeCampusCenter(nodes, campusId);

    // --- Create overview map ---
    const map = L.map("map-overview", {
      center: [centerLat, centerLng],
      zoom: 18,
      zoomControl: false,
      dragging: true,
      scrollWheelZoom: false,
      doubleClickZoom: false,
      boxZoom: false,
      keyboard: false
    });

    mapOverviewInstance = map;
    const mapContainer = document.getElementById("map-overview");
    if (mapContainer) mapContainer._leaflet_map_instance = map;

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      attribution: "© OpenStreetMap contributors"
    }).addTo(map);

    // render (reuse existing renderDataOnMap signature)
    renderDataOnMap(map, nodes, false, edges);

    // --- Modal open handler (overwrite to avoid multiple listeners) ---
    const modal = document.getElementById("mapModal");
    const closeBtn = document.getElementById("closeModal");
    const mapContainerFull = document.getElementById("map-full");

    mapContainer.onclick = () => {
      modal.style.display = "block";

      setTimeout(() => {
        // destroy any existing full map safely
        destroyFullMap();

        // create full map with same center/zoom as overview
        const currentCenter = map.getCenter();
        const currentZoom = map.getZoom();

        mapFullInstance = L.map("map-full", {
          center: currentCenter,
          zoom: currentZoom,
          zoomControl: true,
          dragging: true,
          scrollWheelZoom: true,
          doubleClickZoom: true,
          boxZoom: true,
          keyboard: true
        });

        // ensure Leaflet internal id cleared (defensive)
        if (mapContainerFull._leaflet_id) delete mapContainerFull._leaflet_id;

        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
          attribution: "© OpenStreetMap contributors"
        }).addTo(mapFullInstance);

        // render interactive (enableClick = true)
        renderDataOnMap(mapFullInstance, nodes, true, edges);

        // fix tiles after modal open
        setTimeout(() => {
          try { mapFullInstance.invalidateSize(); } catch (e) { /* ignore */ }
        }, 300);
      }, 200);
    };

    // Modal close handlers (idempotent)
    closeBtn.onclick = () => {
      modal.style.display = "none";
      destroyFullMap();
    };
    window.onclick = (e) => {
      if (e.target === modal) {
        modal.style.display = "none";
        destroyFullMap();
      }
    };

  } catch (err) {
    console.error("Error loading map data:", err);
  } finally {
    hideMapLoader(containerId);
  }
}









function renderDataOnMap(map, data, enableClick = false, edges = []) {
  window._activeMap = map; // keep reference for popups

  // --- 🟩 GROUP BARRIERS BY CAMPUS (but use same color globally) ---
  const barrierNodes = data.filter(d => d.type === "barrier");
  const barriersByCampus = {};

  barrierNodes.forEach(node => {
    if (!barriersByCampus[node.campus_id]) {
      barriersByCampus[node.campus_id] = [];
    }
    barriersByCampus[node.campus_id].push(node);
  });

  const barrierColor = "green"; // ✅ unified barrier color for all campuses

  // --- Render barrier polygons for each campus ---
  Object.keys(barriersByCampus).forEach(campusId => {
    const campusBarriers = barriersByCampus[campusId];
    if (campusBarriers.length === 0) return;

    // 🔹 Calculate centroid to sort polygon points
    const centroid = campusBarriers.reduce(
      (acc, n) => {
        acc.lat += n.latitude;
        acc.lng += n.longitude;
        return acc;
      },
      { lat: 0, lng: 0 }
    );
    centroid.lat /= campusBarriers.length;
    centroid.lng /= campusBarriers.length;

    // 🔹 Sort barrier nodes around centroid (clockwise)
    const sortedNodes = [...campusBarriers].sort((a, b) => {
      const angleA = Math.atan2(a.latitude - centroid.lat, a.longitude - centroid.lng);
      const angleB = Math.atan2(b.latitude - centroid.lat, b.longitude - centroid.lng);
      return angleA - angleB;
    });

    const barrierCoords = sortedNodes.map(b => [b.latitude, b.longitude]);

    // 🔹 Draw polygon (same color for all campuses)
    const polygon = L.polygon(barrierCoords, {
      color: barrierColor,
      weight: 3,
      fillOpacity: 0.1
    }).addTo(map);

    // 🏷️ Campus label (optional, still shows campus name)
    const label = L.marker([centroid.lat, centroid.lng], {
      icon: L.divIcon({
        className: "campus-label",
        html: `<b style="color:${barrierColor}; font-size:14px;">${campusId}</b>`,
        iconSize: [100, 20],
        iconAnchor: [50, 10]
      })
    }).addTo(map);

    if (enableClick) {
      polygon.on("click", (e) => {
        showDetails({
          name: `Campus ${campusId} Area`,
          type: "Barrier Fence",
          latitude: e.latlng.lat.toFixed(6),
          longitude: e.latlng.lng.toFixed(6)
        });
      });

      // 🔹 Barrier corner markers
      sortedNodes.forEach(node => {
        const cornerMarker = L.circleMarker([node.latitude, node.longitude], {
          radius: 6,
          color: barrierColor,
          fillColor: "lightgreen",
          fillOpacity: 0.9
        }).addTo(map);

        cornerMarker.on("click", () => showDetails({ ...node, _map: map }));
      });
    }
  });

  // --- 🏢 Infrastructure (Buildings) ---
  data.filter(d => d.type === "infrastructure").forEach(building => {
    const marker = L.circleMarker([building.latitude, building.longitude], {
      radius: 6,
      color: "red",
      fillColor: "pink",
      fillOpacity: 0.7
    }).addTo(map);

    if (enableClick) {
      marker.on("click", () => showDetails({ ...building, _map: map }));
    }
  });

  // --- 🚪 Rooms ---
  data.filter(d => d.type === "room").forEach(room => {
    const marker = L.marker([room.latitude, room.longitude]).addTo(map);
    if (enableClick) {
      marker.on("click", () => showDetails({ ...room, _map: map }));
    }
  });

  // --- 🌳 Outdoor Nodes ---
  data.filter(d => d.type === "outdoor").forEach(outdoor => {
    const marker = L.circleMarker([outdoor.latitude, outdoor.longitude], {
      radius: 5,
      color: "green",
      fillColor: "#90ee90",
      fillOpacity: 0.8
    }).addTo(map);

    if (enableClick) {
      marker.on("click", () => showDetails({ ...outdoor, _map: map }));
    }
  });

  // --- ⚙️ Intermediate Nodes ---
  data.filter(d => d.type === "intermediate").forEach(intermediate => {
    const marker = L.circleMarker([intermediate.latitude, intermediate.longitude], {
      radius: 4,
      color: "purple",
      fillColor: "violet",
      fillOpacity: 0.8
    }).addTo(map);

    if (enableClick) {
      marker.on("click", () => showDetails({ ...intermediate, _map: map }));
    }
  });

  // --- 🟧 Edges (lines between nodes, skip barriers) ---
  if (edges.length > 0) {
    const nodeMap = new Map();
    data.forEach(node => {
      if (node.node_id && node.latitude && node.longitude) {
        nodeMap.set(node.node_id, {
          coords: [Number(node.latitude), Number(node.longitude)],
          type: node.type,
          campus_id: node.campus_id
        });
      }
    });

    edges.forEach(edge => {
      if (!edge.from_node || !edge.to_node) return;
      const from = nodeMap.get(edge.from_node);
      const to = nodeMap.get(edge.to_node);

      // 🚫 Skip if missing endpoints or if either endpoint is a barrier
      if (!from || !to || from.type === "barrier" || to.type === "barrier") return;

      const isCrossCampus = String(from.campus_id) !== String(to.campus_id);

      // If not showing all campuses, skip cross-campus edges
      if (isCrossCampus && !showAllCampuses) return;

      // Style: dashed for cross-campus to make them visually distinct
      const lineStyle = {
        color: isCrossCampus ? "orange" : "orange",
        weight: 3,
        opacity: 0.9,
      };

      const line = L.polyline([from.coords, to.coords], lineStyle).addTo(map);

      if (enableClick) {
        line.on("click", () => {
          showDetails({
            edge_id: edge.edge_id,
            from: edge.from_node,
            to: edge.to_node,
            distance: edge.distance,
            path_type: edge.path_type
          });
        });
      }
    });
  }
}














// ---- Show details in popup ----
function showDetails(node) {
  let content = `
    <b>${node.name || "Unnamed"}</b><br>
    Type: ${node.type || "-"}<br>
    Lat: ${node.latitude}<br>
    Lng: ${node.longitude}
  `;

  if (node.campusName) content += `<br>Campus: ${node.campusName}`;
  if (node.infraName) content += `<br>Infrastructure: ${node.infraName}`;

  L.popup()
    .setLatLng([node.latitude, node.longitude])
    .setContent(content)
    .openOn(node._map || window._activeMap);
}





    const modal = document.getElementById("mapModal");
  const closeBtn = document.getElementById("closeModal");

  // Close when clicking the X button
  closeBtn.addEventListener("click", () => {
    modal.style.display = "none";
  });

  // Close when clicking outside the modal content
  window.addEventListener("click", (e) => {
    if (e.target === modal) {
      modal.style.display = "none";
    }
  });