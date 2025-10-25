// ======================= FIREBASE SETUP ===========================
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
import {
    getFirestore, collection, addDoc, getDocs, query, orderBy, where, updateDoc, doc, getDoc, arrayUnion, writeBatch, deleteDoc, setDoc
} from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";

import { firebaseConfig } from "../firebaseConfig.js";


const app = initializeApp(firebaseConfig);
const db = getFirestore(app);


// ======================= NODE SECTION =============================

// ----------- Modal Controls -----------
function showNodeModal() {
    document.getElementById('addNodeModal').style.display = 'flex';
    generateNextNodeId();
    populateInfraDropdown();
    populateIndoorInfraDropdown(); 
    populateCampusDropdown();
}
function hideNodeModal() {
    document.getElementById('addNodeModal').style.display = 'none';
    document.getElementById("nodeName").value = "";
    document.getElementById("latitude").value = "";
    document.getElementById("longitude").value = "";
}
window.openNodeModal = showNodeModal;
window.closeNodeModal = hideNodeModal;

// ----------- Auto-Increment Node ID -----------
async function generateNextNodeId() {
    const mapSelect = document.getElementById("mapSelect");
    const mapId = mapSelect ? mapSelect.value : null;
    if (!mapId) return;

    // Get current version from MapVersions
    const mapDocRef = doc(db, "MapVersions", String(mapId));
    const mapDocSnap = await getDoc(mapDocRef);
    if (!mapDocSnap.exists()) return;

    const currentVersion = mapDocSnap.data().current_version || "v1.0.0";
    const versionRef = doc(db, "MapVersions", String(mapId), "versions", currentVersion);
    const versionSnap = await getDoc(versionRef);
    if (!versionSnap.exists()) return;

    const nodes = Array.isArray(versionSnap.data().nodes) ? versionSnap.data().nodes : [];

    let maxNum = 0;
    nodes.forEach(node => {
        if (node.node_id) {
            const num = parseInt(node.node_id.replace("ND-", ""));
            if (!isNaN(num) && num > maxNum) maxNum = num;
        }
    });

    const nextId = `ND-${String(maxNum + 1).padStart(3, "0")}`;
    document.getElementById("nodeId").value = nextId;
}

// ----------- Load Buildings Dropdown for Node Modal -----------
async function loadBuildingsDropdownForNode() {
    const buildingSelect = document.getElementById("linkedBuilding");
    if (!buildingSelect) return;

    buildingSelect.innerHTML = `<option value="">Select a building</option>`;

    try {
        const q = query(collection(db, "Buildings"), orderBy("createdAt", "asc"));
        const snapshot = await getDocs(q);

        snapshot.forEach(doc => {
            const data = doc.data();
            if (data.building_id && data.name) {
                const option = document.createElement("option");
                option.value = data.building_id;
                option.textContent = `${data.building_id} - ${data.name}`;
                buildingSelect.appendChild(option);
            }
        });
    } catch (err) {
        console.error("Error loading buildings into dropdown:", err);
    }
}

// ----------- Load Buildings Dropdown by Element ID -----------
async function loadBuildingsDropdownById(selectId) {
    const buildingSelect = document.getElementById(selectId);
    if (!buildingSelect) return;

    buildingSelect.innerHTML = `<option value="">Select a building</option>`;

    try {
        const q = query(collection(db, "Buildings"), orderBy("createdAt", "asc"));
        const snapshot = await getDocs(q);

        snapshot.forEach(docSnap => {
            const data = docSnap.data();
            if (data.building_id && data.name) {
                const option = document.createElement("option");
                option.value = data.building_id;
                option.textContent = `${data.building_id} - ${data.name}`;
                buildingSelect.appendChild(option);
            }
        });
    } catch (err) {
        console.error("Error loading buildings into dropdown:", err);
    }
}

// ----------- Populate Related Infrastructure Dropdown -----------
async function populateInfraDropdown(selectId = "relatedInfra") {
    const select = document.getElementById(selectId);
    if (!select) return;
    select.innerHTML = `<option value="">Select infrastructure</option>`;

    const q = query(collection(db, "Infrastructure"));
    const snapshot = await getDocs(q);

    // Collect all infrastructures into an array
    const infraList = [];
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.infra_id && data.name) {
            infraList.push({
                id: data.infra_id,
                name: data.name
            });
        }
    });

    // Sort A-Z by name
    infraList.sort((a, b) => a.name.localeCompare(b.name));

    // Append sorted options
    infraList.forEach(infra => {
        const option = document.createElement("option");
        option.value = infra.id;
        option.textContent = infra.name;
        select.appendChild(option);
    });
}


// ----------- Populate Related Room Dropdown -----------
async function populateRoomDropdown(selectId = "relatedRoom") {
    const select = document.getElementById(selectId);
    if (!select) return;
    select.innerHTML = `<option value="">Select room</option>`;
    const q = query(collection(db, "Rooms"));
    const snapshot = await getDocs(q);
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.room_id && data.name) {
            const option = document.createElement("option");
            option.value = data.room_id;
            option.textContent = data.name;
            select.appendChild(option);
        }
    });
}

// ----------- Populate Campus Dropdown -----------
async function populateCampusDropdown(selectId = "campusDropdown") {
    const select = document.getElementById(selectId);
    if (!select) return;
    select.innerHTML = `<option value="">Select campus</option>`;
    const q = query(collection(db, "Campus"));
    const snapshot = await getDocs(q);
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.campus_id && data.campus_name) {
            const option = document.createElement("option");
            option.value = data.campus_id;
            option.textContent = data.campus_name;
            select.appendChild(option);
        }
    });
}







