  // ======================= FIREBASE SETUP ===========================
  import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
  import {
    getFirestore, collection, getDocs, query, orderBy, limit, doc, getDoc
  } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";
  import { firebaseConfig } from "./../firebaseConfig.js";

  const app = initializeApp(firebaseConfig);
  const db = getFirestore(app);
  

async function updateDashboardStats() {
    try {
        // --- Buildings (Infrastructure nodes) ---
        const nodesSnap = await getDocs(collection(db, "Nodes"));
        const buildingsCount = nodesSnap.docs
            .map(doc => doc.data())
            .filter(d => d.type === "infrastructure" && !d.is_deleted)
            .length;

        // --- Categories ---
        const categoriesSnap = await getDocs(collection(db, "Categories"));
        const categoriesCount = categoriesSnap.docs
            .map(doc => doc.data())
            .filter(d => !d.is_deleted)
            .length;

        // --- Rooms & Offices ---
        const roomsSnap = await getDocs(collection(db, "Rooms"));
        const roomsCount = roomsSnap.docs
            .map(doc => doc.data())
            .filter(d => !d.is_deleted)
            .length;

        // --- Current Map Version ---
        const mapVersionsSnap = await getDocs(query(collection(db, "MapVersions"), orderBy("createdAt", "desc"), limit(1)));
        let currentVersion = "—";
        if (!mapVersionsSnap.empty) {
            const latestMap = mapVersionsSnap.docs[0].data();
            currentVersion = latestMap.current_version || "—";
        }

        // --- Update HTML ---
        const statsCards = document.querySelectorAll(".stats .card .info h2");
        if (statsCards.length >= 4) {
            statsCards[0].textContent = buildingsCount;      // Buildings
            statsCards[1].textContent = categoriesCount;     // Categories
            statsCards[2].textContent = currentVersion;      // Current Map
            statsCards[3].textContent = roomsCount;          // Rooms & Offices
        }
    } catch (err) {
        console.error("Error updating dashboard stats:", err);
    }
}

// Call on page load
updateDashboardStats();






