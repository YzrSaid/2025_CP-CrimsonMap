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
    let categories = [];
    let indoorInfras = [];
    let buildingsCount = 0;
    let categoriesCount = 0;
    let roomsCount = 0;
    let currentVersion = "—";

    if (navigator.onLine) {
      // 🔹 Online: Firestore
      const [categoriesSnap, indoorSnap, mapVersionsSnap] = await Promise.all([
        getDocs(collection(db, "Categories")),
        getDocs(collection(db, "IndoorInfrastructure")),
        getDocs(collection(db, "MapVersions"))
      ]);

      categories = categoriesSnap.docs.map(d => d.data());
      indoorInfras = indoorSnap.docs.map(d => d.data());

      // 🗺️ Find the active map based on its own current_active_map
      const mapVersions = mapVersionsSnap.docs.map(d => ({ id: d.id, ...d.data() }));
      const activeMap = mapVersions.find(m => m.current_active_map && m.current_version);

      if (activeMap) {
        const mapId = activeMap.map_id || activeMap.id;
        const activeVersion = activeMap.current_version;
        currentVersion = activeVersion || "—";

        // ✅ Fetch nodes from that specific version inside the subcollection
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

      // ✅ Counts for other collections
      categoriesCount = categories.filter(c => !c.is_deleted).length;
      roomsCount = indoorInfras.filter(r => !r.is_deleted).length;

    } else {
      // 🔹 Offline: JSON
      const [categoriesRes, indoorRes, mapsRes] = await Promise.all([
        fetch("../assets/firestore/Categories.json"),
        fetch("../assets/firestore/IndoorInfrastructure.json"),
        fetch("../assets/firestore/MapVersions.json")
      ]);

      categories = await categoriesRes.json();
      indoorInfras = await indoorRes.json();
      const mapVersions = await mapsRes.json();

      // 🗺️ Get active map and its version
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

    // 🔹 Update the dashboard HTML
    const statsCards = document.querySelectorAll(".stats .card .info h2");
    if (statsCards.length >= 4) {
      statsCards[0].textContent = buildingsCount;   // Buildings (Infrastructure)
      statsCards[1].textContent = categoriesCount;  // Categories
      statsCards[2].textContent = currentVersion;   // ✅ Correct Current Version
      statsCards[3].textContent = roomsCount;       // IndoorInfrastructure
    }

    console.log(
      `🏢 Buildings: ${buildingsCount} | 🗂️ Categories: ${categoriesCount} | 🏷️ Version: ${currentVersion} | 🏠 Rooms: ${roomsCount}`
    );

  } catch (err) {
    console.error("Error updating dashboard stats:", err);
  }
}

// Call on page load
document.addEventListener("DOMContentLoaded", updateDashboardStats);
window.addEventListener("online", updateDashboardStats);
window.addEventListener("offline", updateDashboardStats);







