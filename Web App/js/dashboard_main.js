  // ======================= FIREBASE SETUP ===========================
  import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
  import {
    getFirestore, collection, getDocs, query, orderBy, limit, doc, getDoc, updateDoc
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

        mapsSnap.forEach(docSnap => {
            const mapData = docSnap.data();
            const mapId = docSnap.id;
            const option = document.createElement("option");
            option.value = mapId;
            option.textContent = `${mapData.map_name} (${mapId})`;
            mapSelect.appendChild(option);
        });

        if (mapsSnap.docs.length > 0) {
            const firstMapId = mapsSnap.docs[0].id;
            mapSelect.value = firstMapId;
            await populateCampuses(firstMapId);
            await populateVersions(firstMapId);
        }

        // When Map changes
        mapSelect.addEventListener("change", async () => {
            const selectedMapId = mapSelect.value;
            if (!selectedMapId) return;
            await populateCampuses(selectedMapId);
            await populateVersions(selectedMapId);
        });

    } catch (err) {
        console.error("Error loading maps:", err);
    }
}

async function populateCampuses(mapId) {
    const campusSelect = document.getElementById("campusSelect");
    campusSelect.innerHTML = '<option value="">Select Campus</option>';

    const mapDocRef = doc(db, "MapVersions", mapId);
    const mapDocSnap = await getDoc(mapDocRef);
    if (!mapDocSnap.exists()) return;

    const mapData = mapDocSnap.data();
    const campuses = mapData.campus_included || [];

    campuses.forEach(campusId => {
        const option = document.createElement("option");
        option.value = campusId;
        option.textContent = campusId; // You can fetch the campus name if needed
        campusSelect.appendChild(option);
    });

    // Optional: select first campus by default
    if (campuses.length > 0) campusSelect.value = campuses[0];

    // When campus changes, you could filter nodes/edges tables here
    campusSelect.addEventListener("change", () => {
        const selectedCampus = campusSelect.value;
        // e.g., renderNodesTable(selectedCampus)
        // e.g., renderEdgesTable(selectedCampus)
    });
}

async function populateVersions(mapId) {
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

    // Set dropdown to current version
    if (currentVersion) versionSelect.value = currentVersion;

    versionSelect.addEventListener("change", async () => {
        const selectedVersion = versionSelect.value;
        if (!selectedVersion) return;

        try {
            await updateDoc(mapDocRef, { current_version: selectedVersion });
            alert(`Current version updated to ${selectedVersion}`);

            Array.from(versionSelect.options).forEach(opt => {
                opt.textContent = opt.value + (opt.value === selectedVersion ? " ✅" : "");
            });
        } catch (err) {
            console.error("Error updating current version:", err);
        }
    });
}

// Run on page load
document.addEventListener("DOMContentLoaded", () => {
    populateMaps();
});










  async function loadMap() {
    try {
      const [infraSnap, campusSnap] = await Promise.all([
        getDocs(collection(db, "Infrastructure")),
        getDocs(collection(db, "Campus"))
      ]);

      const infraMap = {};
      infraSnap.forEach(doc => infraMap[doc.data().infra_id] = doc.data().name);

      const campusMap = {};
      campusSnap.forEach(doc => campusMap[doc.data().campus_id] = doc.data().campus_name);

      // ---- Fetch Nodes ----
      const q = query(collection(db, "Nodes"), orderBy("created_at", "asc"));
      const querySnapshot = await getDocs(q);

      const nodes = [];
      querySnapshot.forEach(docSnap => {
        const d = docSnap.data();
        nodes.push({
          ...d,
          infraName: d.related_infra_id ? (infraMap[d.related_infra_id] || d.related_infra_id) : "-",
          campusName: d.campus_id ? (campusMap[d.campus_id] || d.campus_id) : "-"
        });
      });

      // ------------------ Convert Lat/Lng to X/Y ------------------
const origin = { lat: 6.913341, lng: 122.063693 }; // your point of origin

function latLngToXY(lat, lng, origin) {
  const R = 6371000; // Earth radius in meters
  const dLat = (lat - origin.lat) * Math.PI / 180;
  const dLng = (lng - origin.lng) * Math.PI / 180;

  const x = dLng * Math.cos(origin.lat * Math.PI / 180) * R;
  const y = dLat * R;

  return { x, y };
}

// Loop through buildings only
nodes
  .filter(n => n.type === "infrastructure")
  .forEach(building => {
    const { x, y } = latLngToXY(building.latitude, building.longitude, origin);
    console.log(`Building: ${building.name} → X: ${x.toFixed(2)}, Y: ${y.toFixed(2)}`);
    building.cartesian = { x, y }; // optional: store inside the node
  });

      // ---- Small overview map ----
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

      // ---- Modal logic ----
      const modal = document.getElementById("mapModal");
      const closeBtn = document.getElementById("closeModal");

      document.getElementById("map-overview").addEventListener("click", () => {
        modal.style.display = "block";

        setTimeout(() => {
          const mapFull = L.map("map-full", {
            center: [6.9130, 122.0630],
            zoom: 18,
            zoomControl: true
          });

          L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            attribution: "© OpenStreetMap"
          }).addTo(mapFull);

          renderDataOnMap(mapFull, nodes, true);
        }, 200);
      });

      closeBtn.addEventListener("click", () => {
        modal.style.display = "none";
      });

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
      .openOn(node._map || window._activeMap); // fallback if needed
  }

  // ---- Render barriers, buildings, rooms ----
  function renderDataOnMap(map, data, enableClick = false) {
    window._activeMap = map; // keep reference for popups

    // --- Barriers (polygon + corner markers) ---
    const barrierNodes = data.filter(d => d.type === "barrier");
    const barrierCoords = barrierNodes.map(b => [b.latitude, b.longitude]);

    if (barrierCoords.length > 0) {
      // Draw polygon
      const polygon = L.polygon(barrierCoords, {
        color: "green",
        weight: 3,
        fillOpacity: 0.1
      }).addTo(map);

      if (enableClick) {
        // Click inside polygon → campus info
        polygon.on("click", (e) => {
          showDetails({
            name: "WMSU Camp B",
            type: "Campus Area",
            latitude: e.latlng.lat.toFixed(6),
            longitude: e.latlng.lng.toFixed(6)
          });
        });

        // Corner markers
        barrierNodes.forEach(node => {
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