// ----------- Load Recent Activity Table -----------
async function renderRecentActivity() {
    const tbody = document.querySelector(".recent-activity .activity-table tbody");
    if (!tbody) return;
    tbody.innerHTML = ""; // Clear table

    try {
        const q = query(collection(db, "ActivityLogs"), orderBy("timestamp", "desc")); // show latest first
        const querySnapshot = await getDocs(q);

        let counter = 1;
        querySnapshot.forEach(docSnap => {
            const data = docSnap.data();

            // Format timestamp
            let formattedDate = "-";
            if (data.timestamp && typeof data.timestamp.toDate === "function") {
                const d = data.timestamp.toDate();
                const dateStr = d.toLocaleDateString("en-CA"); // YYYY-MM-DD
                const timeStr = d.toLocaleTimeString("en-US", {
                    hour: "numeric",
                    minute: "2-digit",
                    hour12: true
                });
                formattedDate = `${dateStr} ${timeStr}`; // ✅ fixed template literal
            }

            // Combine activity + item
            const activityText = data.item 
                ? `${data.activity || "-"} <i>${data.item}</i>`  // ✅ fixed template literal
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
        const mapsSnap = await getDocs(collection(db, "MapVersions"));

        // 🔹 Populate map dropdown
        mapsSnap.forEach(docSnap => {
            const mapData = docSnap.data();
            const mapId = docSnap.id;
            const option = document.createElement("option");
            option.value = mapId;
            option.textContent = `${mapData.map_name} (${mapId})`;
            mapSelect.appendChild(option);
        });

        if (mapsSnap.docs.length > 0) {
            // 🔹 Select the current active map by default
            const currentMapDoc = mapsSnap.docs.find(d => d.data().current_active_map === d.id) || mapsSnap.docs[0];
            const mapId = currentMapDoc.id;
            mapSelect.value = mapId;

            await populateCampuses(mapId, true);
            await populateVersions(mapId, true);
            await loadMap(mapId); // Load initial map view
        }

        // 🔹 When Map changes (view only)
        mapSelect.addEventListener("change", async () => {
            const selectedMapId = mapSelect.value;
            if (!selectedMapId) return;
            await populateCampuses(selectedMapId, false);
            await populateVersions(selectedMapId, false);
            await loadMap(selectedMapId); // Load map view for selected map
        });

    } catch (err) {
        console.error("Error loading maps:", err);
    }
}

async function populateCampuses(mapId, selectCurrent = true) {
    const campusSelect = document.getElementById("campusSelect");
    campusSelect.innerHTML = '<option value="">Select Campus</option>';

    const mapDocRef = doc(db, "MapVersions", mapId);
    const mapDocSnap = await getDoc(mapDocRef);
    if (!mapDocSnap.exists()) return;

    const mapData = mapDocSnap.data();
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

    const mapDocRef = doc(db, "MapVersions", mapId);
    const mapDocSnap = await getDoc(mapDocRef);
    if (!mapDocSnap.exists()) return;

    const mapData = mapDocSnap.data();
    const currentVersion = mapData.current_version || "";

    const versionsSnap = await getDocs(collection(db, "MapVersions", mapId, "versions"));

    versionsSnap.forEach(docSnap => {
        const version = docSnap.id;
        const option = document.createElement("option");
        option.value = version;
        option.textContent = version + (version === currentVersion ? " ✅" : "");
        versionSelect.appendChild(option);
    });

    if (selectCurrent && currentVersion) {
        versionSelect.value = currentVersion;
    } else if (versionsSnap.docs.length > 0) {
        versionSelect.value = versionsSnap.docs[0].id;
    }

    // 🔹 When version changes, only update view
    versionSelect.addEventListener("change", async () => {
        const selectedVersion = versionSelect.value;
        if (!selectedVersion) return;
        await loadMap(mapId, document.getElementById("campusSelect").value, selectedVersion); // View selected version
    });
}

// Call on page load
document.addEventListener("DOMContentLoaded", () => {
    populateMaps();
});










async function loadMap(mapId = null, campusId = null, versionId = null) {
  try {
    // ---- Get map versions ----
    const mapVersionsRef = collection(db, "MapVersions");
    const mapsSnapshot = await getDocs(mapVersionsRef);

    if (mapsSnapshot.empty) {
      console.error("No MapVersions found");
      return;
    }

    // Use provided mapId or fallback to first
    let mapDoc;
    if (mapId) {
      mapDoc = mapsSnapshot.docs.find(d => d.id === mapId);
    }
    if (!mapDoc) mapDoc = mapsSnapshot.docs[0];

    const mapDocId = mapDoc.id;
    const mapData = mapDoc.data();

    // Use provided versionId or current_version or fallback
    const currentVersion = versionId || mapData.current_version || "v1.0.0";

    // Fetch nodes from version
    const versionRef = doc(db, "MapVersions", mapDocId, "versions", currentVersion);
    const versionSnap = await getDoc(versionRef);
    if (!versionSnap.exists()) {
      console.error("Version not found:", currentVersion);
      return;
    }

    let nodes = versionSnap.data().nodes || [];
    const edges = versionSnap.data().edges || [];

    // Filter nodes by campusId if provided
    if (campusId) {
      nodes = nodes.filter(n => n.campus_id === campusId);
    }

    // Fetch Infra + Campus names
    const [infraSnap, campusSnap] = await Promise.all([
      getDocs(collection(db, "Infrastructure")),
      getDocs(collection(db, "Campus"))
    ]);

    const infraMap = {};
    infraSnap.forEach(doc => infraMap[doc.data().infra_id] = doc.data().name);

    const campusMap = {};
    campusSnap.forEach(doc => campusMap[doc.data().campus_id] = doc.data().campus_name);

    nodes.forEach(d => {
      d.infraName = d.related_infra_id ? (infraMap[d.related_infra_id] || d.related_infra_id) : "-";
      d.campusName = d.campus_id ? (campusMap[d.campus_id] || d.campus_id) : "-";
    });

    // Convert Lat/Lng to X/Y
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

    // ---- Render map ----
    const mapContainer = document.getElementById("map-overview");

    // Remove existing Leaflet map if any
    if (mapContainer._leaflet_id) {
      mapContainer._leaflet_id = null;
      mapContainer.innerHTML = "";
    }

    const map = L.map("map-overview", {
      center: [6.9130, 122.0630],
      zoom: 18,
      zoomControl: false,
      dragging: false,
      scrollWheelZoom: false,
      doubleClickZoom: false,
      boxZoom: false,
      keyboard: false
    });

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      attribution: "© OpenStreetMap contributors"
    }).addTo(map);

    renderDataOnMap(map, nodes);

    // Modal logic
    const modal = document.getElementById("mapModal");
    const closeBtn = document.getElementById("closeModal");

    mapContainer.addEventListener("click", () => {
      modal.style.display = "block";
      setTimeout(() => {
        const mapFull = L.map("map-full", { center: [6.9130, 122.0630], zoom: 18, zoomControl: true });
        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", { attribution: "© OpenStreetMap" }).addTo(mapFull);
        renderDataOnMap(mapFull, nodes, true);
      }, 200);
    });

    closeBtn.addEventListener("click", () => { modal.style.display = "none"; });

  } catch (err) {
    console.error("Error loading map data:", err);
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

// ---- Render barriers, buildings, rooms ----
function renderDataOnMap(map, data, enableClick = false) {
  window._activeMap = map; // keep reference for popups

  // --- Barriers (polygon + corner markers) ---
  const barrierNodes = data.filter(d => d.type === "barrier");

  if (barrierNodes.length > 0) {
    // 🔑 Step 1: Get centroid
    const centroid = barrierNodes.reduce(
      (acc, n) => {
        acc.lat += n.latitude;
        acc.lng += n.longitude;
        return acc;
      },
      { lat: 0, lng: 0 }
    );
    centroid.lat /= barrierNodes.length;
    centroid.lng /= barrierNodes.length;

    // 🔑 Step 2: Sort nodes by angle around centroid
    const sortedNodes = [...barrierNodes].sort((a, b) => {
      const angleA = Math.atan2(a.latitude - centroid.lat, a.longitude - centroid.lng);
      const angleB = Math.atan2(b.latitude - centroid.lat, b.longitude - centroid.lng);
      return angleA - angleB;
    });

    // 🔑 Step 3: Build ordered coordinates
    const barrierCoords = sortedNodes.map(b => [b.latitude, b.longitude]);

    // Draw polygon
    const polygon = L.polygon(barrierCoords, {
      color: "green",
      weight: 3,
      fillOpacity: 0.1
    }).addTo(map);

    if (enableClick) {
      polygon.on("click", (e) => {
        showDetails({
          name: "Campus Area",
          type: "Barrier Fence",
          latitude: e.latlng.lat.toFixed(6),
          longitude: e.latlng.lng.toFixed(6)
        });
      });

      // Corner markers
      sortedNodes.forEach(node => {
        const cornerMarker = L.circleMarker([node.latitude, node.longitude], {
          radius: 6,
          color: "darkgreen",
          fillColor: "lightgreen",
          fillOpacity: 0.9
        }).addTo(map);

        cornerMarker.on("click", () => showDetails({ ...node, _map: map }));
      });
    }
  }

  // --- Infrastructure (Buildings) ---
  data.filter(d => d.type === "infrastructure").forEach(building => {
    const marker = L.circleMarker([building.latitude, building.longitude], {
      radius: 8,
      color: "blue",
      fillColor: "lightblue",
      fillOpacity: 0.7
    }).addTo(map);

    if (enableClick) {
      marker.on("click", () => showDetails({ ...building, _map: map }));
    }
  });

  // --- Rooms ---
  data.filter(d => d.type === "room").forEach(room => {
    const marker = L.marker([room.latitude, room.longitude]).addTo(map);

    if (enableClick) {
      marker.on("click", () => showDetails({ ...room, _map: map }));
    }
  });
}

  

  document.addEventListener("DOMContentLoaded", loadMap);






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