// ----------- Load Nodes Table with Spinner -----------
async function renderNodesTable() {
    const tbody = document.querySelector(".nodetbl tbody");
    if (!tbody) return;

    // 🔄 Show loading row with spinner
    tbody.innerHTML = `
        <tr class="loading-row">
            <td colspan="9" style="text-align:center; padding:20px;">
                <div class="spinner"></div>
                <span class="loading-text">Loading nodes...</span>
            </td>
        </tr>
    `;

    try {
        let nodes = [];
        let infra = [];
        let rooms = [];
        let indoorInfras = [];
        let campuses = [];

        if (navigator.onLine) {
            // 🔹 Online: Firestore
            const [infraSnap, roomSnap, indoorSnap, campusSnap, mapVersionsSnap] = await Promise.all([
                getDocs(collection(db, "Infrastructure")),
                getDocs(collection(db, "Rooms")),
                getDocs(collection(db, "IndoorInfrastructure")),
                getDocs(collection(db, "Campus")),
                getDocs(collection(db, "MapVersions"))
            ]);

            infra = infraSnap.docs.map(d => d.data());
            rooms = roomSnap.docs.map(d => d.data());
            indoorInfras = indoorSnap.docs.map(d => d.data());
            campuses = campusSnap.docs.map(d => d.data());

            for (const mapDoc of mapVersionsSnap.docs) {
                const mapData = mapDoc.data();
                const currentCampus = mapData.current_active_campus;
                const currentVersion = mapData.current_version;
                if (!currentCampus || !currentVersion) continue;

                const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
                const versionSnap = await getDoc(versionRef);
                if (!versionSnap.exists()) continue;

                const versionData = versionSnap.data();
                const mapNodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
                nodes.push(...mapNodes.filter(n => !n.is_deleted && n.campus_id === currentCampus));
            }
        } else {
            // 🔹 Offline fallback
            const [nodesRes, infraRes, roomsRes, indoorRes, campusesRes] = await Promise.all([
                fetch("../assets/firestore/MapVersions.json"),
                fetch("../assets/firestore/Infrastructure.json"),
                fetch("../assets/firestore/Rooms.json"),
                fetch("../assets/firestore/IndoorInfrastructure.json"),
                fetch("../assets/firestore/Campus.json")
            ]);

            const mapVersions = await nodesRes.json();
            infra = (await infraRes.json()).filter(i => !i.is_deleted);
            rooms = (await roomsRes.json()).filter(r => !r.is_deleted);
            indoorInfras = (await indoorRes.json()).filter(r => !r.is_deleted);
            campuses = (await campusesRes.json()).filter(c => !c.is_deleted);

            for (const mapData of mapVersions) {
                const currentCampus = mapData.current_active_campus;
                const currentVersion = mapData.current_version;
                if (!currentCampus || !currentVersion) continue;

                const version = mapData.versions.find(v => v.id === currentVersion);
                if (!version) continue;

                const mapNodes = Array.isArray(version.nodes) ? version.nodes : [];
                nodes.push(...mapNodes.filter(n => !n.is_deleted && n.campus_id === currentCampus));
            }
        }

        // 🔹 Build lookup maps
        const infraMap = Object.fromEntries(infra.map(i => [i.infra_id, i.name]));
        const roomMap = Object.fromEntries(rooms.map(r => [r.room_id, r.name]));
        const indoorInfraMap = Object.fromEntries(indoorInfras.map(r => [r.room_id, r.name]));
        const campusMap = Object.fromEntries(campuses.map(c => [c.campus_id, c.campus_name]));

        // 🔹 Sort nodes
        nodes.sort((a, b) => (a.created_at?.seconds || 0) - (b.created_at?.seconds || 0));

        // 🔹 Clear loading row and render nodes
        tbody.innerHTML = "";
        nodes.forEach(data => {
            const coords = (data.latitude && data.longitude) ? `${data.latitude}, ${data.longitude}` : "-";
            const infraName = data.related_infra_id ? (infraMap[data.related_infra_id] || data.related_infra_id) : "-";

            let roomName = "-";
            if (data.related_room_id) roomName = indoorInfraMap[data.related_room_id] || roomMap[data.related_room_id] || data.related_room_id;

            const campusName = data.campus_id ? (campusMap[data.campus_id] || data.campus_id) : "-";

            let indoorOutdoor = "Outdoor";
            if (data.indoor) {
                indoorOutdoor = `Indoor (Floor: ${data.indoor.floor ?? "-"}, X: ${data.indoor.x ?? "-"}, Y: ${data.indoor.y ?? "-"})`;
            }

            const type = data.type ? data.type.charAt(0).toUpperCase() + data.type.slice(1) : "-";

            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${data.node_id}</td>
                <td>${data.name}</td>
                <td>${type}</td>
                <td>${coords}</td>
                <td>${infraName}</td>
                <td>${roomName}</td>
                <td>${indoorOutdoor}</td>
                <td>${campusName}</td>
                <td class="actions">
                    <i class="fas fa-edit"></i>
                    <i class="fas fa-trash" data-node-id="${data.node_id}"></i>
                </td>
            `;
            tbody.appendChild(tr);
        });

        setupNodeDeleteHandlers();
    } catch (err) {
        console.error("Error loading nodes:", err);
        tbody.innerHTML = `<tr><td colspan="9" style="text-align:center; color:red;">Error loading nodes</td></tr>`;
    }
}










// SHOWS ALL THW NODES FROM VERSIONS


// // ----------- Load Nodes Table -----------
// async function renderNodesTable() {
//     const tbody = document.querySelector(".nodetbl tbody");
//     if (!tbody) return;
//     tbody.innerHTML = "";

//     try {
//         // Fetch all needed collections for mapping IDs to names
//         const [infraSnap, roomSnap, campusSnap] = await Promise.all([
//             getDocs(collection(db, "Infrastructure")),
//             getDocs(collection(db, "Rooms")),
//             getDocs(collection(db, "Campus"))
//         ]);

//         // Build lookup maps
//         const infraMap = {};
//         infraSnap.forEach(doc => {
//             const d = doc.data();
//             infraMap[d.infra_id] = d.name;
//         });

//         const roomMap = {};
//         roomSnap.forEach(doc => {
//             const d = doc.data();
//             roomMap[d.room_id] = d.name;
//         });

//         const campusMap = {};
//         campusSnap.forEach(doc => {
//             const d = doc.data();
//             campusMap[d.campus_id] = d.campus_name;
//         });

//         // Fetch all maps
//         const mapsSnap = await getDocs(collection(db, "MapVersions"));

//         for (const mapDoc of mapsSnap.docs) {
//             const mapData = mapDoc.data();
//             const currentVersion = mapData.current_version || "v1.0.0";

//             const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
//             const versionSnap = await getDoc(versionRef);

//             if (!versionSnap.exists()) continue;

//             const { nodes = [] } = versionSnap.data();

//             nodes.forEach(node => {
//                 // Coordinates
//                 const coords = (node.latitude && node.longitude) ? `${node.latitude}, ${node.longitude}` : "-";

//                 // Related infra/room names
//                 const infraName = node.related_infra_id ? (infraMap[node.related_infra_id] || node.related_infra_id) : "-";
//                 const roomName = node.related_room_id ? (roomMap[node.related_room_id] || node.related_room_id) : "-";

//                 // Indoor/Outdoor
//                 let indoorOutdoor = "Outdoor";
//                 if (node.indoor) {
//                     indoorOutdoor = `Indoor (Floor: ${node.indoor.floor || "-"}, X: ${node.indoor.x || "-"}, Y: ${node.indoor.y || "-"})`;
//                 }

//                 // Campus name
//                 const campusName = node.campus_id ? (campusMap[node.campus_id] || node.campus_id) : "-";

//                 // Type
//                 const type = node.type ? node.type.charAt(0).toUpperCase() + node.type.slice(1) : "-";

//                 const tr = document.createElement("tr");
//                 tr.innerHTML = `
//                     <td>${node.node_id}</td>
//                     <td>${node.name}</td>
//                     <td>${type}</td>
//                     <td>${coords}</td>
//                     <td>${infraName}</td>
//                     <td>${roomName}</td>
//                     <td>${indoorOutdoor}</td>
//                     <td>${campusName}</td>
//                     <td class="actions">
//                         <i class="fas fa-edit"></i>
//                         <i class="fas fa-trash"></i>
//                     </td>
//                 `;
//                 tbody.appendChild(tr);
//             });
//         }

//     } catch (err) {
//         console.error("Error loading nodes: ", err);
//     }
// }












// ----------- Type Dropdown/Input Switch -----------
document.addEventListener("DOMContentLoaded", function() {
    const typeSelect = document.getElementById("nodeType");
    let typeInput = null;

    typeSelect.addEventListener("change", function() {
        if (this.value === "other") {
            typeInput = document.createElement("input");
            typeInput.type = "text";
            typeInput.id = "nodeType";
            typeInput.placeholder = "Enter type";
            typeInput.classList.add("custom-input");
            this.parentNode.replaceChild(typeInput, this);

            typeInput.addEventListener("blur", function() {
                if (typeInput.value.trim() === "") {
                    typeInput.parentNode.replaceChild(typeSelect, typeInput);
                    typeSelect.value = "";
                }
            });
        }
    });
});











// ----------- Populate Related Indoor Infrastructure Dropdown -----------
async function populateIndoorInfraDropdown(selectId = "relatedIndoorInfra") {
    const select = document.getElementById(selectId);
    if (!select) return;
    select.innerHTML = `<option value="">Select Indoor Infra</option>`;

    try {
        const q = query(collection(db, "IndoorInfrastructure"));
        const snapshot = await getDocs(q);

        // Collect into array then sort by name A-Z
        const indoorList = [];
        snapshot.forEach(docSnap => {
            const data = docSnap.data();
            if (data.room_id && data.name) {
                indoorList.push({ id: data.room_id, name: data.name });
            }
        });

        indoorList.sort((a, b) => a.name.localeCompare(b.name));

        // Append sorted options
        indoorList.forEach(item => {
            const option = document.createElement("option");
            option.value = item.id;
            option.textContent = item.name;
            select.appendChild(option);
        });
    } catch (err) {
        console.error("Error loading indoor infrastructure into dropdown:", err);
    }
}









document.addEventListener("DOMContentLoaded", function() {
    const nodeTypeSelect = document.getElementById("nodeType");
    const relatedInfraSelect = document.getElementById("relatedInfra");
    const relatedIndoorInfraSelect = document.getElementById("relatedIndoorInfra");
    const indoorDetails = document.getElementById("indoorDetails");
    const coordinatesBlock = Array.from(document.querySelectorAll(".form-group"))
        .find(group => group.querySelector("label")?.textContent.trim() === "Coordinates");

    // ✅ Reset node type to default ("Select type") on load
    nodeTypeSelect.value = "";

    // Hide indoor details by default
    indoorDetails.style.display = "none";

    nodeTypeSelect.addEventListener("change", function() {
        const type = this.value;

        if (type === "indoorInfra") {
            relatedInfraSelect.disabled = true;
            relatedInfraSelect.classList.add("disabled");
            relatedIndoorInfraSelect.disabled = false;
            relatedIndoorInfraSelect.classList.remove("disabled");
            indoorDetails.style.display = "block"; // Show indoor details

            // ✅ Hide entire coordinates block
            if (coordinatesBlock) coordinatesBlock.style.display = "none";

        } else if (type === "infrastructure") {
            relatedInfraSelect.disabled = false;
            relatedInfraSelect.classList.remove("disabled");
            relatedIndoorInfraSelect.disabled = true;
            relatedIndoorInfraSelect.classList.add("disabled");
            indoorDetails.style.display = "none"; // Hide indoor details

            // ✅ Show coordinates block again
            if (coordinatesBlock) coordinatesBlock.style.display = "";
            
        } else if (type === "intermediate" || type === "barrier") {
            relatedInfraSelect.disabled = true;
            relatedInfraSelect.classList.add("disabled");
            relatedIndoorInfraSelect.disabled = true;
            relatedIndoorInfraSelect.classList.add("disabled");
            indoorDetails.style.display = "none"; // Hide indoor details

            // ✅ Show coordinates block again
            if (coordinatesBlock) coordinatesBlock.style.display = "";

        } else {
            relatedInfraSelect.disabled = false;
            relatedInfraSelect.classList.remove("disabled");
            relatedIndoorInfraSelect.disabled = false;
            relatedIndoorInfraSelect.classList.remove("disabled");
            indoorDetails.style.display = "none";

            // ✅ Show coordinates block again
            if (coordinatesBlock) coordinatesBlock.style.display = "";
        }
    });
});








let pendingNodeData = null; 

document.getElementById("nodeForm").addEventListener("submit", async (e) => {
    e.preventDefault();

    // --- Helper functions ---
    const parseNumberOrNull = (val) => {
        const num = parseFloat(val);
        return isNaN(num) ? null : num;
    };

    const stringOrNull = (val) => val && val.trim() !== "" ? val : null;

    // --- Collect values ---
    const nodeId = document.getElementById("nodeId").value;
    const nodeName = stringOrNull(document.getElementById("nodeName").value);
    const latitude = parseNumberOrNull(document.getElementById("latitude").value);
    const longitude = parseNumberOrNull(document.getElementById("longitude").value);
    const typeEl = document.getElementById("nodeType");
    const typeValue = typeEl ? typeEl.value : "";
    const relatedInfraId = stringOrNull(document.getElementById("relatedInfra").value);
    const relatedIndoorInfraId = stringOrNull(document.getElementById("relatedIndoorInfra").value);
    const campusId = stringOrNull(document.getElementById("campusDropdown").value);

    let type = null;
    let indoor = null;

    if (typeValue === "indoorInfra") {
        type = "indoorinfra";
        indoor = {
            floor: stringOrNull(document.getElementById("floor").value),
            x: parseNumberOrNull(document.getElementById("xCoord").value),
            y: parseNumberOrNull(document.getElementById("yCoord").value)
        };

        if (!indoor.floor && indoor.x === null && indoor.y === null) {
            indoor = null;
        }
    } else if (typeValue === "infrastructure") {
        type = "infrastructure";
        indoor = null;
    } else if (typeValue === "barrier") {
        type = "barrier";
        indoor = null;
    } else if (typeValue === "intermediate") {
        type = "intermediate";
        indoor = null;
    }


    // Cartesian conversion only if lat/lng are valid
    let xCoord = null, yCoord = null;
    if (latitude !== null && longitude !== null) {
        const origin = { lat: 6.913341, lng: 122.063693 };
        function latLngToXY(lat, lng, origin) {
            const R = 6371000;
            const dLat = (lat - origin.lat) * Math.PI / 180;
            const dLng = (lng - origin.lng) * Math.PI / 180;
            const x = dLng * Math.cos(origin.lat * Math.PI / 180) * R;
            const y = dLat * R;
            return { x, y };
        }
        const { x, y } = latLngToXY(latitude, longitude, origin);
        xCoord = x;
        yCoord = y;
    }

    // --- Build clean data object ---
    pendingNodeData = {
        node_id: nodeId,
        name: nodeName,
        latitude,
        longitude,
        x_coordinate: xCoord,
        y_coordinate: yCoord,
        type,
        related_infra_id: relatedInfraId,
        related_room_id: relatedIndoorInfraId, // Use indoor infra as related_room_id
        indoor,
        is_active: true,
        campus_id: campusId,
        created_at: new Date()
    };

    document.getElementById("nodeSaveModal").style.display = "flex";
});

// ----------- Modal Buttons -----------
document.getElementById("closeNodeSaveModal").addEventListener("click", () => {
    pendingNodeData = null;
    document.getElementById("nodeSaveModal").style.display = "none";
});

document.getElementById("overwriteNodeBtn").addEventListener("click", async () => {
    await saveNode("overwrite");
    pendingNodeData = null;
    document.getElementById("nodeSaveModal").style.display = "none";
});

document.getElementById("newVersionNodeBtn").addEventListener("click", async () => {
    await saveNode("newVersion");
    pendingNodeData = null;
    document.getElementById("nodeSaveModal").style.display = "none";
});

async function saveNode(option) {
    if (!pendingNodeData) return;

    try {
        const campusId = pendingNodeData.campus_id;

        // --- Get the map that includes this campus ---
        const mapsQuery = query(
            collection(db, "MapVersions"),
            where("campus_included", "array-contains", campusId)
        );
        const mapsSnapshot = await getDocs(mapsQuery);

        if (mapsSnapshot.empty) {
            alert("No map found for this campus.");
            return;
        }

        const mapDoc = mapsSnapshot.docs[0];
        const mapDocId = mapDoc.id;
        const mapData = mapDoc.data();
        const currentVersion = mapData.current_version || "v1.0.0";

        // --- Reference the current version document ---
        const versionRef = doc(db, "MapVersions", mapDocId, "versions", currentVersion);
        const versionSnap = await getDoc(versionRef);

        let oldNodes = [];
        let oldEdges = [];

        if (versionSnap.exists()) {
            const versionData = versionSnap.data();
            oldNodes = versionData.nodes || [];
            oldEdges = versionData.edges || [];
        }

        if (option === "overwrite") {
            const updatedNodes = oldNodes.filter(n => n.node_id !== pendingNodeData.node_id);
            updatedNodes.push(pendingNodeData);

            await updateDoc(versionRef, { nodes: updatedNodes });

            alert(`Node ${pendingNodeData.node_id} added/updated in current version ${currentVersion}`);
        } 
        else if (option === "newVersion") {
            let [major, minor, patch] = currentVersion.slice(1).split(".").map(Number);
            if (patch < 99) patch += 1;
            else { patch = 0; minor += 1; }

            const newVersion = `v${major}.${minor}.${patch}`;
            const migratedNodes = oldNodes.map(n => ({ ...n }));
            migratedNodes.push({ ...pendingNodeData });
            const migratedEdges = oldEdges.map(e => ({ ...e }));

            await setDoc(doc(db, "MapVersions", mapDocId, "versions", newVersion), {
                nodes: migratedNodes,
                edges: migratedEdges
            });

            await updateDoc(doc(db, "MapVersions", mapDocId), { 
                current_version: newVersion,
                current_version_update: true,
            });

            alert(`New version created: ${newVersion}`);
        }

        // ✅ Update StaticDataVersions/GlobalInfo
        const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
        await updateDoc(staticDataRef, { infrastructure_updated: true });

        // ✅ NEW FEATURE: Calculate and save map center (all campuses combined)
        const allCampuses = mapData.campus_included || [];

        let allNodes = [];
        for (const campId of allCampuses) {
            const versionQuery = query(
                collection(db, "MapVersions", mapDocId, "versions")
            );
            const versionDocs = await getDocs(versionQuery);
            
            versionDocs.forEach(vDoc => {
                const vData = vDoc.data();
                const campusNodes = (vData.nodes || []).filter(n => n.campus_id === campId);
                allNodes.push(...campusNodes);
            });
        }

        // Compute geographic center from all nodes
        const getGeographicCenter = (nodes) => {
            if (!nodes.length) return [6.9130, 122.0630];
            let x = 0, y = 0, z = 0;
            nodes.forEach(n => {
                if (!n.latitude || !n.longitude) return;
                const latRad = parseFloat(n.latitude) * Math.PI / 180;
                const lonRad = parseFloat(n.longitude) * Math.PI / 180;
                x += Math.cos(latRad) * Math.cos(lonRad);
                y += Math.cos(latRad) * Math.sin(lonRad);
                z += Math.sin(latRad);
            });
            const total = nodes.length;
            x /= total; y /= total; z /= total;
            const lon = Math.atan2(y, x);
            const hyp = Math.sqrt(x * x + y * y);
            const lat = Math.atan2(z, hyp);
            return [lat * 180 / Math.PI, lon * 180 / Math.PI];
        };

        const [centerLat, centerLng] = getGeographicCenter(allNodes);

        // ✅ Update the Maps collection with the new center
        const mapsCollection = collection(db, "Maps");
        const mapsQueryRef = query(mapsCollection, where("map_id", "==", mapDocId));
        const mapsDocs = await getDocs(mapsQueryRef);

        if (!mapsDocs.empty) {
            const mapRef = mapsDocs.docs[0].ref;
            await updateDoc(mapRef, {
                center_lat: centerLat,
                center_lng: centerLng,
                updatedAt: new Date()
            });
            console.log(`Map center updated for ${mapDocId}:`, centerLat, centerLng);
        }

        // --- Reset UI ---
        document.getElementById("nodeForm").reset();
        document.getElementById("indoorDetails").style.display = "none";
        generateNextNodeId();
        renderNodesTable();
        pendingNodeData = null;
        document.getElementById("nodeSaveModal").style.display = "none";

    } catch (err) {
        console.error(err);
        alert("Error saving node: " + err);
    }
}



async function handleIndoorInfraSelection({
  selectId = "relatedIndoorInfra",
  nameInputId = "nodeName",
  relatedInfraSelectId = "relatedInfra",
  campusSelectId = "campusDropdown"
} = {}) {
  const select = document.getElementById(selectId);
  if (!select) return;

  select.addEventListener("change", async () => {
    const roomId = select.value;
    if (!roomId) return;

    // helper to set input value only when user hasn't typed a custom value
    const nameInput = document.getElementById(nameInputId);
    const setNameIfAllowed = (val) => {
      if (!nameInput) return;
      // If empty OR previously auto-filled, overwrite. If user edited, don't overwrite.
      const wasAuto = nameInput.dataset.autofilled === "true";
      if (!nameInput.value || wasAuto) {
        nameInput.value = val || "";
        nameInput.dataset.autofilled = val ? "true" : "false";
      }
      // Clear autofill flag when user types
      if (!nameInput._autofillListenerAdded) {
        nameInput.addEventListener("input", () => {
          if (nameInput.dataset.autofilled === "true") nameInput.dataset.autofilled = "false";
        });
        nameInput._autofillListenerAdded = true;
      }
    };

    const relatedInfraSelect = document.getElementById(relatedInfraSelectId);
    const campusSelect = document.getElementById(campusSelectId);

    try {
      // Try online Firestore first
      if (navigator.onLine) {
        // Find indoor infra doc by room_id
        const q = query(collection(db, "IndoorInfrastructure"), where("room_id", "==", roomId));
        const snap = await getDocs(q);
        let indoorDocData = null;
        if (!snap.empty) {
          indoorDocData = snap.docs[0].data();
        } else {
          // fallback: try Rooms collection (some setups store rooms there)
          const rq = query(collection(db, "Rooms"), where("room_id", "==", roomId));
          const rSnap = await getDocs(rq);
          if (!rSnap.empty) indoorDocData = rSnap.docs[0].data();
        }

        if (indoorDocData) {
          // Prefill name
          setNameIfAllowed(indoorDocData.name || indoorDocData.room_name || "");

          // Prefill related infrastructure dropdown if infra_id exists
          if (indoorDocData.infra_id && relatedInfraSelect) {
            // if option exists, set directly; otherwise, try to populate first then set
            const optExists = Array.from(relatedInfraSelect.options).some(o => o.value === indoorDocData.infra_id);
            if (optExists) {
              relatedInfraSelect.value = indoorDocData.infra_id;
            } else {
              // try to populate infra dropdown then set (best-effort)
              try { await populateInfraDropdown(relatedInfraSelectId); relatedInfraSelect.value = indoorDocData.infra_id; } catch (e) { /* ignore */ }
            }
          }

          // Attempt to find campus via Rooms or Infrastructure (best-effort)
          let campusId = null;

          // 1) Check Rooms doc for campus_id
          try {
            const roomsQ = query(collection(db, "Rooms"), where("room_id", "==", roomId));
            const roomsSnap = await getDocs(roomsQ);
            if (!roomsSnap.empty) {
              const r = roomsSnap.docs[0].data();
              if (r.campus_id) campusId = r.campus_id;
            }
          } catch (e) { /* ignore */ }

          // 2) If not found, check IndoorInfrastructure doc for campus_id
          if (!campusId && indoorDocData.campus_id) campusId = indoorDocData.campus_id;

          // 3) If still not found, look up the Infrastructure doc for a campus reference
          if (!campusId && indoorDocData.infra_id) {
            try {
              const infraQ = query(collection(db, "Infrastructure"), where("infra_id", "==", indoorDocData.infra_id));
              const infraSnap = await getDocs(infraQ);
              if (!infraSnap.empty) {
                const infraData = infraSnap.docs[0].data();
                if (infraData.campus_id) campusId = infraData.campus_id;
              }
            } catch (e) { /* ignore */ }
          }

          // Set campus dropdown if we found one
          if (campusId && campusSelect) {
            // ensure option exists; if not, try to populate then set
            const optExists = Array.from(campusSelect.options).some(o => o.value === campusId);
            if (optExists) campusSelect.value = campusId;
            else {
              try { await populateCampusDropdown(campusSelectId); campusSelect.value = campusId; } catch (e) { /* ignore */ }
            }
          }
        }
      } else {
        // OFFLINE — use local JSON fallback
        const [indoorRes, roomsRes, infraRes] = await Promise.all([
          fetch("../assets/firestore/IndoorInfrastructure.json").then(r => r.json()),
          fetch("../assets/firestore/Rooms.json").then(r => r.json()),
          fetch("../assets/firestore/Infrastructure.json").then(r => r.json())
        ]);

        const indoorDoc = (indoorRes || []).find(x => x.room_id === roomId) || (roomsRes || []).find(x => x.room_id === roomId);
        if (!indoorDoc) return;

        setNameIfAllowed(indoorDoc.name || indoorDoc.room_name || "");

        if (indoorDoc.infra_id && relatedInfraSelect) {
          const optExists = Array.from(relatedInfraSelect.options).some(o => o.value === indoorDoc.infra_id);
          if (optExists) relatedInfraSelect.value = indoorDoc.infra_id;
        }

        // try to find campus via rooms or infra JSON
        let campusId = (roomsRes || []).find(r => r.room_id === roomId)?.campus_id;
        if (!campusId && indoorDoc.infra_id) campusId = (infraRes || []).find(i => i.infra_id === indoorDoc.infra_id)?.campus_id;
        if (campusId && campusSelect) {
          const optExists = Array.from(campusSelect.options).some(o => o.value === campusId);
          if (optExists) campusSelect.value = campusId;
        }
      }
    } catch (err) {
      console.error("Error prefilling from indoor infra selection:", err);
    }
  });
}

// Attach to add + edit selects (if present)
handleIndoorInfraSelection({
  selectId: "relatedIndoorInfra",
  nameInputId: "nodeName",
  relatedInfraSelectId: "relatedInfra",
  campusSelectId: "campusDropdown"
});
handleIndoorInfraSelection({
  selectId: "editRelatedIndoorInfra",
  nameInputId: "editNodeName",
  relatedInfraSelectId: "editRelatedInfra",
  campusSelectId: "editCampusDropdown"
});







// ----------- Edit Node Modal Open Handler (Fixed & Updated for Related Indoor Infra) -----------
document.querySelector(".nodetbl").addEventListener("click", async (e) => {
  if (!e.target.classList.contains("fa-edit")) return;

  const row = e.target.closest("tr");
  if (!row) return;

  const nodeId = row.querySelector("td")?.textContent?.trim();
  if (!nodeId) return;

  try {
    // 🔹 STEP 1: Find node from MapVersions
    const mapVersionsSnap = await getDocs(collection(db, "MapVersions"));
    let nodeData = null;
    let versionRef = null;

    for (const mapDoc of mapVersionsSnap.docs) {
      const mapData = mapDoc.data();
      const currentVersion = mapData.current_version;
      if (!currentVersion) continue;

      const versionDocRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
      const versionSnap = await getDoc(versionDocRef);
      if (!versionSnap.exists()) continue;

      const versionData = versionSnap.data();
      const nodeFound = versionData.nodes?.find((n) => n.node_id === nodeId);
      if (nodeFound) {
        nodeData = nodeFound;
        versionRef = versionDocRef;
        break;
      }
    }

    if (!nodeData) {
      alert("Node not found in the current map versions.");
      return;
    }

    // 🔹 STEP 2: Populate dropdowns
    await populateInfraDropdown("editRelatedInfra");
    document.getElementById("editRelatedInfra").value = nodeData.related_infra_id ?? "";

    await populateIndoorInfraDropdown("editRelatedIndoorInfra");
    document.getElementById("editRelatedIndoorInfra").value = nodeData.related_room_id ?? "";

    await populateCampusDropdown("editCampusDropdown");
    document.getElementById("editCampusDropdown").value = nodeData.campus_id ?? "";

    // 🔹 STEP 3: Populate form fields
    document.getElementById("editNodeId").value = nodeData.node_id ?? "";
    document.getElementById("editNodeIdHidden").value = nodeData.node_id ?? "";
    document.getElementById("editNodeName").value = nodeData.name ?? "";
    document.getElementById("editLatitude").value = nodeData.latitude ?? "";
    document.getElementById("editLongitude").value = nodeData.longitude ?? "";

    // 🔹 STEP 4: Type Handling (Fixed + Dropdown Logic)
    let typeSelect = document.getElementById("editNodeType");
    let relatedInfraSelect = document.getElementById("editRelatedInfra");
    let relatedIndoorSelect = document.getElementById("editRelatedIndoorInfra");

    let typeValue = nodeData.type;
    if (typeValue === "indoor") typeValue = "indoorInfra";

    // Ensure it's a select element
    if (typeSelect.tagName !== "SELECT") {
      const parent = typeSelect.parentNode;
      const newSelect = document.createElement("select");
      newSelect.id = "editNodeType";
      newSelect.innerHTML = `
        <option value="">Select type</option>
        <option value="infrastructure">Infrastructure</option>
        <option value="indoorInfra">Indoor Infrastructure</option>
        <option value="barrier">Barrier</option>
        <option value="intermediate">Intermediate</option>
      `;
      parent.replaceChild(newSelect, typeSelect);
      typeSelect = newSelect;
    }

    // Set dropdown value
    if (["infrastructure", "indoorInfra", "barrier", "intermediate"].includes(typeValue)) {
      typeSelect.value = typeValue;
    } else {
      typeSelect.value = "";
    }

    // ----------------------------------------------------------------
// Find the coordinates and indoor details blocks
const coordinatesBlock = document.getElementById("coordinatesGroup");
const indoorDetails = document.getElementById("editIndoorDetails");

// ----------------------------------------------------------------
// STEP 5: Disable/Enable dropdowns + toggle visibility
function updateDropdownStates(selectedType) {
  // Reset dropdown states first
  relatedInfraSelect.disabled = false;
  relatedIndoorSelect.disabled = false;

  switch (selectedType) {
    case "infrastructure":
    case "barrier":
      relatedIndoorSelect.disabled = true;
      relatedInfraSelect.disabled = false;
      // Show coordinates, hide indoor
      if (coordinatesBlock) coordinatesBlock.style.display = "block";
      if (indoorDetails) indoorDetails.style.display = "none";
      break;

    case "intermediate":
      relatedInfraSelect.disabled = true;
      relatedIndoorSelect.disabled = true;
      // Show coordinates, hide indoor
      if (coordinatesBlock) coordinatesBlock.style.display = "block";
      if (indoorDetails) indoorDetails.style.display = "none";
      break;

    case "indoorInfra":
      relatedInfraSelect.disabled = true;
      relatedIndoorSelect.disabled = false;
      // ✅ Hide coordinates, show indoor details
      if (coordinatesBlock) coordinatesBlock.style.display = "none";
      if (indoorDetails) indoorDetails.style.display = "block";
      break;

    default:
      relatedInfraSelect.disabled = false;
      relatedIndoorSelect.disabled = false;
      // Show coordinates, hide indoor
      if (coordinatesBlock) coordinatesBlock.style.display = "block";
      if (indoorDetails) indoorDetails.style.display = "none";
      break;
  }
}

// Run on modal open (for existing node)
updateDropdownStates(typeSelect.value);

// Run again when user changes the type (if ever enabled)
typeSelect.addEventListener("change", (e) => {
  updateDropdownStates(e.target.value);
});


    if (nodeData.indoor || nodeData.type === "indoorInfra") {
      indoorDetails.style.display = "block";
      document.getElementById("editFloor").value = nodeData.indoor?.floor ?? "";
      document.getElementById("editXCoord").value = nodeData.indoor?.x ?? "";
      document.getElementById("editYCoord").value = nodeData.indoor?.y ?? "";
    } else {
      indoorDetails.style.display = "none";
      document.getElementById("editFloor").value = "";
      document.getElementById("editXCoord").value = "";
      document.getElementById("editYCoord").value = "";
    }

    // 🔹 STEP 7: Campus Dropdown
    document.getElementById("editCampusDropdown").value = nodeData.campus_id ?? "";

    // 🔹 STEP 8: Show modal
    document.getElementById("editNodeModal").style.display = "flex";
    document.getElementById("editNodeForm").dataset.mapVersionRef = versionRef.path;
  } catch (err) {
    console.error("Error opening edit modal:", err);
    alert("Failed to open edit modal. Check console for details.");
  }
});







// ----------- Edit Node Save Handler (Fixed for Indoor Infra + Field IDs) -----------
document.getElementById("editNodeForm").addEventListener("submit", async (e) => {
  e.preventDefault();

  const form = e.target;
  const versionRefPath = form.dataset.mapVersionRef; // stored earlier
  if (!versionRefPath) {
    alert("No map version reference found for update.");
    return;
  }

  const nodeId = document.getElementById("editNodeIdHidden").value.trim();
  const nodeName = document.getElementById("editNodeName").value.trim();
  const type = document.getElementById("editNodeType").value;
  const relatedInfraId = document.getElementById("editRelatedInfra").value;
  const relatedIndoorInfraId = document.getElementById("editRelatedIndoorInfra").value;
  const campusId = document.getElementById("editCampusDropdown").value;

  // Indoor/Outdoor data handling
  let latitude = parseFloat(document.getElementById("editLatitude").value);
  let longitude = parseFloat(document.getElementById("editLongitude").value);

  let indoor = null;
  if (type === "indoorInfra") {
    indoor = {
      floor: document.getElementById("editFloor").value.trim(),
      x: parseFloat(document.getElementById("editXCoord").value) || 0,
      y: parseFloat(document.getElementById("editYCoord").value) || 0
    };
    // Indoor infra should not have lat/long
    latitude = null;
    longitude = null;
  }

  try {
    const versionRef = doc(db, versionRefPath);
    const versionSnap = await getDoc(versionRef);
    if (!versionSnap.exists()) throw new Error("Version document not found!");

    const versionData = versionSnap.data();
    const updatedNodes = versionData.nodes.map((node) => {
      if (node.node_id === nodeId) {
        return {
          ...node,
          name: nodeName,
          latitude,
          longitude,
          type,
          related_infra_id: relatedInfraId,
          related_room_id: relatedIndoorInfraId,
          indoor,
          campus_id: campusId,
          updated_at: new Date(),
        };
      }
      return node;
    });

    await updateDoc(versionRef, { nodes: updatedNodes });

    // ✅ Update StaticDataVersions/GlobalInfo after saving or updating a node
    const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
    await updateDoc(staticDataRef, {
        infrastructure_updated: true,
    });


    alert("✅ Node updated successfully!");
    document.getElementById("editNodeModal").style.display = "none";
    renderNodesTable();

  } catch (err) {
    console.error("Error updating node:", err);
    alert("❌ Failed to update node: " + err.message);
  }
});













































// ======================= EDGE SECTION =============================

// ----------- Auto-Increment Edge ID -----------
async function generateNextEdgeId() {
    // Get current active map and version
    const mapSelect = document.getElementById("mapSelect");
    const mapId = mapSelect ? mapSelect.value : null;
    if (!mapId) return;

    const mapDocRef = doc(db, "MapVersions", String(mapId));
    const mapDocSnap = await getDoc(mapDocRef);
    if (!mapDocSnap.exists()) return;

    const currentVersion = mapDocSnap.data().current_version || "v1.0.0";
    const versionRef = doc(db, "MapVersions", String(mapId), "versions", currentVersion);
    const versionSnap = await getDoc(versionRef);
    if (!versionSnap.exists()) return;

    const edges = Array.isArray(versionSnap.data().edges) ? versionSnap.data().edges : [];

    let maxNum = 0;
    edges.forEach(edge => {
        if (edge.edge_id) {
            const num = parseInt(edge.edge_id.replace("EDG-", ""));
            if (!isNaN(num) && num > maxNum) maxNum = num;
        }
    });

    const nextId = `EDG-${String(maxNum + 1).padStart(3, "0")}`;
    document.querySelector("#addEdgeModal input[type='text']").value = nextId;
    return nextId;
}
// ...existing code...

// ----------- Load Nodes into Edge Dropdowns (Current Active Map/Campus/Version) -----------
async function loadNodesDropdownsForEdge() {
    const startNodeSelect = document.getElementById("startNode");
    const endNodeSelect = document.getElementById("endNode");

    startNodeSelect.innerHTML = `<option value="">Select start node</option>`;
    endNodeSelect.innerHTML = `<option value="">Select end node</option>`;

    try {
        // 🔹 Get all MapVersions
        const mapVersionsSnap = await getDocs(collection(db, "MapVersions"));

        for (const mapDoc of mapVersionsSnap.docs) {
            const mapData = mapDoc.data();

            // 👉 Use the "current active" fields
            const currentMap = mapData.current_active_map;
            const currentCampus = mapData.current_active_campus;
            const currentVersion = mapData.current_version;

            if (!currentMap || !currentCampus || !currentVersion) {
                console.warn(`Skipping ${mapDoc.id}: Missing current active fields`);
                continue;
            }

            // 🔹 Get the current version document
            const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
            const versionSnap = await getDoc(versionRef);

            if (!versionSnap.exists()) {
                console.warn(`No version data found for: ${mapDoc.id} → ${currentVersion}`);
                continue;
            }

            const versionData = versionSnap.data();
            const nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];

            // 🔹 Filter nodes: not deleted + must belong to current campus
            const filteredNodes = nodes.filter(n => !n.is_deleted && n.campus_id === currentCampus);

            // 🔹 Sort nodes by created_at
            filteredNodes.sort((a, b) => {
                if (!a.created_at || !b.created_at) return 0;
                return a.created_at.seconds - b.created_at.seconds;
            });

            // 🔹 Populate dropdowns
            filteredNodes.forEach(node => {
                if (node.node_id) {
                    const label = `${node.node_id} - ${node.name || "Unnamed"}`;

                    const option1 = document.createElement("option");
                    option1.value = node.node_id;
                    option1.textContent = label;
                    startNodeSelect.appendChild(option1);

                    const option2 = document.createElement("option");
                    option2.value = node.node_id;
                    option2.textContent = label;
                    endNodeSelect.appendChild(option2);
                }
            });
        }
    } catch (err) {
        console.error("Error loading nodes into edge dropdowns:", err);
    }
}








let pendingEdgeData = null; // store edge temporarily

// ----------- Add Edge Handler -----------
document.querySelector("#addEdgeModal form").addEventListener("submit", async (e) => {
    e.preventDefault();

    const edgeId = document.querySelector("#addEdgeModal input[type='text']").value;
    const startNode = document.getElementById("startNode").value;
    const endNode = document.getElementById("endNode").value;

    let pathTypeEl = document.getElementById("pathType") || document.querySelector("input[name='pathType']");
    let pathType = pathTypeEl ? pathTypeEl.value.trim() : "";

    let elevationEl = document.getElementById("elevation") || document.querySelector("input[name='elevation']");
    let elevation = elevationEl ? elevationEl.value.trim() : "";

    const toSnakeCase = str => str.toLowerCase().replace(/\s+/g, "_");
    if (pathType && !["via_overpass", "via_underpass", "stairs", "ramp"].includes(pathType)) pathType = toSnakeCase(pathType);
    if (elevation && !["slope_up", "slope_down", "flat"].includes(elevation)) elevation = toSnakeCase(elevation);

    pendingEdgeData = {
        edge_id: edgeId,
        from_node: startNode,
        to_node: endNode,
        distance: null,
        path_type: pathType || null,
        elevations: elevation || null,
        is_active: true,
        is_deleted: false,
        created_at: new Date()
    };

    document.getElementById("edgeSaveModal").style.display = "flex";
});

// ----------- Modal Buttons -----------
document.getElementById("closeEdgeSaveModal").addEventListener("click", () => {
    pendingEdgeData = null;
    document.getElementById("edgeSaveModal").style.display = "none";
});

document.getElementById("overwriteEdgeBtn").addEventListener("click", async () => {
    await saveEdge("overwrite");
    pendingEdgeData = null;
    document.getElementById("edgeSaveModal").style.display = "none";
});

document.getElementById("newVersionEdgeBtn").addEventListener("click", async () => {
    await saveEdge("newVersion");
    pendingEdgeData = null;
    document.getElementById("edgeSaveModal").style.display = "none";
});

// 🌍 Haversine formula for lat/lng
function haversineDistance(lat1, lon1, lat2, lon2) {
    const R = 6371000; // meters
    const toRad = (deg) => deg * Math.PI / 180;

    const dLat = toRad(lat2 - lat1);
    const dLon = toRad(lon2 - lon1);

    const a =
        Math.sin(dLat / 2) * Math.sin(dLat / 2) +
        Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) *
        Math.sin(dLon / 2) * Math.sin(dLon / 2);

    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    return R * c; // distance in meters
}

async function saveEdge(option) {
    if (!pendingEdgeData) return;

    try {
        // ✅ Step 1: Get the active map (instead of campus)
        const activeMapQuery = query(
            collection(db, "MapVersions"),
            where("current_active_map", "==", pendingEdgeData.map_id || null)
        );

        let mapDoc, mapDocId, mapData, mapId;


        // If you already store active map globally, you can simplify:
        const mapsSnapshot = await getDocs(collection(db, "MapVersions"));
        mapDoc = mapsSnapshot.docs.find(d => d.data().current_active_map === d.id);

        if (!mapDoc) {
            alert("No active map found.");
            return;
        }

        // Doc ID (for Firestore paths)
        mapDocId = mapDoc.id;

        // Doc data
        mapData = mapDoc.data();

        // ✅ mapId comes from the field, not the doc ID
        mapId = mapData.map_id;

        const currentVersion = mapData.current_version || "v1.0.0";

        // ✅ Step 2: Fetch current version document
        const versionRef = doc(db, "MapVersions", mapDocId, "versions", currentVersion);
        const versionSnap = await getDoc(versionRef);

        let oldNodes = [];
        let oldEdges = [];

        if (versionSnap.exists()) {
            const versionData = versionSnap.data();
            oldNodes = versionData.nodes || [];
            oldEdges = versionData.edges || [];
        }

        // ✅ Step 3: Find start & end node inside versioned nodes array
        const startNode = oldNodes.find(n => n.node_id === pendingEdgeData.from_node);
        const endNode = oldNodes.find(n => n.node_id === pendingEdgeData.to_node);

        if (!startNode || !endNode) {
            alert("Start or End node not found in MapVersion.");
            return;
        }

        // ✅ Step 4: Calculate distance from lat/lng
        const distance = haversineDistance(
            startNode.latitude, startNode.longitude,
            endNode.latitude, endNode.longitude
        );

        // ✅ Step 5: Add distance into edge data
        pendingEdgeData.distance = Number(distance.toFixed(2)); // meters

        // ✅ Step 6: Continue with overwrite/newVersion logic
        if (option === "overwrite") {
            await updateDoc(versionRef, {
                edges: arrayUnion(pendingEdgeData)
            });

        } else if (option === "newVersion") {
            let versionMatch = /^v(\d+)\.(\d+)\.(\d+)$/.exec(currentVersion);
            let major = 1, minor = 0, patch = 0;

            if (versionMatch) {
                major = parseInt(versionMatch[1], 10);
                minor = parseInt(versionMatch[2], 10);
                patch = parseInt(versionMatch[3], 10);
            }

            if (patch < 99) patch += 1;
            else { patch = 0; minor += 1; }

            const newVersion = `v${major}.${minor}.${patch}`;

            const migratedNodes = oldNodes.map(n => ({ ...n }));
            const migratedEdges = oldEdges.map(e => ({ ...e }));
            migratedEdges.push({ ...pendingEdgeData });

            await setDoc(doc(db, "MapVersions", mapDocId, "versions", newVersion), {
                nodes: migratedNodes,
                edges: migratedEdges
            });

            await updateDoc(doc(db, "MapVersions", mapDocId), { current_version: newVersion });
            alert(`New version created: ${newVersion} with migrated nodes and edges`);
        }

          // ✅ Update StaticDataVersions/GlobalInfo after saving or updating a node
          const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
          await updateDoc(staticDataRef, {
              infrastructure_updated: true,
          });

        // ✅ Refresh UI
        renderEdgesTable();
        loadMap(mapId);

        // ✅ Close modals automatically
        document.getElementById("edgeSaveModal").style.display = "none";
        document.getElementById("addEdgeModal").style.display = "none";

        // ✅ Reset
        pendingEdgeData = null;

    } catch (err) {
        console.error(err);
        alert("Error saving edge: " + err);
    }
}





async function renderEdgesTable() {
    const tbody = document.querySelector(".edgetbl tbody");
    if (!tbody) return;

    // Clear table and show loading spinner
    tbody.innerHTML = `
        <tr>
            <td colspan="6" style="text-align:center;">
                <div class="spinner"></div>
                <span class="loading-text">Loading edges...</span>
            </td>
        </tr>
    `;

    try {
        let edges = [];
        let mapVersions = [];

        const mapVersionsSnap = await getDocs(collection(db, "MapVersions"));
        mapVersions = mapVersionsSnap.docs.map(d => ({ id: d.id, ...d.data() }));

        for (const mapData of mapVersions) {
            const currentCampus = mapData.current_active_campus;
            const currentVersion = mapData.current_version;
            if (!currentCampus || !currentVersion) continue;

            const versionRef = doc(db, "MapVersions", mapData.id, "versions", currentVersion);
            const versionSnap = await getDoc(versionRef);
            if (!versionSnap.exists()) continue;

            const versionData = versionSnap.data();

            const allNodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
            const allEdges = Array.isArray(versionData.edges) ? versionData.edges : [];

            const nodeCampusMap = {};
            allNodes.forEach(n => {
                if (n.node_id && n.campus_id) nodeCampusMap[n.node_id] = n.campus_id;
            });

            const filteredEdges = allEdges.filter(e => {
                if (e.is_deleted) return false;
                const fromCampus = nodeCampusMap[e.from_node];
                const toCampus = nodeCampusMap[e.to_node];
                return fromCampus === currentCampus || toCampus === currentCampus;
            });

            edges.push(...filteredEdges);
        }

        // Sort edges by created_at
        edges.sort((a, b) => (a?.created_at?.seconds || 0) - (b?.created_at?.seconds || 0));

        // Clear spinner row
        tbody.innerHTML = "";

        // Render edges
        edges.forEach(data => {
            const formatText = (value) => {
                if (!value) return "-";
                return value.toString().split("_").map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(" ");
            };

            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${data.edge_id}</td>
                <td>${data.from_node} → ${data.to_node}</td>
                <td>${data.distance || "-"}</td>
                <td>${formatText(data.path_type)}</td>
                <td>${formatText(data.elevations)}</td>
                <td class="actions">
                    <i class="fas fa-edit"></i>
                    <i class="fas fa-trash" data-id="${data.edge_id}"></i>
                </td>
            `;
            tbody.appendChild(tr);
        });

        setupEdgeDeleteHandlers();
        console.log(`✅ Rendered ${edges.length} edges for the current active campus`);

    } catch (err) {
        console.error("Error loading edges:", err);
        tbody.innerHTML = `<tr><td colspan="6" style="text-align:center;color:red;">Failed to load edges</td></tr>`;
    }
}






// THIS SHOWS THE EDGES FROM THE VERSIONS

// // ----------- Load Edges Table -----------
// async function renderEdgesTable() {
//     const tbody = document.querySelector(".edgetbl tbody");
//     if (!tbody) return;
//     tbody.innerHTML = "";

//     try {
//         // 🔍 Find all maps
//         const mapsSnap = await getDocs(collection(db, "MapVersions"));

//         for (const mapDoc of mapsSnap.docs) {
//             const mapData = mapDoc.data();
//             const currentVersion = mapData.current_version || "v1.0.0";

//             const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
//             const versionSnap = await getDoc(versionRef);

//             if (!versionSnap.exists()) continue;

//             const { edges = [] } = versionSnap.data();

//             edges.forEach(edge => {
//                 // Formatter helper
//                 const formatText = (value) => {
//                     if (!value) return "-";
//                     return value
//                         .split("_")
//                         .map(word => word.charAt(0).toUpperCase() + word.slice(1))
//                         .join(" ");
//                 };

//                 const formattedPathType = formatText(edge.path_type);
//                 const formattedElevation = formatText(edge.elevations);

//                 const tr = document.createElement("tr");
//                 tr.innerHTML = `
//                     <td>${edge.edge_id}</td>
//                     <td>${edge.from_node} > ${edge.to_node}</td>
//                     <td>${edge.distance || "-"}</td>
//                     <td>${formattedPathType}</td>
//                     <td>${formattedElevation}</td>
//                     <td class="actions">
//                         <i class="fas fa-edit"></i>
//                         <i class="fas fa-trash"></i>
//                     </td>
//                 `;
//                 tbody.appendChild(tr);
//             });
//         }
//     } catch (err) {
//         console.error("Error rendering edges:", err);
//     }
// }









// ----------- Edge Modal Controls -----------
window.openEdgeModal = async function () {
    document.getElementById("addEdgeModal").style.display = "flex";
    await generateNextEdgeId();
    await loadNodesDropdownsForEdge();
};
window.closeEdgeModal = function () {
    document.getElementById("addEdgeModal").style.display = "none";
};





// ======================= EDIT EDGE SECTION =============================

// ----------- Select Template Storage for Edit Modal -----------
const selectTemplates = {};
document.addEventListener("DOMContentLoaded", () => {
    ["editPathType", "editElevation"].forEach(id => {
        const el = document.getElementById(id);
        if (el) selectTemplates[id] = el.outerHTML;
    });
});

// ----------- Ensure Select Exists (for custom input restoration) -----------
function ensureSelectExists(selectId) {
    let select = document.getElementById(selectId);
    if (select) return select;

    const modal = document.getElementById("editEdgeModal");
    const input = modal.querySelector(`input[name='${selectId}']`);
    if (input) {
        const tpl = selectTemplates[selectId];
        if (!tpl) return null;
        const wrapper = document.createElement("div");
        wrapper.innerHTML = tpl.trim();
        const newSelect = wrapper.firstElementChild;
        input.parentNode.replaceChild(newSelect, input);
        return newSelect;
    }
    return null;
}

// ----------- Handle Preselect or Custom Input for Edit Modal -----------
function handlePreselectOrCustom(selectId, value) {
    let select = document.getElementById(selectId);
    if (!select) select = ensureSelectExists(selectId);

    if (!select) return;

    if (!value) {
        select.value = "";
        return;
    }

    const optionExists = Array.from(select.options).some(opt => opt.value === value);
    if (optionExists) {
        select.value = value;
    } else {
        // custom value → replace with input
        const input = document.createElement("input");
        input.type = "text";
        input.name = selectId;
        input.value = value;
        input.classList.add("custom-input");
        select.parentNode.replaceChild(input, select);
    }
}

// ----------- Edit Edge Modal Open Handler (corrected to use MapVersions schema) -----------
document.querySelector(".edgetbl").addEventListener("click", async (e) => {
    if (!e.target.classList.contains("fa-edit")) return;

    const tr = e.target.closest("tr");
    const edgeId = tr.children[0].textContent.trim();
    if (!edgeId) return;

    try {
        let edgeData = null;

        // 🔹 Search through MapVersions → versions → edges arrays
        const mapVersionsSnap = await getDocs(collection(db, "MapVersions"));
        for (const mapDoc of mapVersionsSnap.docs) {
            const mapData = mapDoc.data();
            const currentVersion = mapData.current_version;
            if (!currentVersion) continue;

            const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
            const versionSnap = await getDoc(versionRef);
            if (!versionSnap.exists()) continue;

            const versionData = versionSnap.data();
            const found = Array.isArray(versionData.edges)
                ? versionData.edges.find(e => e.edge_id === edgeId)
                : null;

            if (found) {
                edgeData = found;
                break;
            }
        }

        if (!edgeData) {
            alert("Edge data not found in MapVersions.");
            return;
        }

        // ✅ Fill modal fields
        document.getElementById("editEdgeId").value = edgeData.edge_id || "";

        await loadNodesDropdownsForEditEdge(edgeData.from_node, edgeData.to_node);

        handlePreselectOrCustom("editPathType", edgeData.path_type);
        handlePreselectOrCustom("editElevation", edgeData.elevations || edgeData.elevation);

        // ✅ Store info for saving later
        const modal = document.getElementById("editEdgeModal");
        modal.dataset.edgeId = edgeData.edge_id;
        modal.dataset.mapId = edgeData.map_id || "";
        modal.style.display = "flex";

    } catch (err) {
        console.error("Error opening edge edit modal:", err);
        alert("Failed to load edge data. See console for details.");
    }
});



// ----------- Load Nodes into Edit Edge Dropdowns (uses MapVersions -> versions -> nodes) -----------
async function loadNodesDropdownsForEditEdge(selectedFrom, selectedTo) {
    const startNodeSelect = document.getElementById("editStartNode");
    const endNodeSelect = document.getElementById("editEndNode");

    startNodeSelect.innerHTML = `<option value="">Select start node</option>`;
    endNodeSelect.innerHTML = `<option value="">Select end node</option>`;

    try {
        const mapVersionsSnap = await getDocs(collection(db, "MapVersions"));

        for (const mapDoc of mapVersionsSnap.docs) {
            const mapData = mapDoc.data();

            // use the same "current active" fields as your add-edge loader
            const currentMap = mapData.current_active_map;
            const currentCampus = mapData.current_active_campus;
            const currentVersion = mapData.current_version;

            if (!currentMap || !currentCampus || !currentVersion) {
                // skip docs that are not currently active
                continue;
            }

            const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
            const versionSnap = await getDoc(versionRef);
            if (!versionSnap.exists()) continue;

            const versionData = versionSnap.data();
            const nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];

            // filter nodes: not deleted and belonging to current campus
            const filteredNodes = nodes.filter(n => !n.is_deleted && n.campus_id === currentCampus);

            // sort by created_at.seconds (fallback to 0 if missing)
            filteredNodes.sort((a, b) => {
                const aSec = (a.created_at && a.created_at.seconds) ? a.created_at.seconds : 0;
                const bSec = (b.created_at && b.created_at.seconds) ? b.created_at.seconds : 0;
                return aSec - bSec;
            });

            // append to selects (preserve order across versions)
            filteredNodes.forEach(node => {
                if (!node.node_id) return;
                const label = `${node.node_id} - ${node.name || "Unnamed"}`;

                const opt1 = document.createElement("option");
                opt1.value = node.node_id;
                opt1.textContent = label;
                if (node.node_id === selectedFrom) opt1.selected = true;
                startNodeSelect.appendChild(opt1);

                const opt2 = document.createElement("option");
                opt2.value = node.node_id;
                opt2.textContent = label;
                if (node.node_id === selectedTo) opt2.selected = true;
                endNodeSelect.appendChild(opt2);
            });
        }
    } catch (err) {
        console.error("Error loading nodes into edit edge dropdowns:", err);
    }
}


// ----------- Handle "Other" Option for Selects -----------
function handleOtherOption(selectId) {
    const select = document.getElementById(selectId);
    if (!select) return;

    select.addEventListener("change", function () {
        if (this.value === "other") {
            const input = document.createElement("input");
            input.type = "text";
            input.name = selectId;
            input.placeholder = "Enter your own value";
            input.classList.add("custom-input");

            this.parentNode.replaceChild(input, this);

            input.addEventListener("blur", function () {
                if (input.value.trim() === "") {
                    input.parentNode.replaceChild(select, input);
                    select.value = "";
                }
            });
        }
    });
}
handleOtherOption("pathType");
handleOtherOption("elevation");
handleOtherOption("editPathType");
handleOtherOption("editElevation");


// ----------- Edit Edge Save Handler (saves to correct MapVersions/versions/edges path) -----------
document.querySelector("#editEdgeModal form").addEventListener("submit", async (e) => {
    e.preventDefault();

    const modal = document.getElementById("editEdgeModal");
    const docId = modal.dataset.docId; // this will be the edge_id (we’ll find it properly below)

    // Helper: get either <select> or <input> value
    const getFieldValue = (id) => {
        const select = document.getElementById(id);
        const input = document.querySelector(`input[name='${id}']`);
        return select ? select.value.trim() : (input ? input.value.trim() : "");
    };

    // Helper: convert to snake_case
    const toSnakeCase = str => str.toLowerCase().replace(/\s+/g, "_");

    let pathType = getFieldValue("editPathType");
    let elevation = getFieldValue("editElevation");

    if (pathType && !["via_overpass", "via_underpass", "stairs", "ramp"].includes(pathType)) {
        pathType = toSnakeCase(pathType);
    }
    if (elevation && !["slope_up", "slope_down", "flat"].includes(elevation)) {
        elevation = toSnakeCase(elevation);
    }

    const updatedData = {
        from_node: document.getElementById("editStartNode").value,
        to_node: document.getElementById("editEndNode").value,
        path_type: pathType || null,
        elevations: elevation || null,
    };

    try {
        // 🔹 Get the active MapVersion info first
        const mapVersionsSnap = await getDocs(collection(db, "MapVersions"));
        let activeVersionRef = null;

        for (const mapDoc of mapVersionsSnap.docs) {
            const mapData = mapDoc.data();
            if (mapData.current_active_map && mapData.current_active_campus && mapData.current_version) {
                activeVersionRef = doc(db, "MapVersions", mapDoc.id, "versions", mapData.current_version);
                break;
            }
        }

        if (!activeVersionRef) {
            alert("No active map version found!");
            return;
        }

        // 🔹 Get the version data
        const versionSnap = await getDoc(activeVersionRef);
        if (!versionSnap.exists()) {
            alert("Active version document not found!");
            return;
        }

        const versionData = versionSnap.data();
        const edges = Array.isArray(versionData.edges) ? [...versionData.edges] : [];

        // 🔹 Find the edge by ID and update it
        const edgeIndex = edges.findIndex(edge => edge.edge_id === docId);
        if (edgeIndex === -1) {
            alert("Edge not found in current version!");
            return;
        }

        edges[edgeIndex] = {
            ...edges[edgeIndex],
            ...updatedData,
            updated_at: new Date(),
        };

        // 🔹 Save the updated edges array
        await updateDoc(activeVersionRef, { edges });

        // ✅ Update StaticDataVersions/GlobalInfo after saving or updating a node
        const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
        await updateDoc(staticDataRef, {
            infrastructure_updated: true,
        });

        alert("Edge updated successfully!");
        modal.style.display = "none";
        renderEdgesTable();

    } catch (err) {
        console.error("Error updating edge:", err);
        alert("Error updating edge: " + err.message);
    }
});

// ----------- Edit Edge Modal Cancel & Outside Click -----------
document.getElementById("cancelEditEdgeBtn").addEventListener("click", () => {
    document.getElementById("editEdgeModal").style.display = "none";
});
document.getElementById("editEdgeModal").addEventListener("click", (e) => {
    if (e.target.id === "editEdgeModal") {
        document.getElementById("editEdgeModal").style.display = "none";
    }
});












// ======================= UI & TAB CONTROLS =============================

// ----------- Initial Data Load -----------
window.onload = () => {

    renderEdgesTable();
};

document.addEventListener("DOMContentLoaded", function () {
    const tabs = document.querySelectorAll(".top-tabs .tab");
    const tables = document.querySelectorAll(".bottom-tbl > div");
    const addButton = document.querySelector(".addnode .add-btn");
    const breadcrumbDetail = document.querySelector(".breadcrumb .span-details"); // added

    // Modals
    const addNodeModal = document.getElementById("addNodeModal");
    const addEdgeModal = document.getElementById("addEdgeModal");

    const cancelNodeBtn = document.querySelector("#addNodeModal .cancel-btn");
    const cancelEdgeBtn = document.querySelector("#addEdgeModal .cancel-btn");

    // Button text mapping
    const buttonTexts = ["Add Node", "Add Edge"];

    // Breadcrumb text mapping
    const breadcrumbTexts = ["Nodes", "Edges"]; // <-- what shows in the breadcrumb span

    // Tab switching
    tabs.forEach((tab, index) => {
        tab.addEventListener("click", () => {
            tabs.forEach(t => t.classList.remove("active"));
            tables.forEach(tbl => tbl.style.display = "none");
            tab.classList.add("active");
            tables[index].style.display = "block";

            if (buttonTexts[index]) {
                addButton.textContent = buttonTexts[index];
            }

            // ✅ Update breadcrumb
            if (breadcrumbTexts[index]) {
                breadcrumbDetail.textContent = ' ' + breadcrumbTexts[index];
            }
        });
    });

    // Open modal
    addButton.addEventListener("click", () => {
        if (addButton.textContent === "Add Node") {
            window.openNodeModal();
            addNodeModal.style.display = "flex";
        } else if (addButton.textContent === "Add Edge") {
            window.openEdgeModal();
            addEdgeModal.style.display = "flex";
        }
    });

    // Close Node modal
    cancelNodeBtn.addEventListener("click", () => {
        window.closeNodeModal();
    });
    addNodeModal.addEventListener("click", (e) => {
        if (e.target === addNodeModal) {
            window.closeNodeModal();
        }
    });

    // Close Edge modal
    cancelEdgeBtn.addEventListener("click", () => {
        addEdgeModal.style.display = "none";
    });
    addEdgeModal.addEventListener("click", (e) => {
        if (e.target === addEdgeModal) {
            addEdgeModal.style.display = "none";
        }
    });
});




document.addEventListener("DOMContentLoaded", function() {
    const indoorCheckbox = document.getElementById("indoorCheckbox");
    const outdoorCheckbox = document.getElementById("outdoorCheckbox");
    const indoorDetails = document.getElementById("indoorDetails");

    if (indoorCheckbox && outdoorCheckbox && indoorDetails) {
        indoorCheckbox.addEventListener("change", function() {
            if (indoorCheckbox.checked) {
                indoorDetails.style.display = "block";
                outdoorCheckbox.checked = false;
            } else {
                indoorDetails.style.display = "none";
            }
        });

        outdoorCheckbox.addEventListener("change", function() {
            if (outdoorCheckbox.checked) {
                indoorCheckbox.checked = false;
                indoorDetails.style.display = "none";
            }
        });
    }
});



  const editNodeModal = document.getElementById("editNodeModal");
  const cancelEditBtn = document.getElementById("cancelEditNodeBtn");

  // Use event delegation to catch clicks on edit icons
  document.querySelector(".nodetbl").addEventListener("click", (e) => {
    if (e.target.classList.contains("fa-edit")) {
      editNodeModal.style.display = "flex"; // show modal
    }
  });

  // Cancel button closes modal
  cancelEditBtn.addEventListener("click", () => {
    editNodeModal.style.display = "none";
  });

  // Click outside modal box closes it
  editNodeModal.addEventListener("click", (e) => {
    if (e.target === editNodeModal) {
      editNodeModal.style.display = "none";
    }
  });


    const editEdgeModal = document.getElementById("editEdgeModal");
  const cancelEditEdgeBtn = document.getElementById("cancelEditEdgeBtn");

  // Use event delegation to catch clicks on edit icons inside .edgetbl
  document.querySelector(".edgetbl").addEventListener("click", (e) => {
    if (e.target.classList.contains("fa-edit")) {
      editEdgeModal.style.display = "flex"; // show modal
    }
  });

  // Cancel button closes modal
  cancelEditEdgeBtn.addEventListener("click", () => {
    editEdgeModal.style.display = "none";
  });

  // Click outside modal box closes it
  editEdgeModal.addEventListener("click", (e) => {
    if (e.target === editEdgeModal) {
      editEdgeModal.style.display = "none";
    }
  });


  








let edgeToDelete = null;

// ----------- Edge Delete Modal Logic -----------
function setupEdgeDeleteHandlers() {
    const tbody = document.querySelector(".edgetbl tbody");
    if (!tbody) return;

    tbody.querySelectorAll(".fa-trash").forEach(btn => {
        btn.addEventListener("click", () => {
            const tr = btn.closest("tr");
            const edgeId = tr.children[0]?.textContent || "";
            const docId = btn.dataset.id;

            edgeToDelete = { docId, edgeId };
            document.getElementById("deleteEdgePrompt").textContent =
                `Are you sure you want to delete edge "${edgeId}"?`;
            document.getElementById("deleteEdgeModal").style.display = "flex";
        });
    });
}

// ----------- Confirm Edge Deletion -----------
document.getElementById("confirmDeleteEdgeBtn").addEventListener("click", async () => {
    if (!edgeToDelete) return;
    try {
        await updateDoc(doc(db, "Edges", edgeToDelete.docId), {
            is_deleted: true,
            deletedAt: new Date()
        });
        document.getElementById("deleteEdgeModal").style.display = "none";
        edgeToDelete = null;
        renderEdgesTable();
    } catch (err) {
        alert("Error deleting edge: " + err);
    }
});

// ----------- Cancel Edge Deletion -----------
document.getElementById("cancelDeleteEdgeBtn").addEventListener("click", () => {
    document.getElementById("deleteEdgeModal").style.display = "none";
    edgeToDelete = null;
});

// ----------- Close modal if click outside -----------
document.getElementById("deleteEdgeModal").addEventListener("click", (e) => {
    if (e.target === document.getElementById("deleteEdgeModal")) {
        document.getElementById("deleteEdgeModal").style.display = "none";
        edgeToDelete = null;
    }
});




let nodeToDelete = null;

// ----------- Node Delete Modal Logic -----------
function setupNodeDeleteHandlers() {
    const tbody = document.querySelector(".nodetbl tbody");
    if (!tbody) return;

    tbody.querySelectorAll(".fa-trash").forEach(btn => {
        btn.addEventListener("click", () => {
            const tr = btn.closest("tr");
            const nodeId = tr.children[0]?.textContent || "";
            const docId = btn.dataset.id;

            nodeToDelete = { docId, nodeId };
            document.getElementById("deleteNodePrompt").textContent =
                `Are you sure you want to delete node "${nodeId}"?`;
            document.getElementById("deleteNodeModal").style.display = "flex";
        });
    });
}

// ----------- Confirm Node Deletion -----------
document.getElementById("confirmDeleteNodeBtn").addEventListener("click", async () => {
    if (!nodeToDelete) return;
    try {
        await updateDoc(doc(db, "Nodes", nodeToDelete.docId), {
            is_deleted: true,
            deletedAt: new Date()
        });
        document.getElementById("deleteNodeModal").style.display = "none";
        nodeToDelete = null;
        renderNodesTable();
    } catch (err) {
        alert("Error deleting node: " + err);
    }
});

// ----------- Cancel Node Deletion -----------
document.getElementById("cancelDeleteNodeBtn").addEventListener("click", () => {
    document.getElementById("deleteNodeModal").style.display = "none";
    nodeToDelete = null;
});

// ----------- Close modal if click outside -----------
document.getElementById("deleteNodeModal").addEventListener("click", (e) => {
    if (e.target === document.getElementById("deleteNodeModal")) {
        document.getElementById("deleteNodeModal").style.display = "none";
        nodeToDelete = null;
    }
});

















// ----------- Populate Maps (Online + Offline) -----------
async function populateMaps() {
    const mapSelect = document.getElementById("mapSelect");
    const campusSelect = document.getElementById("campusSelect");
    const versionSelect = document.getElementById("versionSelect");

    mapSelect.innerHTML = '<option value="">Select Map</option>';
    campusSelect.innerHTML = '<option value="">Select Campus</option>';
    versionSelect.innerHTML = '<option value="">Select Version</option>';

    try {
        let mapsData = [];

        if (navigator.onLine) {
            // Online: Firestore
            const mapsSnap = await getDocs(collection(db, "MapVersions"));
            mapsData = mapsSnap.docs.map(doc => ({ id: doc.id, ...doc.data() }));
        } else {
            // Offline: JSON fallback
            const res = await fetch("../assets/firestore/MapVersions.json");
            mapsData = await res.json();
        }

        // Populate map select options
        mapsData.forEach(map => {
            const option = document.createElement("option");
            option.value = map.id;
            option.textContent = `${map.map_name || map.id} (${map.id})`;
            mapSelect.appendChild(option);
        });

        // Select first map as default
        if (mapsData.length > 0) {
            const firstMapId = mapsData[0].id;
            mapSelect.value = firstMapId;
            await populateCampuses(firstMapId, true, mapsData);
            await populateVersions(firstMapId, true, mapsData);

            await renderNodesTable();
            await renderEdgesTable();
            await loadMap(firstMapId);
        }

        // Remove previous event listeners before adding new one
        mapSelect.onchange = null;
        mapSelect.addEventListener("change", async () => {
            const selectedMapId = mapSelect.value;
            if (!selectedMapId) return;

            // 🔹 Update current_active_map in Firestore if online
            if (navigator.onLine) {
                const mapDocRef = doc(db, "MapVersions", selectedMapId);
                await updateDoc(mapDocRef, { current_active_map: selectedMapId });
            }

            await populateCampuses(selectedMapId, true, mapsData);
            await populateVersions(selectedMapId, true, mapsData);

            await renderNodesTable();
            await renderEdgesTable();
            await loadMap(selectedMapId);
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
        const mapDocRef = doc(db, "MapVersions", mapId);
        const mapDocSnap = await getDoc(mapDocRef);
        if (!mapDocSnap.exists()) return;
        mapData = mapDocSnap.data();
    } else {
        if (!mapsData) {
            mapsData = await fetch("../assets/firestore/MapVersions.json").then(res => res.json());
        }
        mapData = mapsData.find(m => m.id === mapId);
        if (!mapData) return;
    }

    const campuses = mapData.campus_included || [];
    const currentCampus = mapData.current_active_campus || "";

    campuses.forEach(campusId => {
        const option = document.createElement("option");
        option.value = campusId;
        option.textContent = campusId;
        campusSelect.appendChild(option);
    });

    if (selectCurrent && currentCampus && campuses.includes(currentCampus)) {
        campusSelect.value = currentCampus;
    } else if (campuses.length > 0) {
        campusSelect.value = campuses[0];
    }

    campusSelect.onchange = null;
    campusSelect.addEventListener("change", async () => {
        const selectedCampus = campusSelect.value;
        const selectedMapId = mapSelect.value;
        const selectedVersion = versionSelect.value;
        if (!selectedCampus) return;

        // 🔹 Update current_active_campus in Firestore if online
        if (navigator.onLine) {
            const mapDocRef = doc(db, "MapVersions", selectedMapId);
            await updateDoc(mapDocRef, { current_active_campus: selectedCampus });
        }

        await renderNodesTable();
        await renderEdgesTable();
        await loadMap(selectedMapId, selectedCampus, selectedVersion);
    });
}

async function populateVersions(mapId, selectCurrent = true, mapsData = null) {
    const versionSelect = document.getElementById("versionSelect");
    versionSelect.innerHTML = '<option value="">Select Version</option>';

    let mapData, currentVersion, versions = [];

    if (navigator.onLine) {
        const mapDocRef = doc(db, "MapVersions", mapId);
        const mapDocSnap = await getDoc(mapDocRef);
        if (!mapDocSnap.exists()) return;

        mapData = mapDocSnap.data();
        currentVersion = mapData.current_version || "";

        const versionsSnap = await getDocs(collection(db, "MapVersions", mapId, "versions"));
        versions = versionsSnap.docs.map(docSnap => ({ id: docSnap.id, ...docSnap.data() }));

    } else {
        if (!mapsData) {
            mapsData = await fetch("../assets/firestore/MapVersions.json").then(res => res.json());
        }
        mapData = mapsData.find(m => m.id === mapId);
        if (!mapData) return;

        currentVersion = mapData.current_version || "";
        versions = mapData.versions || [];
    }

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

    versionSelect.onchange = null;
    versionSelect.addEventListener("change", async () => {
        const selectedVersion = versionSelect.value;
        const selectedMapId = mapSelect.value;
        const selectedCampus = campusSelect.value;
        if (!selectedVersion) return;

        // 🔹 Update current_version in Firestore if online
        if (navigator.onLine) {
            const mapDocRef = doc(db, "MapVersions", selectedMapId);
            await updateDoc(mapDocRef, { current_version: selectedVersion });

            // ✅ Also update ALL MapVersions documents to mark current_version_update = true
            const mapVersionsCollection = collection(db, "MapVersions");
            const snapshot = await getDocs(mapVersionsCollection);
            const batch = writeBatch(db);

            snapshot.docs.forEach((docSnap) => {
                batch.update(docSnap.ref, {
                    current_version_update: true,
                });
            });

            await batch.commit();
            console.log("✅ All MapVersions documents updated: current_version_update = true");
        }

        await renderNodesTable();
        await renderEdgesTable();
        await loadMap(selectedMapId, selectedCampus, selectedVersion);
    });
}



// ----------- Helper: set current active map -----------
async function setActiveMap(mapId) {
    const mapDocRef = doc(db, "MapVersions", String(mapId)); // ✅ enforce doc.id
    try {
        await updateDoc(mapDocRef, { current_active_map: String(mapId) });
        console.log(`Current active map updated to ${mapId}`);
    } catch (err) {
        console.error("Error updating current active map:", err);
    }
}

// Run on page load
document.addEventListener("DOMContentLoaded", () => {
    populateMaps();
});









// ✅ Helper: compute geographic center of nodes
function getGeographicCenter(nodes, campusId) {
  // Special case for CAMP-02 → always use fixed center
  if (campusId === "CAMP-02") {
    return [6.9130, 122.0630];
  }

  if (!nodes.length) return [6.9130, 122.0630]; // fallback default

  let x = 0, y = 0, z = 0;

  nodes.forEach(n => {
    if (!n.latitude || !n.longitude) return;
    const latRad = parseFloat(n.latitude) * Math.PI / 180;
    const lonRad = parseFloat(n.longitude) * Math.PI / 180;

    x += Math.cos(latRad) * Math.cos(lonRad);
    y += Math.cos(latRad) * Math.sin(lonRad);
    z += Math.sin(latRad);
  });

  const total = nodes.length;
  x /= total;
  y /= total;
  z /= total;

  const lon = Math.atan2(y, x);
  const hyp = Math.sqrt(x * x + y * y);
  const lat = Math.atan2(z, hyp);

  return [lat * 180 / Math.PI, lon * 180 / Math.PI];
}


// ✅ Helper: compute bounds of all nodes in a campus
function getCampusBounds(nodes, campusId) {
  // Special case for CAMP-02 → fixed center
  if (campusId === "CAMP-02") {
    return null; // we'll skip bounds fit for CAMP-02
  }

  const latLngs = nodes
    .filter(n => n.latitude && n.longitude)
    .map(n => [parseFloat(n.latitude), parseFloat(n.longitude)]);

  return latLngs.length ? L.latLngBounds(latLngs) : null;
}









let mapFull = null;
let mapOverview = null;
let showAllCampuses = false; // 🔹 global toggle state
let currentMapId = null;     // 🔹 to reload same map when toggle changes

// --- Toggle listener ---
document.getElementById("customToggle").addEventListener("change", (e) => {
  showAllCampuses = e.target.checked;
  console.log(showAllCampuses ? "🟢 Showing ALL campuses" : "🔴 Showing active campus only");

  if (currentMapId) {
    loadMap(currentMapId); // reload map with toggle state
  }
});

// ===============================================================
// --- LOAD MAP FUNCTION ---
// ===============================================================
async function loadMap(mapId, campusId = null, versionId = null) {
  try {
    currentMapId = mapId; // store current map for toggle reload
    const safeMapId = String(mapId);

    let mapData, activeCampus, activeVersion, nodes = [], edges = [];
    let infraMap = {}, roomMap = {}, campusMap = {};

    if (navigator.onLine) {
      // --- Firestore ---
      const mapDocRef = doc(db, "MapVersions", safeMapId);
      const mapDocSnap = await getDoc(mapDocRef);
      if (!mapDocSnap.exists()) return console.error("❌ Map not found:", safeMapId);

      mapData = mapDocSnap.data();
      activeCampus = campusId || mapData.current_active_campus;
      activeVersion = String(versionId || mapData.current_version || "");

      const versionDocRef = doc(db, "MapVersions", safeMapId, "versions", activeVersion);
      const versionDocSnap = await getDoc(versionDocRef);
      if (!versionDocSnap.exists()) return console.error("❌ Version not found:", activeVersion);

      const versionData = versionDocSnap.data();
      nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
      edges = Array.isArray(versionData.edges) ? versionData.edges : [];

      const [infraSnap, roomSnap, campusSnap] = await Promise.all([
        getDocs(collection(db, "Infrastructure")),
        getDocs(collection(db, "Rooms")),
        getDocs(collection(db, "Campus"))
      ]);
      infraSnap.forEach(doc => infraMap[doc.data().infra_id] = doc.data().name);
      roomSnap.forEach(doc => roomMap[doc.data().room_id] = doc.data().name);
      campusSnap.forEach(doc => campusMap[doc.data().campus_id] = doc.data().campus_name);

    } else {
      // --- Offline JSON ---
      const mapRes = await fetch("../assets/firestore/MapVersions.json");
      const mapsJson = await mapRes.json();
      mapData = mapsJson.find(m => m.map_id === safeMapId) || mapsJson[0];
      if (!mapData) return console.error("No maps found in JSON");

      activeCampus = campusId || mapData.current_active_campus;
      activeVersion = String(versionId || mapData.current_version || (mapData.versions?.[0]?.id || ""));
      const versionData = mapData.versions.find(v => v.id === activeVersion);
      nodes = versionData ? versionData.nodes || [] : [];
      edges = versionData ? versionData.edges || [] : [];

      const [infraRes, roomRes, campusRes] = await Promise.all([
        fetch("../assets/firestore/Infrastructure.json"),
        fetch("../assets/firestore/Rooms.json"),
        fetch("../assets/firestore/Campus.json")
      ]);
      const infraJson = await infraRes.json();
      const roomJson = await roomRes.json();
      const campusJson = await campusRes.json();
      infraJson.forEach(i => infraMap[i.infra_id] = i.name);
      roomJson.forEach(r => roomMap[r.room_id] = r.name);
      campusJson.forEach(c => campusMap[c.campus_id] = c.campus_name);

      console.log("📂 Offline → Map, nodes, edges loaded from JSON");
    }

    // ===============================================================
    // --- FILTER NODES BASED ON TOGGLE ---
    // ===============================================================
    if (showAllCampuses && Array.isArray(mapData.campus_included)) {
      // ✅ SHOW ALL CAMPUSES
      console.log("🗺️ Displaying all campuses:", mapData.campus_included);
      nodes = nodes.filter(n => {
        if (n.is_deleted) return false;
        if (!n.campus_id) return false;
        return mapData.campus_included.includes(n.campus_id);
      });
    } else {
      // 🔴 SHOW ONLY ACTIVE CAMPUS
      nodes = nodes.filter(n => {
        if (n.is_deleted) return false;
        if (!n.campus_id) n.campus_id = activeCampus;
        return n.campus_id === activeCampus;
      });
    }

    // ===============================================================
    // --- FILTER VALID EDGES ---
    // ===============================================================
    const validNodeIds = new Set(nodes.map(n => n.node_id));
    edges = edges.filter(e =>
      !e.is_deleted &&
      validNodeIds.has(e.from_node) &&
      validNodeIds.has(e.to_node)
    );

    // ===============================================================
    // --- ADD NAME MAPPING TO NODES ---
    // ===============================================================
    nodes.forEach(d => {
      d.infraName = d.related_infra_id ? (infraMap[d.related_infra_id] || d.related_infra_id) : "-";
      d.roomName = d.related_room_id ? (roomMap[d.related_room_id] || d.related_room_id) : "-";
      d.campusName = d.campus_id ? (campusMap[d.campus_id] || d.campus_id) : "-";
    });

    // ===============================================================
    // --- CREATE / REFRESH OVERVIEW MAP ---
    // ===============================================================
    createOverviewMap(nodes, edges, activeCampus);

  } catch (err) {
    console.error("❌ Error loading map:", err);
  }
}

// ===============================================================
// --- CREATE OVERVIEW MAP ---
// ===============================================================
function createOverviewMap(nodes, edges, activeCampus) {
  if (mapOverview) {
    mapOverview.remove();
    document.getElementById("map-overview").innerHTML = "";
  }

  mapOverview = L.map("map-overview", {
    zoomControl: true,
    dragging: true,
    scrollWheelZoom: false,
    doubleClickZoom: true,
    boxZoom: false,
    keyboard: false
  });

  const bounds = getCampusBounds(nodes, activeCampus);
  if (bounds) {
    mapOverview.fitBounds(bounds, { padding: [20, 20], maxZoom: 20, animate: true });
    mapOverview.setZoom(mapOverview.getZoom() + 0.4);
  } else {
    mapOverview.setView(getGeographicCenter(nodes, activeCampus), 18);
  }

  L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
    attribution: "© OpenStreetMap"
  }).addTo(mapOverview);

  renderDataOnMap(mapOverview, { nodes, edges });

  // ===============================================================
  // --- MODAL MAP SYNC ---
  // ===============================================================
  const modal = document.getElementById("mapModal");
  const closeBtn = document.querySelector(".close-btn");

  document.getElementById("map-overview").addEventListener("click", () => {
    modal.style.display = "block";

    setTimeout(() => {
      if (mapFull) {
        mapFull.remove();
        document.getElementById("map-full").innerHTML = "";
      }

      const currentCenter = mapOverview.getCenter();
      const currentZoom = mapOverview.getZoom();

      mapFull = L.map("map-full", {
        center: currentCenter,
        zoom: currentZoom,
        zoomControl: true,
        dragging: true,
        scrollWheelZoom: true,
        doubleClickZoom: true,
        boxZoom: true,
        keyboard: true
      });

      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        attribution: "© OpenStreetMap"
      }).addTo(mapFull);

      renderDataOnMap(mapFull, { nodes, edges }, true);
    }, 200);
  });

  closeBtn.addEventListener("click", () => {
    modal.style.display = "none";
    if (mapFull) {
      mapFull.remove();
      mapFull = null;
    }
  });
}







// let mapFull = null; 
// let mapOverview = null;


// async function loadMap(mapId, campusId = null, versionId = null) {
//   try {
//     const safeMapId = String(mapId);

//     let mapData, activeCampus, activeVersion, nodes = [], edges = [];
//     let infraMap = {}, roomMap = {}, campusMap = {};

//     if (navigator.onLine) {
//       // Online: Firestore
//       const mapDocRef = doc(db, "MapVersions", safeMapId);
//       const mapDocSnap = await getDoc(mapDocRef);

//       if (!mapDocSnap.exists()) {
//         console.error("❌ Map not found:", safeMapId);
//         return;
//       }

//       mapData = mapDocSnap.data();
//       activeCampus = campusId || mapData.current_active_campus;
//       activeVersion = String(versionId || mapData.current_version || "");

//       // Fetch nodes & edges for the selected version
//       const versionDocRef = doc(db, "MapVersions", safeMapId, "versions", activeVersion);
//       const versionDocSnap = await getDoc(versionDocRef);

//       if (!versionDocSnap.exists()) {
//         console.error("❌ Version not found:", activeVersion);
//         return;
//       }

//       const versionData = versionDocSnap.data();
//       nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
//       edges = Array.isArray(versionData.edges) ? versionData.edges : [];

//       // Fetch Infra, Room, Campus names
//       const [infraSnap, roomSnap, campusSnap] = await Promise.all([
//         getDocs(collection(db, "Infrastructure")),
//         getDocs(collection(db, "Rooms")),
//         getDocs(collection(db, "Campus"))
//       ]);
//       infraSnap.forEach(doc => infraMap[doc.data().infra_id] = doc.data().name);
//       roomSnap.forEach(doc => roomMap[doc.data().room_id] = doc.data().name);
//       campusSnap.forEach(doc => campusMap[doc.data().campus_id] = doc.data().campus_name);

//     } else {
//       // Offline: JSON
//       const mapRes = await fetch("../assets/firestore/MapVersions.json");
//       const mapsJson = await mapRes.json();

//       mapData = mapsJson.find(m => m.map_id === safeMapId) || mapsJson[0];
//       if (!mapData) {
//         console.error("No maps found in JSON");
//         return;
//       }
//       activeCampus = campusId || mapData.current_active_campus;
//       activeVersion = String(versionId || mapData.current_version || (mapData.versions?.[0]?.id || ""));

//       const versionData = mapData.versions.find(v => v.id === activeVersion);
//       if (!versionData) {
//         console.error("Version not found in JSON:", activeVersion);
//         nodes = [];
//         edges = [];
//       } else {
//         nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
//         edges = Array.isArray(versionData.edges) ? versionData.edges : [];
//       }

//       // Fetch Infra, Room, Campus names from JSON
//       const [infraRes, roomRes, campusRes] = await Promise.all([
//         fetch("../assets/firestore/Infrastructure.json"),
//         fetch("../assets/firestore/Rooms.json"),
//         fetch("../assets/firestore/Campus.json")
//       ]);
//       const infraJson = await infraRes.json();
//       const roomJson = await roomRes.json();
//       const campusJson = await campusRes.json();
//       infraJson.forEach(i => infraMap[i.infra_id] = i.name);
//       roomJson.forEach(r => roomMap[r.room_id] = r.name);
//       campusJson.forEach(c => campusMap[c.campus_id] = c.campus_name);

//       console.log("📂 Offline → Map, nodes, edges loaded from JSON");
//     }

// // ✅ Filter nodes by active campus (without dropping edges)
// nodes = nodes.filter(n => {
//   if (n.is_deleted) return false;

//   // If campus_id is missing, assume it's part of the active campus
//   if (!n.campus_id) {
//     console.warn("⚠️ Node missing campus_id, assigning to active campus:", n);
//     n.campus_id = activeCampus;
//   }

//   return n.campus_id === activeCampus;
// });

// console.log("✅ Filtered nodes for campus:", activeCampus, nodes);

// // Build set of valid node IDs
// const validNodeIds = new Set(nodes.map(n => n.node_id));

// // Keep edges that connect only valid nodes, not deleted
// edges = edges.filter(e => {
//   if (e.is_deleted) return false;
//   return validNodeIds.has(e.from_node) && validNodeIds.has(e.to_node);
// });


// console.log("✅ Filtered edges for campus:", activeCampus, edges);


//     // Attach readable names
//     nodes.forEach(d => {
//       d.infraName = d.related_infra_id ? (infraMap[d.related_infra_id] || d.related_infra_id) : "-";
//       d.roomName = d.related_room_id ? (roomMap[d.related_room_id] || d.related_room_id) : "-";
//       d.campusName = d.campus_id ? (campusMap[d.campus_id] || d.campus_id) : "-";
//     });

//     // ✅ Get center of campus nodes
// // ✅ Get center of campus nodes
// const center = getGeographicCenter(nodes, activeCampus);


//     // ---- Reset map containers ----
//     const mapContainer = document.getElementById("map-overview");
//     if (mapContainer._leaflet_id) {
//       mapContainer._leaflet_id = null;
//       mapContainer.innerHTML = "";
//     }

//     const map = L.map("map-overview", {
//         zoomControl: false,
//         dragging: false,
//         scrollWheelZoom: false,
//         doubleClickZoom: false,
//         boxZoom: false,
//         keyboard: false
//     });

// const bounds = getCampusBounds(nodes, activeCampus);

// if (bounds) {
//   map.fitBounds(bounds, { padding: [10, 10] });

//   // ✅ Make the map closer after fitBounds
//   const currentZoom = map.getZoom();
//   map.setZoom(currentZoom + 0.4); // increase by 1 (or +2 if you want even closer)
// } else {
//   // fallback to fixed center
//   map.setView(getGeographicCenter(nodes, activeCampus), 18);
// }



//     L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
//       attribution: "© OpenStreetMap"
//     }).addTo(map);

//     renderDataOnMap(map, { nodes, edges });

//     // ---- Fullscreen modal map ----
//     const modal = document.getElementById("mapModal");
//     const closeBtn = document.querySelector(".close-btn");

// // --- create overview map ---
// function createOverviewMap(nodes, edges, activeCampus) {
//   if (mapOverview) {
//     mapOverview.remove();
//     document.getElementById("map-overview").innerHTML = "";
//   }

//   mapOverview = L.map("map-overview", {
//     zoomControl: false,
//     dragging: false,
//     scrollWheelZoom: false,
//     doubleClickZoom: false,
//     boxZoom: false,
//     keyboard: false
//   });

//   const bounds = getCampusBounds(nodes, activeCampus);
//   if (bounds) {
//     mapOverview.fitBounds(bounds, { padding: [20, 20], maxZoom: 20, animate: true });
//   } else {
//     mapOverview.setView(getGeographicCenter(nodes, activeCampus), 18);
//   }

//   L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
//     attribution: "© OpenStreetMap"
//   }).addTo(mapOverview);

//   renderDataOnMap(mapOverview, { nodes, edges });
// }

// // --- open modal map ---
// mapContainer.addEventListener("click", () => {
//   modal.style.display = "block";

//   setTimeout(() => {
//     if (mapFull) {
//       mapFull.remove();
//       document.getElementById("map-full").innerHTML = "";
//     }

//     // ✅ take current view from overview map
//     const currentCenter = mapOverview.getCenter();
//     const currentZoom = mapOverview.getZoom();

//     mapFull = L.map("map-full", {
//       center: currentCenter,
//       zoom: currentZoom,
//       zoomControl: true,
//       dragging: true,
//       scrollWheelZoom: true,
//       doubleClickZoom: true,
//       boxZoom: true,
//       keyboard: true
//     });

//     L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
//       attribution: "© OpenStreetMap"
//     }).addTo(mapFull);

//     renderDataOnMap(mapFull, { nodes, edges }, true);
//   }, 200);
// });

// closeBtn.addEventListener("click", () => {
//   modal.style.display = "none";
//   if (mapFull) {
//     mapFull.remove();
//     mapFull = null;
//   }
// });



//     closeBtn.addEventListener("click", () => {
//       modal.style.display = "none";
//     });

//   } catch (err) {
//     console.error("❌ Error loading map:", err);
//   }
// }












function renderDataOnMap(map, data, enableClick = false) {
  const nodes = Array.isArray(data.nodes) ? data.nodes : [];
  const edges = Array.isArray(data.edges) ? data.edges : [];

  // collect node marker references so we can ensure they render above edges
  const nodeMarkers = [];

  // --- Barriers (grouped per campus) ---
  const barrierNodes = nodes.filter(d => d.type === "barrier");

  // ✅ Group barriers by campus_id
  const barriersByCampus = {};
  barrierNodes.forEach(b => {
    const campusId = b.campus_id || "unknown";
    if (!barriersByCampus[campusId]) barriersByCampus[campusId] = [];
    barriersByCampus[campusId].push(b);
  });

  // ✅ Draw one polygon per campus
  Object.entries(barriersByCampus).forEach(([campusId, barriers]) => {
    const barrierCoords = barriers.map(b => [b.latitude, b.longitude]);
    if (barrierCoords.length === 0) return;

    const center = {
      lat: barrierCoords.reduce((sum, c) => sum + c[0], 0) / barrierCoords.length,
      lng: barrierCoords.reduce((sum, c) => sum + c[1], 0) / barrierCoords.length
    };

    const sortedCoords = barrierCoords.slice().sort((a, b) => {
      const angleA = Math.atan2(a[1] - center.lng, a[0] - center.lat);
      const angleB = Math.atan2(b[1] - center.lng, b[0] - center.lat);
      return angleA - angleB;
    });

    const polygon = L.polygon(sortedCoords, {
      color: "green",
      weight: 3,
      fillOpacity: 0.1
    }).addTo(map);

    if (enableClick) {
      polygon.on("click", (e) => {
        showDetails({
          name: `Campus Area (${campusId})`,
          type: "Campus Area",
          latitude: e.latlng.lat.toFixed(6),
          longitude: e.latlng.lng.toFixed(6)
        });
      });

      barriers.forEach(node => {
        const cornerMarker = L.circleMarker([node.latitude, node.longitude], {
          radius: 6,
          color: "darkgreen",
          fillColor: "lightgreen",
          fillOpacity: 0.9
        }).addTo(map);

        // keep track so it can be brought to front after edges draw
        nodeMarkers.push(cornerMarker);

        cornerMarker.on("click", () => showDetails(node));
      });
    }
  });

  // ======================================================
  // 🔹 Build lookup for nodes by node_id (store type too)
  const nodeMap = new Map();
  nodes.forEach(node => {
    if (node.node_id && node.latitude && node.longitude) {
      nodeMap.set(node.node_id, {
        coords: [node.latitude, node.longitude],
        type: node.type
      });
    }
  });

  // 🔹 Render edges (lines between nodes, skip barriers)
  edges.forEach(edge => {
    if (!edge.from_node || !edge.to_node) return;
    const from = nodeMap.get(edge.from_node);
    const to = nodeMap.get(edge.to_node);

    // 🚫 Skip if either endpoint is missing or a barrier
    if (!from || !to || from.type === "barrier" || to.type === "barrier") return;

    // place edges on map early — markers will be brought above afterwards
    const line = L.polyline([from.coords, to.coords], {
      color: "orange",
      weight: 3,
      opacity: 0.8,
      interactive: true
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

  // --- Infrastructure (Buildings) ---
  nodes.filter(d => d.type === "infrastructure").forEach(building => {
    const marker = L.circleMarker([building.latitude, building.longitude], {
      radius: 6,
      color: "red",
      fillColor: "pink",
      fillOpacity: 0.7
    }).addTo(map);

    // ensure marker is above edges
    nodeMarkers.push(marker);

    if (enableClick) {
      marker.on("click", () => showDetails(building));
    }
  });

  // --- Rooms ---
  nodes.filter(d => d.type === "room").forEach(room => {
    const marker = L.marker([room.latitude, room.longitude], { riseOnHover: true }).addTo(map);

    // marker supports z-index offset to keep above polylines
    if (typeof marker.setZIndexOffset === "function") marker.setZIndexOffset(1000);
    nodeMarkers.push(marker);

    if (enableClick) marker.on("click", () => showDetails(room));
  });

  // --- Outdoor Nodes (like pathways, landmarks, etc.) ---
  nodes.filter(d => d.type === "outdoor").forEach(outdoor => {
    const marker = L.circleMarker([outdoor.latitude, outdoor.longitude], {
      radius: 6,
      color: "red",
      fillColor: "pink",
      fillOpacity: 0.8
    }).addTo(map);

    nodeMarkers.push(marker);
    if (enableClick) marker.on("click", () => showDetails(outdoor));
  });

  // --- Intermediate Nodes ---
  nodes.filter(d => d.type === "intermediate").forEach(intermediate => {
    const marker = L.circleMarker([intermediate.latitude, intermediate.longitude], {
      radius: 3,
      color: "black",
      fillColor: "black",
      fillOpacity: 1.0
    }).addTo(map);

    nodeMarkers.push(marker);
    if (enableClick) marker.on("click", () => showDetails(intermediate));
  });

  // Bring all node markers to front so they sit above polylines and receive clicks
  nodeMarkers.forEach(m => {
    try {
      if (typeof m.bringToFront === "function") m.bringToFront();
      if (typeof m.setZIndexOffset === "function") m.setZIndexOffset(1000);
    } catch (e) { /* ignore */ }
  });
}

















async function showDetails(node) {
  const sidebar = document.querySelector(".map-sidebar");

  // QUICK LOADING STATE (prevents empty flash while we fetch)
  sidebar.innerHTML = `
    <div style="display:flex;flex-direction:column;align-items:center;justify-content:center;padding:24px;">
      <div style="width:48px;height:48px;border:5px solid rgba(0,0,0,0.08);border-top-color:#DC143C;border-radius:50%;animation:spin 0.8s linear infinite"></div>
      <div style="margin-top:12px;color:#666;font-weight:600">Loading details...</div>
    </div>
  `;

  // --- Fetch related infrastructure info (image / email / phone) ---
  let imageUrl = null;
  let infraEmail = "-";
  let infraPhone = "-";
  try {
    if (node.related_infra_id) {
      const q = query(collection(db, "Infrastructure"), where("infra_id", "==", node.related_infra_id));
      const snap = await getDocs(q);
      if (!snap.empty) {
        const infra = snap.docs[0].data();
        imageUrl = infra.image_url || null;
        infraEmail = infra.email || infraEmail;
        infraPhone = infra.phone || infraPhone;
      }
    }
  } catch (err) {
    console.warn("Failed to load infrastructure info for sidebar:", err);
  }

  // --- Small inline SVG placeholder so UI stays consistent when there's no image ---
  const placeholderSVG = 'data:image/svg+xml;utf8,' + encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="600" viewBox="0 0 1200 600">
       <rect width="100%" height="100%" fill="#f6f6f8"/>
       <g fill="#d1d3d8" font-family="Inter, Arial, Helvetica, sans-serif" font-size="26" text-anchor="middle">
         <text x="50%" y="50%" dy="0">No image available</text>
       </g>
     </svg>`
  );

  const imgSrc = imageUrl || placeholderSVG;

  // --- Format created_at similar to existing behavior ---
  let createdAtFormatted = "-";
  if (node.created_at) {
    try {
      if (typeof node.created_at.toDate === "function") {
        const d = node.created_at.toDate();
        createdAtFormatted = d.toLocaleString();
      } else if (node.created_at.seconds) {
        const d = new Date(node.created_at.seconds * 1000);
        createdAtFormatted = d.toLocaleString();
      } else {
        createdAtFormatted = String(node.created_at);
      }
    } catch (ex) {
      createdAtFormatted = String(node.created_at);
    }
  }

  // --- Coordinates string (Longitude, Latitude) as requested ---
  const coordText = (node.longitude !== undefined && node.latitude !== undefined && node.longitude !== null && node.latitude !== null)
    ? `${Number(node.longitude).toFixed(6)}, ${Number(node.latitude).toFixed(6)}`
    : "-";

  // --- Active status ---
  const statusHtml = node.is_active
    ? `<span class="status-pill status-active" style="display:inline-flex;align-items:center;gap:6px;background:#e8f7ef;color:#0a7a4a;padding:6px 10px;border-radius:999px;font-weight:600;"><i class="fas fa-check-circle"></i> Active</span>`
    : `<span class="status-pill status-inactive" style="display:inline-flex;align-items:center;gap:6px;background:#fff3f3;color:#a33;padding:6px 10px;border-radius:999px;font-weight:600;"><i class="fas fa-times-circle"></i> Inactive</span>`;

  // --- Build sidebar UI (image -> name -> divider -> coordinates, status, createdAt -> divider -> contact) ---
  sidebar.innerHTML = `
    <div style="padding:12px; display:flex;flex-direction:column;gap:10px;">
      <div style="width:100%;display:flex;justify-content:center;">
        <div style="width:100%;max-width:320px;border-radius:8px;overflow:hidden;box-shadow:0 6px 18px rgba(9,30,66,0.08);position:relative;">
          <img id="sidebar-infra-image" src="${imgSrc}" alt="${(node.name||'Image').replace(/"/g,'')}" style="width:100%;height:220px;object-fit:cover;display:block;background:#f6f6f8" />
          <!-- show rooms link overlay -->
          <a class="show-rooms-link" href="#" style="position:absolute;left:12px;bottom:12px;background:rgba(255,255,255,0.95);padding:6px 10px;border-radius:6px;color:#0f1720;font-weight:600;text-decoration:none;box-shadow:0 2px 6px rgba(2,6,23,0.12);display:inline-flex;align-items:center;gap:8px;">
            <i class="fas fa-th-large" style="color:#374151"></i>
            Show Rooms
          </a>
        </div>
      </div>

      <div style="text-align:center;padding:4px 8px;">
        <h3 style="margin:6px 0;font-size:18px;font-weight:700;color:#111">${node.name || "-"}</h3>
        <div style="color:#57606a;font-size:13px;">${node.node_id ? node.node_id : ""}</div>
      </div>

      <div style="height:1px;background:linear-gradient(90deg,#afafaf,#afafaf);margin:6px 0;border-radius:2px;"></div>

      <div style="display:flex;flex-direction:column;gap:8px;padding:0 6px;">
        <div style="display:flex;align-items:center;gap:10px;">
          <i class="fas fa-map-marker-alt" style="color:#DC143C;width:20px;text-align:center"></i>
          <div style="flex:1">
            <div style="font-size:12px;color:#69717a;font-weight:600">Coordinates</div>
            <div style="font-size:14px;color:#0f1720">${coordText}</div>
          </div>
        </div>

        <div style="display:flex;align-items:center;gap:10px;">
          <i class="fas fa-info-circle" style="color:#2b7a78;width:20px;text-align:center"></i>
          <div style="flex:1">
            <div style="font-size:12px;color:#69717a;font-weight:600">Status</div>
            <div style="font-size:14px">${statusHtml}</div>
          </div>
        </div>

        <div style="display:flex;align-items:center;gap:10px;">
          <i class="fas fa-calendar-alt" style="color:#667085;width:20px;text-align:center"></i>
          <div style="flex:1">
            <div style="font-size:12px;color:#69717a;font-weight:600">Created</div>
            <div style="font-size:14px;color:#0f1720;">${createdAtFormatted}</div>
          </div>
        </div>
      </div>

      <div style="height:1px;background:linear-gradient(90deg,#afafaf,#afafaf);margin:10px 0;border-radius:2px;"></div>

      <div style="display:flex;flex-direction:column;gap:8px;padding:0 6px;">
        <div style="display:flex;align-items:center;gap:10px;">
          <i class="fas fa-envelope" style="color:#475569;width:20px;text-align:center"></i>
          <div style="flex:1">
            <div style="font-size:12px;color:#69717a;font-weight:600">Email</div>
            <div style="font-size:14px;color:#0f1720;word-break:break-word">${infraEmail || "-"}</div>
          </div>
        </div>

        <div style="display:flex;align-items:center;gap:10px;">
          <i class="fas fa-phone" style="color:#475569;width:20px;text-align:center"></i>
          <div style="flex:1">
            <div style="font-size:12px;color:#69717a;font-weight:600">Phone</div>
            <div style="font-size:14px;color:#0f1720">${infraPhone || "-"}</div>
          </div>
        </div>
      </div>

      <div class="qr-section" style="padding-top:12px;display:flex;justify-content:center;"></div>
    </div>
  `;

  // attach show rooms handler (only visible when infra has related_infra_id)
  const showLink = document.querySelector(".map-sidebar .show-rooms-link");
  if (showLink) {
    showLink.addEventListener("click", (ev) => {
      ev.preventDefault();
      // Only open if we have a related_infra_id
      const infraId = node.related_infra_id || null;
      if (!infraId) {
        alert("No related infrastructure recorded for this node.");
        return;
      }
      openRoomsModal({ infra_id: infraId, infra_node: node });
    });
  }

  // re-use existing QR rendering logic
  await renderQrSection(node);
}



















const ROOM_ICON_SVGS = {
  // door / room (Material-style simplified)
  room: `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 24 24" fill="#000000"><g fill="none" stroke="#000000" stroke-linecap="round" stroke-linejoin="round" stroke-width="2"><path d="M5 2h11a3 3 0 0 1 3 3v14a1 1 0 0 1-1 1h-3"/><path d="m5 2l7.588 1.518A3 3 0 0 1 15 6.459V20.78a1 1 0 0 1-1.196.98l-7.196-1.438A2 2 0 0 1 5 18.36V2Zm7 10v2"/></g></svg>`,
  // stairs
  stairs: `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 24 24" fill="#000000"><g fill="none" stroke="#000000" stroke-linecap="round" stroke-linejoin="round" stroke-width="2"><path d="M2 16h10v4H2zm2-4h10v4H4zm2-4h10v4H6zm2-4h10v4H8z"/><path d="M12 20h10V4h-4"/></g></svg>`,
  // exit (door open arrow)
  fire_exit: `<svg width="512" height="512" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 8 8"><path fill="#000000" d="M4 3L3 5h1v3H3V6H2v1H0V6h1l1-3H1L0 4V2h4l1-1l-1-1l-1 1l2 2h1v1H5m2-3H6L5 0h3v8H7"/></svg>`
};

const _roomIconCache = {}; // kind -> HTMLImageElement

function svgToDataUrl(svg) {
  return 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
}

function loadIconImage(kind) {
  return new Promise((resolve) => {
    const key = kind || 'room';
    if (_roomIconCache[key] !== undefined) return resolve(_roomIconCache[key]);
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => { _roomIconCache[key] = img; resolve(img); };
    img.onerror = () => { _roomIconCache[key] = null; resolve(null); };
    img.src = svgToDataUrl(ROOM_ICON_SVGS[key] || ROOM_ICON_SVGS.room);
  });
}

let _roomIconsLoadedPromise = null;
function ensureRoomIconsLoaded() {
  if (_roomIconsLoadedPromise) return _roomIconsLoadedPromise;
  _roomIconsLoadedPromise = Promise.all([
    loadIconImage('room'),
    loadIconImage('stairs'),
    loadIconImage('fire_exit')
  ]).then(() => true).catch(() => true);
  return _roomIconsLoadedPromise;
}

async function openRoomsModal({ infra_id, infra_node = null } = {}) {
  document.querySelectorAll(".rooms-modal, .rooms-modal-backdrop").forEach(n => n.remove());
  await ensureRoomIconsLoaded();

  // try to fetch infra name for header
  let infraName = infra_id;
  try {
    const q = query(collection(db, "Infrastructure"), where("infra_id", "==", infra_id));
    const snap = await getDocs(q);
    if (!snap.empty) infraName = snap.docs[0].data().name || infra_id;
  } catch (e) {
    console.warn("Could not fetch infra name for rooms modal:", e);
  }

  const backdrop = document.createElement("div");
  backdrop.className = "rooms-modal-backdrop";
  backdrop.style = "position:fixed;inset:0;background:rgba(6,18,31,0.45);z-index:10000;display:flex;align-items:center;justify-content:center;";
  document.body.appendChild(backdrop);

  const modal = document.createElement("div");
  modal.className = "rooms-modal";
  modal.style = "background:#fff;border-radius:12px;padding:18px;width:920px;max-width:96%;max-height:86vh;overflow:hidden;box-shadow:0 18px 50px rgba(2,6,23,0.35);display:flex;flex-direction:column;gap:14px;font-family:Inter,Arial,Helvetica,sans-serif;position:relative;";
  modal.innerHTML = `
    <style>
      @keyframes spinCone { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
    </style>

    <div style="display:flex;justify-content:space-between;align-items:center;padding-bottom:6px;border-bottom:1px solid #eef2f7;">
      <div style="display:flex;flex-direction:column;">
        <div style="font-weight:700;color:#0f1720;font-size:16px">Rooms — ${escapeHtml(infraName)}</div>
        <div id="rooms-floor-label" style="color:#6b7280;font-size:13px;margin-top:4px;">Floor • —</div>
      </div>
      <div style="display:flex;align-items:center;gap:8px;">
        <button id="rooms-floor-prev" aria-label="Up (higher floor)" title="Up (higher floor)" style="border:none;background:#fff;border-radius:8px;padding:8px;cursor:pointer;box-shadow:0 2px 6px rgba(2,6,23,0.06)"><i class="fas fa-arrow-up" style="color:#374151"></i></button>
        <button id="rooms-floor-next" aria-label="Down (lower floor)" title="Down (lower floor)" style="border:none;background:#fff;border-radius:8px;padding:8px;cursor:pointer;box-shadow:0 2px 6px rgba(2,6,23,0.06)"><i class="fas fa-arrow-down" style="color:#374151"></i></button>
        <button id="close-rooms-modal" title="Close" style="border:none;background:transparent;color:#556070;padding:6px;cursor:pointer;font-size:18px;"><i class="fas fa-times"></i></button>
      </div>
    </div>

    <div style="display:flex;gap:12px;flex:1;min-height:380px;">
      <div style="flex:1;display:flex;flex-direction:column;gap:10px;align-items:center;justify-content:center;padding-top:6px;">
        <canvas id="rooms-canvas" width="820" height="520" style="background:linear-gradient(180deg,#fcfdff,#fbfcfd);border:1px solid #eef2f7;border-radius:8px;"></canvas>
        <div id="rooms-legend" style="font-size:13px;color:#556070;width:100%;display:flex;justify-content:center;"></div>
      </div>
    </div>

    <!-- Crimson loader overlay (circular spinner, crimson) -->
    <div id="rooms-loading-overlay" style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(255,255,255,0.88);z-index:40;">
      <div style="display:flex;flex-direction:column;align-items:center;gap:10px;">
        <!-- circular spinner (crimson) -->
        <div style="width:48px;height:48px;border-radius:50%;border:5px solid rgba(0,0,0,0.08);border-top-color:#DC143C;box-sizing:border-box;animation:spinCone 0.9s linear infinite;"></div>
        <!-- loading text in black -->
        <div style="color:#000000;font-weight:600">Loading rooms…</div>
      </div>
    </div>
  `;
  backdrop.appendChild(modal);

  modal.querySelector("#close-rooms-modal").addEventListener("click", () => closeRoomsModal());
  backdrop.addEventListener("click", (e) => { if (e.target === backdrop) closeRoomsModal(); });

  const loadingOverlay = modal.querySelector("#rooms-loading-overlay");
  if (loadingOverlay) loadingOverlay.style.display = "flex";

  // collect indoor room nodes that belong to this infrastructure
  let roomNodes = [];
  try {
    const mapsSnap = await getDocs(collection(db, "MapVersions"));
    for (const mapDoc of mapsSnap.docs) {
      const mapData = mapDoc.data();
      const currentVersion = mapData.current_version;
      if (!currentVersion) continue;
      const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
      const versionSnap = await getDoc(versionRef);
      if (!versionSnap.exists()) continue;
      const versionNodes = Array.isArray(versionSnap.data().nodes) ? versionSnap.data().nodes : [];
      versionNodes.forEach(n => {
        const isIndoor = !!(n.indoor && (n.indoor.x !== undefined || n.indoor.y !== undefined || n.indoor.floor !== undefined));
        const matchesInfra = n.related_infra_id && infra_id && String(n.related_infra_id) === String(infra_id);
        if (isIndoor && matchesInfra) roomNodes.push(Object.assign({}, n));
      });
    }
  } catch (err) {
    console.error("Error loading room nodes for Show Rooms modal:", err);
  }

  if (!roomNodes.length) {
    // hide loader only after we've shown the "no rooms" state and drawn canvas
    if (loadingOverlay) loadingOverlay.style.display = "none";
    modal.querySelector("#rooms-legend").textContent = "No room nodes found for this infrastructure.";
    renderRoomsFloor(modal, [], null, infra_node);
    return;
  }

  // load IndoorInfrastructure entries for mapping indoor_type + name
  const indoorMap = {};
  try {
    const indoorSnap = await getDocs(collection(db, "IndoorInfrastructure"));
    indoorSnap.forEach(d => {
      const data = d.data();
      if (data.room_id) indoorMap[String(data.room_id)] = { indoor_type: data.indoor_type || data.type || "", name: data.name || data.room_name || "", infra_id: data.infra_id || null };
    });
  } catch (e) {
    console.warn("Failed to load IndoorInfrastructure map:", e);
  }

  // also load Infrastructure names map (for room infra names)
  const infraMap = {};
  try {
    const infraSnap = await getDocs(collection(db, "Infrastructure"));
    infraSnap.forEach(d => {
      const data = d.data();
      if (data.infra_id) infraMap[String(data.infra_id)] = data.name || data.infra_id;
    });
  } catch (e) {
    console.warn("Failed to load Infrastructure map:", e);
  }

  // enrich roomNodes with kind and display name
  roomNodes = roomNodes.map(n => {
    const relatedRoomId = String(n.related_room_id || n.room_id || "");
    const indoorDoc = indoorMap[relatedRoomId] || {};
    const kind = (indoorDoc.indoor_type || n.indoor?.type || n.type || "").toString().toLowerCase();
    const roomName = indoorDoc.name || n.name || n.room_name || relatedRoomId || "Room";
    const roomInfraName = indoorDoc.infra_id ? (infraMap[String(indoorDoc.infra_id)] || indoorDoc.infra_id) : (infraMap[String(n.related_infra_id)] || n.related_infra_id || "");
    return Object.assign({}, n, { resolved_kind: kind, resolved_room_name: roomName, resolved_infra_name: roomInfraName });
  });

  // floors list
  const floors = Array.from(new Set(roomNodes.map(r => (r.indoor?.floor ?? "0").toString()))).filter(Boolean);
  floors.sort((a,b) => {
    const na = Number(a), nb = Number(b);
    if (!isNaN(na) && !isNaN(nb)) return na - nb;
    return a.localeCompare(b);
  });

  let currentFloorIndex = 0;
  const setFloorIndex = (i) => {
    currentFloorIndex = Math.max(0, Math.min(floors.length - 1, i));
    const floor = floors[currentFloorIndex];
    modal.querySelector("#rooms-floor-label").textContent = `Floor ${floor}`;
    // Up arrow (prev) -> higher floor; Down arrow (next) -> lower floor
    modal.querySelector("#rooms-floor-prev").disabled = (currentFloorIndex === floors.length - 1);
    modal.querySelector("#rooms-floor-next").disabled = (currentFloorIndex === 0);
    const roomsForFloor = roomNodes.filter(r => String(r.indoor?.floor ?? "0") === String(floor));
    renderRoomsFloor(modal, roomsForFloor, floor, infra_node);
  };

  modal.querySelector("#rooms-floor-prev").addEventListener("click", () => setFloorIndex(currentFloorIndex + 1));
  modal.querySelector("#rooms-floor-next").addEventListener("click", () => setFloorIndex(currentFloorIndex - 1));

  // initial render (keep loader visible until rendering completes)
  setFloorIndex(0);

  // hide loader now that rooms are rendered
  if (loadingOverlay) loadingOverlay.style.display = "none";
}

function renderRoomsFloor(modal, rooms, floorLabel, infra_node = null) {
  const canvas = modal.querySelector("#rooms-canvas");
  if (!canvas) return;
  const ctx = canvas.getContext("2d");
  const W = canvas.width;
  const H = canvas.height;
  ctx.clearRect(0,0,W,H);

  // subtle background
  ctx.fillStyle = "#fbfcfd";
  ctx.fillRect(0,0,W,H);

  const pad = 36;
  const drawW = W - pad*2;
  const drawH = H - pad*2;

  const infraOriginX = infra_node?.indoor?.x ? Number(infra_node.indoor.x) : 0;
  const infraOriginY = infra_node?.indoor?.y ? Number(infra_node.indoor.y) : 0;

  const points = rooms.map(r => {
    const rx = (r.indoor?.x !== undefined && r.indoor?.x !== null) ? Number(r.indoor.x) - infraOriginX : 0;
    const ry = (r.indoor?.y !== undefined && r.indoor?.y !== null) ? Number(r.indoor.y) - infraOriginY : 0;
    // prefer resolved_kind/resolved_room_name if present
    const kind = (r.resolved_kind || r.indoor?.type || r.indoor_type || r.type || "").toString().toLowerCase();
    const name = r.resolved_room_name || r.name || r.room_name || (r.related_room_id || r.room_id) || "?";
    const infraName = r.resolved_infra_name || r.related_infra_id || "";
    return { raw: r, id: r.node_id || r.related_room_id || r.room_id || "?", name, infraName, x: rx, y: ry, kind };
  });

  // include origin
  const xs = points.map(p => p.x).concat([0]);
  const ys = points.map(p => p.y).concat([0]);

  const minX = Math.min(...xs);
  const maxX = Math.max(...xs);
  const minY = Math.min(...ys);
  const maxY = Math.max(...ys);

  const rangeX = (maxX - minX) || 1;
  const rangeY = (maxY - minY) || 1;

  const scale = Math.min(drawW / rangeX, drawH / rangeY) * 0.9;
  const offsetX = pad + (drawW - (rangeX * scale)) / 2;
  const offsetY = pad + (drawH - (rangeY * scale)) / 2;

  const worldToCanvas = (x, y) => {
    const cx = offsetX + (x - minX) * scale;
    const cy = offsetY + (maxY - y) * scale;
    return [cx, cy];
  };

  // faint gridlines
  ctx.strokeStyle = "rgba(14,39,75,0.045)";
  ctx.lineWidth = 1;
  const gridPx = 40;
  for (let gx = pad; gx <= W - pad; gx += gridPx) {
    ctx.beginPath();
    ctx.moveTo(gx, pad);
    ctx.lineTo(gx, H - pad);
    ctx.stroke();
  }
  for (let gy = pad; gy <= H - pad; gy += gridPx) {
    ctx.beginPath();
    ctx.moveTo(pad, gy);
    ctx.lineTo(W - pad, gy);
    ctx.stroke();
  }

  // axes
  const [originCx, originCy] = worldToCanvas(0, 0);
  ctx.strokeStyle = "#e6eef6";
  ctx.lineWidth = 1.5;
  ctx.beginPath(); ctx.moveTo(originCx, pad); ctx.lineTo(originCx, H - pad); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(pad, originCy); ctx.lineTo(W - pad, originCy); ctx.stroke();

  // origin marker
  ctx.fillStyle = "#0f1720";
  ctx.beginPath(); ctx.arc(originCx, originCy, 4, 0, Math.PI*2); ctx.fill();
  ctx.fillStyle = "#6b7280";
  ctx.font = "12px Inter, Arial, sans-serif";
  ctx.fillText("0,0", originCx + 8, originCy - 10);

  // render points with icons and labels
  points.forEach(p => {
    const [cx, cy] = worldToCanvas(p.x, p.y);

    const iconSize = Math.max(18, Math.min(40, Math.round(18 + Math.abs(p.x || 0) * 0.01)));
    // draw icon (uses preloaded SVG images)
    drawIconImage(ctx, cx, cy, p.kind, iconSize);

    // label above icon: room name (bold) + infra name (subtle) below it (or below name if space)
    const nameText = p.name || p.id;
    const infraText = p.infraName || "";

    ctx.font = "13px Inter, Arial, sans-serif";
    const nameMetrics = ctx.measureText(nameText);
    const nameW = nameMetrics.width;

    ctx.font = "11px Inter, Arial, sans-serif";
    const infraMetrics = ctx.measureText(infraText);
    const infraW = infraMetrics.width;

    const tw = Math.max(nameW, infraW) + 16;
    const th = infraText ? 36 : 22;

    let tx = cx - tw / 2;
    let ty = cy - (iconSize / 2) - 10 - th;

    tx = Math.max(pad - 6, Math.min(W - pad - tw + 6, tx));
    if (ty < 6) ty = cy + (iconSize / 2) + 8;

    ctx.fillStyle = "rgba(255,255,255,0.95)";
    roundRect(ctx, tx, ty, tw, th, 6, true, false);

    // draw name
    ctx.fillStyle = "#0f1720";
    ctx.font = "13px Inter, Arial, sans-serif";
    ctx.fillText(nameText, tx + 8, ty + 16);

    if (infraText) {
      ctx.fillStyle = "#6b7280";
      ctx.font = "11px Inter, Arial, sans-serif";
      ctx.fillText(infraText, tx + 8, ty + 30);
    }
  });

  // legend
  const legend = modal.querySelector("#rooms-legend");
  if (legend) {
    legend.innerHTML = `<div style="display:flex;gap:18px;align-items:center;color:#556070;">
      <div style="display:flex;align-items:center;gap:8px;"><img src="${svgToDataUrl(ROOM_ICON_SVGS.room)}" style="width:18px;height:18px;object-fit:contain;border-radius:3px;"/> Room</div>
      <div style="display:flex;align-items:center;gap:8px;"><img src="${svgToDataUrl(ROOM_ICON_SVGS.stairs)}" style="width:18px;height:18px;object-fit:contain;border-radius:3px;"/> Stairs</div>
      <div style="display:flex;align-items:center;gap:8px;"><img src="${svgToDataUrl(ROOM_ICON_SVGS.fire_exit)}" style="width:18px;height:18px;object-fit:contain;border-radius:3px;"/> Exit</div>
      <div style="margin-left:8px;color:#8b949e;font-size:12px;">${points.length} room(s) — Floor ${floorLabel ?? "-"}</div>
    </div>`;
  }
}

function drawIconImage(ctx, cx, cy, kind = "", size = 18) {
  const k = (kind || "").toLowerCase();
  const key = (k.includes("stair") || k.includes("stairs")) ? "stairs" : (k.includes("fire") || k.includes("exit") ? "fire_exit" : "room");
  const img = _roomIconCache[key];

  if (img && img.width) {
    // draw centered and keep aspect
    const targetW = size;
    const targetH = Math.round(img.height / img.width * targetW);
    ctx.drawImage(img, cx - targetW/2, cy - targetH/2, targetW, targetH);
    return;
  }

  // fallback small colored circle
  ctx.save();
  ctx.beginPath();
  ctx.fillStyle = key === "stairs" ? "#B45309" : (key === "fire_exit" ? "#DC2626" : "#2563EB");
  ctx.arc(cx, cy, 6, 0, Math.PI*2);
  ctx.fill();
  ctx.restore();
}

// small helper: escape simple HTML used in modal header
function escapeHtml(str) {
  if (!str) return "";
  return String(str).replace(/&/g, "&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;");
}

// rounded rect helper
function roundRect(ctx, x, y, w, h, r, fill, stroke) {
  if (typeof r === 'undefined') r = 5;
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
  if (fill) { ctx.fillStyle = ctx.fillStyle || "#fff"; ctx.fill(); }
  if (stroke) ctx.stroke();
}

function closeRoomsModal() {
  document.querySelectorAll(".rooms-modal, .rooms-modal-backdrop").forEach(n => n.remove());
}





























async function renderQrSection(node) {
  const qrSection = document.querySelector(".map-sidebar .qr-section");
  if (!qrSection) return;

  // compact loader
  qrSection.innerHTML = `<div style="display:flex;flex-direction:column;align-items:center;gap:8px;padding:8px;">
    <div style="width:36px;height:36px;border:4px solid rgba(0,0,0,0.08);border-top-color:#007bff;border-radius:50%;animation:spin 0.8s linear infinite"></div>
    <div style="color:#556070;font-size:13px;">Checking QR...</div>
  </div>`;

  const crimsonNodeId = `CRIMSON_${node.node_id}`;
  const qrRef = doc(db, "NodeQRCodes", crimsonNodeId);
  const qrSnap = await getDoc(qrRef);
  const hasQR = qrSnap.exists();
  const lastGenerated = hasQR
    ? (qrSnap.data().last_generated
        ? (new Date(qrSnap.data().last_generated.seconds ? qrSnap.data().last_generated.seconds * 1000 : qrSnap.data().last_generated).toLocaleString())
        : "-")
    : "-";

  // build clean professional QR card WITHOUT an auto-displayed QR container.
  // QR will only appear in the modal when user clicks Generate or View.
  qrSection.innerHTML = `
    <div style="width:100%;max-width:300px;border-radius:8px;padding:12px;background:#fff;border:1px solid #eef2f7;box-shadow:0 6px 18px rgba(9,30,66,0.04);text-align:center;">
      <div style="font-size:13px;color:#394152;font-weight:700;margin-bottom:8px;">QR Code</div>



      <div style="margin-top:4px;color:#6b7280;font-size:12px;">${crimsonNodeId}<br><small>Last: ${lastGenerated}</small></div>

      <div style="display:flex;gap:8px;justify-content:center;margin-top:12px;">
        <button class="btn-generate-qr" style="background:#007bff;color:#fff;border:none;padding:8px 10px;border-radius:6px;cursor:pointer;font-weight:600;">
          <i class="fas fa-qrcode" style="margin-right:6px"></i> Generate
        </button>
        ${hasQR ? `<button class="btn-view-qr" style="background:#fff;border:1px solid #d1d5db;padding:8px 10px;border-radius:6px;cursor:pointer;font-weight:600;"><i class="fas fa-eye" style="margin-right:6px"></i> View</button>` : ''}
      </div>
    </div>
  `;

  // handlers (QR rendering only when user clicks)
  const genBtn = qrSection.querySelector(".btn-generate-qr");
  genBtn.addEventListener("click", async () => {
    try {
      const qrDiv = document.createElement("div");
      new QRCode(qrDiv, {
        text: crimsonNodeId,
        width: 512,
        height: 512,
        correctLevel: QRCode.CorrectLevel.H
      });
      await new Promise(res => setTimeout(res, 80)); // allow render
      const canvas = qrDiv.querySelector("canvas") || qrDiv.querySelector("img");
      const dataUrl = canvas.toDataURL("image/png");

      // show modal with the generated QR (only shown on click)
      openQrModal(dataUrl, crimsonNodeId, canvas);

      // save minimal info to Firestore
      await setDoc(qrRef, {
        node_id: crimsonNodeId,
        last_generated: new Date(),
        node_name: node.name || "-"
      }, { merge: true });

      // refresh section (still will NOT auto-display QR)
      await renderQrSection(node);
    } catch (err) {
      console.error("QR generate error:", err);
      alert("Failed to generate QR");
    }
  });

  const viewBtn = qrSection.querySelector(".btn-view-qr");
  if (viewBtn) {
    viewBtn.addEventListener("click", async () => {
      try {
        const qrDiv = document.createElement("div");
        new QRCode(qrDiv, {
          text: crimsonNodeId,
          width: 512,
          height: 512,
          correctLevel: QRCode.CorrectLevel.H
        });
        await new Promise(res => setTimeout(res, 80));
        const canvas = qrDiv.querySelector("canvas") || qrDiv.querySelector("img");
        const dataUrl = canvas.toDataURL("image/png");
        openQrModal(dataUrl, crimsonNodeId, canvas);
      } catch (err) {
        console.error(err);
      }
    });
  }
}

// ...existing code...
function openQrModal(qrDataUrl, nodeId, canvas = null) {
  // remove existing modal if any
  const existing = document.querySelector(".qr-modal");
  if (existing) existing.remove();

  const modal = document.createElement("div");
  modal.className = "qr-modal";
  modal.style.position = "fixed";
  modal.style.inset = "0";
  modal.style.zIndex = "9999";
  modal.style.display = "flex";
  modal.style.alignItems = "center";
  modal.style.justifyContent = "center";
  modal.style.background = "rgba(6,18,31,0.45)";

  modal.innerHTML = `
    <div style="background:#fff;border-radius:10px;padding:18px;min-width:320px;max-width:720px;width:92%;box-shadow:0 10px 40px rgba(2,6,23,0.35);display:flex;flex-direction:column;gap:12px;align-items:center;">
      <div style="width:100%;display:flex;justify-content:space-between;align-items:center;">
        <div style="font-weight:700;color:#0f1720">${nodeId}</div>
        <button class="qr-close" style="background:transparent;border:none;font-size:20px;cursor:pointer;color:#556070;"><i class="fas fa-times"></i></button>
      </div>

      <div style="background:#fafafa;padding:12px;border-radius:8px;">
        <img src="${qrDataUrl}" alt="${nodeId}" style="width:320px;height:320px;object-fit:contain;display:block;"/>
      </div>

      <div style="display:flex;gap:10px;justify-content:center;width:100%;">
        <button id="download-qr-btn" style="background:#007bff;color:#fff;border:none;padding:10px 14px;border-radius:8px;cursor:pointer;font-weight:700;"><i class="fas fa-download" style="margin-right:8px"></i> Download PNG</button>
        <button id="print-qr-btn" style="background:#fff;border:1px solid #d1d5db;padding:10px 14px;border-radius:8px;cursor:pointer;font-weight:700;color:#111;"><i class="fas fa-print" style="margin-right:8px"></i> Print</button>
        <button id="pdf-qr-btn" style="background:#111;color:#fff;border:none;padding:10px 14px;border-radius:8px;cursor:pointer;font-weight:700;"><i class="fas fa-file-pdf" style="margin-right:8px"></i> Export PDF</button>
      </div>
    </div>
  `;

  document.body.appendChild(modal);

  modal.querySelector(".qr-close").addEventListener("click", () => modal.remove());
  modal.addEventListener("click", (e) => { if (e.target === modal) modal.remove(); });

  modal.querySelector("#download-qr-btn").addEventListener("click", () => {
    const a = document.createElement("a");
    a.href = qrDataUrl;
    a.download = `${nodeId}_qr.png`;
    a.click();
  });

  modal.querySelector("#print-qr-btn").addEventListener("click", () => {
    const w = window.open("");
    w.document.write(`<img src="${qrDataUrl}" style="width:512px;height:512px;">`);
    w.document.close();
    w.focus();
    w.print();
    w.close();
  });

  modal.querySelector("#pdf-qr-btn").addEventListener("click", () => {
    if (!canvas) return alert("Cannot export PDF because QR canvas not available.");
    const imgData = (canvas.tagName === "CANVAS") ? canvas.toDataURL("image/png") : canvas.src;
    const { jsPDF } = window.jspdf;
    const pdf = new jsPDF({ unit: "px", format: "a4" });
    // center 300x300 at top
    pdf.addImage(imgData, "PNG", 60, 60, 420, 420);
    pdf.save(`${nodeId}_qr.pdf`);
  });
}

