// ----------- Load Recent Activity Table -----------
async function renderRecentActivity() {
    const tbody = document.querySelector(".recent-activity .activity-table tbody");
    if (!tbody) return;
    tbody.innerHTML = ""; // Clear table

    try {
        let logs;

        if (navigator.onLine) {
            // Online: fetch from Firestore
            const q = query(collection(db, "ActivityLogs"), orderBy("timestamp", "desc"));
            const querySnapshot = await getDocs(q);
            logs = querySnapshot.docs.map(doc => ({ id: doc.id, ...doc.data() }));
            console.log("🌐 Online → Firestore");
        } else {
            // Offline: fetch from JSON
            logs = await fetch("../assets/firestore/ActivityLogs.json").then(res => res.json());
            // Sort descending by timestamp
            logs.sort((a, b) => {
                const aTime = a.timestamp?.seconds ?? a.timestamp?._seconds ?? 0;
                const bTime = b.timestamp?.seconds ?? b.timestamp?._seconds ?? 0;
                return bTime - aTime;
            });
            console.log("📂 Offline → JSON fallback");
        }

        let counter = 1;
        logs.forEach(data => {
            // Format timestamp
            let formattedDate = "-";
            if (data.timestamp) {
                if (typeof data.timestamp.toDate === "function") {
                    // Firestore Timestamp
                    formattedDate = formatDate(data.timestamp.toDate());
                } else if (data.timestamp.seconds !== undefined) {
                    // JSON export format
                    const d = new Date(data.timestamp.seconds * 1000 + Math.floor(data.timestamp.nanoseconds / 1e6));
                    formattedDate = formatDate(d);
                } else if (data.timestamp._seconds !== undefined) {
                    const d = new Date(data.timestamp._seconds * 1000 + Math.floor((data.timestamp._nanoseconds || 0) / 1e6));
                    formattedDate = formatDate(d);
                }
            }

            // Combine activity + item
            const activityText = data.item 
                ? `${data.activity || "-"} <i>${data.item}</i>` 
                : data.activity || "-";

            // Create table row
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

    mapSelect.innerHTML = '<option value="">Select Map</option>';
    campusSelect.innerHTML = '<option value="">Select Campus</option>';
    versionSelect.innerHTML = '<option value="">Select Version</option>';

    try {
        let maps;

        if (navigator.onLine) {
            // Online: fetch from Firestore
            const mapsSnap = await getDocs(collection(db, "MapVersions"));
            maps = mapsSnap.docs.map(doc => ({ id: doc.id, ...doc.data() }));
            console.log("🌐 Online → Firestore: MapVersions");
        } else {
            // Offline: fetch from JSON
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
            // Select the current active map by default
            const currentMap = maps.find(m => m.current_active_map === m.id) || maps[0];
            mapSelect.value = currentMap.id;

            await populateCampuses(currentMap.id, true, maps);
            await populateVersions(currentMap.id, true, maps);
            await loadMap(currentMap.id); // Load initial map view
        }

        // When Map changes (view only)
        mapSelect.addEventListener("change", async () => {
            const selectedMapId = mapSelect.value;
            if (!selectedMapId) return;
            await populateCampuses(selectedMapId, false, maps);
            await populateVersions(selectedMapId, false, maps);
            await loadMap(selectedMapId); // Load map view for selected map
        });

    } catch (err) {
        console.error("Error loading maps:", err);
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







async function loadMap(mapId = null, campusId = null, versionId = null) {
  try {
    let mapData = null;
    let versionData = null;
    let nodes = [];
    let edges = [];
    let infraMap = {}, campusMap = {};

    if (navigator.onLine) {
      // 🔹 Online: Firestore
      const mapVersionsRef = collection(db, "MapVersions");
      const mapsSnapshot = await getDocs(mapVersionsRef);
      if (mapsSnapshot.empty) {
        console.error("No MapVersions found");
        return;
      }

      // Use provided mapId or fallback to first
      let mapDoc = mapId ? mapsSnapshot.docs.find(d => d.id === mapId) : null;
      if (!mapDoc) mapDoc = mapsSnapshot.docs[0];

      mapData = mapDoc.data();
      mapId = mapDoc.id;

      // ✅ Ensure correct campus is shown on startup
      if (!campusId) {
        campusId = mapData.current_active_campus;
        console.log(`🌍 Defaulting to active campus: ${campusId}`);
      }

      const currentVersion = versionId || mapData.current_version || "v1.0.0";

      // Get the version document
      const versionRef = doc(db, "MapVersions", mapId, "versions", currentVersion);
      const versionSnap = await getDoc(versionRef);
      if (!versionSnap.exists()) {
        console.error("Version not found:", currentVersion);
        return;
      }

      versionData = versionSnap.data();
      nodes = versionData.nodes || [];
      edges = versionData.edges || [];

      // Fetch Infra + Campus names
      const [infraSnap, campusSnap] = await Promise.all([
        getDocs(collection(db, "Infrastructure")),
        getDocs(collection(db, "Campus"))
      ]);
      infraSnap.forEach(doc => infraMap[doc.data().infra_id] = doc.data().name);
      campusSnap.forEach(doc => campusMap[doc.data().campus_id] = doc.data().campus_name);

    } else {
      // 🔹 Offline: JSON
      const res = await fetch("../assets/firestore/MapVersions.json");
      const mapsJson = await res.json();

      // Find the map
      mapData = mapsJson.find(m => m.map_id === mapId) || mapsJson[0];
      if (!mapData) {
        console.error("No maps found in JSON");
        return;
      }
      mapId = mapData.map_id;

      // ✅ Ensure correct campus is shown on startup (offline mode)
      if (!campusId) {
        campusId = mapData.current_active_campus;
        console.log(`🌍 Defaulting to active campus (offline): ${campusId}`);
      }

      const currentVersion = versionId || mapData.current_version || (mapData.versions?.[0]?.id || "v1.0.0");

      versionData = mapData.versions.find(v => v.id === currentVersion);
      if (!versionData) {
        console.error("Version not found in JSON:", currentVersion);
        return;
      }

      nodes = versionData.nodes || [];
      edges = versionData.edges || [];
      console.log("📂 Offline → Map and version loaded from JSON");

      // Fetch Infra + Campus names from JSON
      const [infraRes, campusRes] = await Promise.all([
        fetch("../assets/firestore/Infrastructure.json"),
        fetch("../assets/firestore/Campus.json")
      ]);
      const infraJson = await infraRes.json();
      const campusJson = await campusRes.json();
      infraJson.forEach(i => infraMap[i.infra_id] = i.name);
      campusJson.forEach(c => campusMap[c.campus_id] = c.campus_name);
    }

    // 🔹 Filter nodes & edges by campus
// 🔹 Filter nodes & edges by campus (case-insensitive + fallback)
if (campusId) {
  const normalizedCampusId = campusId.trim().toLowerCase();

  nodes = nodes.filter(n => 
    n.campus_id && n.campus_id.trim().toLowerCase() === normalizedCampusId
  );

  const nodeIds = new Set(nodes.map(n => n.node_id));
  edges = edges.filter(e => nodeIds.has(e.from_node) && nodeIds.has(e.to_node));

  console.log(`🎓 Showing ${nodes.length} nodes for campus: ${campusId}`);
} else {
  console.warn("⚠️ No campusId specified — showing all nodes.");
}

console.log("🔍 Campus nodes preview:", campusId, nodes);
console.log("🔍 Unique node types:", [...new Set(nodes.map(n => n.type))]);


    // Attach readable names
    nodes.forEach(d => {
      d.infraName = d.related_infra_id ? (infraMap[d.related_infra_id] || d.related_infra_id) : "-";
      d.campusName = d.campus_id ? (campusMap[d.campus_id] || d.campus_id) : "-";
    });

    // 🔹 Convert Lat/Lng to X/Y
    const origin = { lat: 6.913341, lng: 122.063693 };
    function latLngToXY(lat, lng, origin) {
      const R = 6371000;
      const dLat = (lat - origin.lat) * Math.PI / 180;
      const dLng = (lng - origin.lng) * Math.PI / 180;
      const x = dLng * Math.cos(origin.lat * Math.PI / 180) * R;
      const y = dLat * R;
      return { x, y };
    }
    nodes.filter(n => n.type === "infrastructure")
         .forEach(b => b.cartesian = latLngToXY(b.latitude, b.longitude, origin));

    // 🔹 Render map (Overview)
    const mapContainer = document.getElementById("map-overview");

    // 🧹 Properly destroy previous Leaflet instance before creating a new one
    if (mapContainer._leaflet_map_instance) {
      mapContainer._leaflet_map_instance.remove();
      mapContainer._leaflet_map_instance = null;
    }

    // 🧩 Also clear any leftover content
    mapContainer.innerHTML = "";


    // 🧭 Dynamically compute the campus center
    let centerLat = 6.9130;
    let centerLng = 122.0630;

    if (nodes.length > 0) {
      // Get only nodes that have valid coordinates
      const validNodes = nodes.filter(n => n.latitude && n.longitude);

      if (validNodes.length > 0) {
        const sumLat = validNodes.reduce((acc, n) => acc + n.latitude, 0);
        const sumLng = validNodes.reduce((acc, n) => acc + n.longitude, 0);
        centerLat = sumLat / validNodes.length;
        centerLng = sumLng / validNodes.length;
        console.log(`📍 Auto-centered map at campus midpoint: ${centerLat}, ${centerLng}`);
      } else {
        console.warn("⚠️ No valid coordinates found for nodes, using default center.");
      }
    }

    // 🗺️ Create map centered on campus
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


    // 💾 Store this map instance for cleanup next time
    mapContainer._leaflet_map_instance = map;


    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      attribution: "© OpenStreetMap contributors"
    }).addTo(map);

    renderDataOnMap(map, nodes, false, edges);

    // 🔹 Modal logic (final safe version)
    const modal = document.getElementById("mapModal");
    const closeBtn = document.getElementById("closeModal");
    const mapContainerFull = document.getElementById("map-full");
    let mapFull = null; // store reference

    function destroyFullMap() {
      if (mapFull) {
        try {
          mapFull.off();
          mapFull.remove();
        } catch (e) {
          console.warn("Map cleanup warning:", e);
        }
        mapFull = null;
      }

      // 🧹 Reset Leaflet internal reference to avoid "reused container" bug
      if (mapContainerFull._leaflet_id) {
        delete mapContainerFull._leaflet_id;
      }

      mapContainerFull.innerHTML = "";
    }

    mapContainer.addEventListener("click", () => {
      modal.style.display = "block";
      setTimeout(() => {
        destroyFullMap(); // ensure no residual map

        // 🧭 Use same computed campus center for modal map
        mapFull = L.map("map-full", {
          center: [centerLat, centerLng],
          zoom: 18,
          zoomControl: true
        });

        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
          attribution: "© OpenStreetMap contributors"
        }).addTo(mapFull);

        renderDataOnMap(mapFull, nodes, true, edges);

        // 🧩 Optional: ensure map tiles load properly after modal animation
        setTimeout(() => mapFull.invalidateSize(), 300);
      }, 200);
    });


    closeBtn.addEventListener("click", () => {
      modal.style.display = "none";
      destroyFullMap(); // fully clean map when closing
    });


  } catch (err) {
    console.error("Error loading map data:", err);
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
          coords: [node.latitude, node.longitude],
          type: node.type,
          campus_id: node.campus_id
        });
      }
    });

    edges.forEach(edge => {
      if (!edge.from_node || !edge.to_node) return;
      const from = nodeMap.get(edge.from_node);
      const to = nodeMap.get(edge.to_node);

      // 🚫 Skip edges if barrier or cross-campus
      if (!from || !to || from.type === "barrier" || to.type === "barrier") return;
      if (from.campus_id !== to.campus_id) return;

      const line = L.polyline([from.coords, to.coords], {
        color: "orange", // ✅ same color for all campuses
        weight: 3,
        opacity: 0.8
      }).addTo(map);

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