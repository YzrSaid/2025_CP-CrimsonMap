// ======================= FIREBASE SETUP ===========================
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
import {
    getFirestore, collection, addDoc, getDocs, query, orderBy, where, updateDoc, doc, getDoc, arrayUnion, writeBatch, deleteField, setDoc
} from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";
import { firebaseConfig } from "./../firebaseConfig.js";

const app = initializeApp(firebaseConfig);
const db = getFirestore(app);


// ======================= NODE SECTION =============================

// ----------- Modal Controls -----------
function showNodeModal() {
    document.getElementById('addNodeModal').style.display = 'flex';
    generateNextNodeId();
    populateInfraDropdown();
    populateRoomDropdown();
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
    const q = query(collection(db, "Nodes"));
    const snapshot = await getDocs(q);

    let maxNum = 0;
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.node_id) {
            const num = parseInt(data.node_id.replace("ND-", ""));
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
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.infra_id && data.name) {
            const option = document.createElement("option");
            option.value = data.infra_id;
            option.textContent = data.name;
            select.appendChild(option);
        }
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









// ----------- Load Nodes Table from Current Active Map/Campus/Version -----------
async function renderNodesTable() {
    const tbody = document.querySelector(".nodetbl tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        let nodes = [];
        let infra = [];
        let rooms = [];
        let campuses = [];

        if (navigator.onLine) {
            // 🔹 Online: Firestore
            const [infraSnap, roomSnap, campusSnap, mapVersionsSnap] = await Promise.all([
                getDocs(collection(db, "Infrastructure")),
                getDocs(collection(db, "Rooms")),
                getDocs(collection(db, "Campus")),
                getDocs(collection(db, "MapVersions"))
            ]);

            infra = infraSnap.docs.map(d => d.data());
            rooms = roomSnap.docs.map(d => d.data());
            campuses = campusSnap.docs.map(d => d.data());

            // Collect nodes from current versions
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
            // 🔹 Offline: JSON fallback
            const [nodesRes, infraRes, roomsRes, campusesRes] = await Promise.all([
                fetch("../assets/firestore/MapVersions.json"),
                fetch("../assets/firestore/Infrastructure.json"),
                fetch("../assets/firestore/Rooms.json"),
                fetch("../assets/firestore/Campus.json")
            ]);

            const mapVersions = await nodesRes.json();
            infra = (await infraRes.json()).filter(i => !i.is_deleted);
            rooms = (await roomsRes.json()).filter(r => !r.is_deleted);
            campuses = (await campusesRes.json()).filter(c => !c.is_deleted);

            // Collect nodes from current versions
            for (const mapData of mapVersions) {
                const currentCampus = mapData.current_active_campus;
                const currentVersion = mapData.current_version;
                if (!currentCampus || !currentVersion) continue;

                const version = mapData.versions.find(v => v.id === currentVersion);
                if (!version) continue;

                const mapNodes = Array.isArray(version.nodes) ? version.nodes : [];
                nodes.push(...mapNodes.filter(n => !n.is_deleted && n.campus_id === currentCampus));
            }

            console.log("📂 Offline → Nodes, Infra, Rooms, Campuses loaded from JSON");
        }

        // 🔹 Build lookup maps
        const infraMap = Object.fromEntries(infra.map(i => [i.infra_id, i.name]));
        const roomMap = Object.fromEntries(rooms.map(r => [r.room_id, r.name]));
        const campusMap = Object.fromEntries(campuses.map(c => [c.campus_id, c.campus_name]));

        // 🔹 Sort nodes by created_at
        nodes.sort((a, b) => {
            if (!a.created_at || !b.created_at) return 0;
            return (a.created_at.seconds || 0) - (b.created_at.seconds || 0);
        });

        // 🔹 Render nodes
        nodes.forEach(data => {
            const coords = (data.latitude && data.longitude) ? `${data.latitude}, ${data.longitude}` : "-";
            const infraName = data.related_infra_id ? (infraMap[data.related_infra_id] || data.related_infra_id) : "-";
            const roomName = data.related_room_id ? (roomMap[data.related_room_id] || data.related_room_id) : "-";
            const campusName = data.campus_id ? (campusMap[data.campus_id] || data.campus_id) : "-";

            let indoorOutdoor = "Outdoor";
            if (data.indoor) {
                indoorOutdoor = `Indoor (Floor: ${data.indoor.floor || "-"}, X: ${data.indoor.x || "-"}, Y: ${data.indoor.y || "-"})`;
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

        setupNodeDeleteHandlers(); // ✅ Keep delete modal functionality

    } catch (err) {
        console.error("Error loading nodes:", err);
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



let pendingNodeData = null; // store node temporarily before user chooses

// ----------- Add Node Handler -----------
document.getElementById("nodeForm").addEventListener("submit", async (e) => {
    e.preventDefault();

    // Gather node data
    const nodeId = document.getElementById("nodeId").value;
    const nodeName = document.getElementById("nodeName").value;
    const latitude = parseFloat(document.getElementById("latitude").value);
    const longitude = parseFloat(document.getElementById("longitude").value);
    const typeEl = document.getElementById("nodeType");
    const type = typeEl ? typeEl.value : "";
    const relatedInfraId = document.getElementById("relatedInfra").value;
    const relatedRoomId = document.getElementById("relatedRoom").value;
    const isIndoor = document.getElementById("indoorCheckbox").checked;
    let indoor = null;
    if (isIndoor) {
        indoor = {
            floor: document.getElementById("floor").value,
            x: parseFloat(document.getElementById("xCoord").value) || 0,
            y: parseFloat(document.getElementById("yCoord").value) || 0
        };
    }
    const campusId = document.getElementById("campusDropdown").value;

    // Cartesian conversion
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

    // Save pending node
    pendingNodeData = {
        node_id: nodeId,
        name: nodeName,
        latitude,
        longitude,
        x_coordinate: x,
        y_coordinate: y,
        type,
        related_infra_id: relatedInfraId,
        related_room_id: relatedRoomId,
        indoor,
        is_active: true,
        campus_id: campusId,
        created_at: new Date()
    };

    // Show modal
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
            // Find if node with same node_id already exists
            const updatedNodes = oldNodes.filter(n => n.node_id !== pendingNodeData.node_id);

            // Add the new/updated node
            updatedNodes.push(pendingNodeData);

            // Update the nodes array
            await updateDoc(versionRef, {
                nodes: updatedNodes
            });

            alert(`Node ${pendingNodeData.node_id} added/updated in current version ${currentVersion}`);
        }

        else if (option === "newVersion") {
            // --- Compute new version string ---
            let [major, minor, patch] = currentVersion.slice(1).split(".").map(Number);
            if (patch < 99) {
                patch += 1;
            } else {
                patch = 0;
                minor += 1;
            }
            const newVersion = `v${major}.${minor}.${patch}`;

            // --- Migrate all old nodes & edges + add new node to new version ---
            const migratedNodes = oldNodes.map(n => ({ ...n }));  // deep copy
            migratedNodes.push({ ...pendingNodeData });           // add new node

            const migratedEdges = oldEdges.map(e => ({ ...e }));  // deep copy edges

            // --- Save to new version document ---
            await setDoc(doc(db, "MapVersions", mapDocId, "versions", newVersion), {
                nodes: migratedNodes,
                edges: migratedEdges
            });

            // --- Update current_version of map ---
            await updateDoc(doc(db, "MapVersions", mapDocId), { current_version: newVersion });

            alert(`New version created: ${newVersion} with migrated nodes and edges`);
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









// ----------- Edit Node Modal Open Handler -----------
document.querySelector(".nodetbl").addEventListener("click", async (e) => {
    if (!e.target.classList.contains("fa-edit")) return;

    const row = e.target.closest("tr");
    if (!row) return;

    const nodeId = row.querySelector("td")?.textContent?.trim();
    if (!nodeId) return;

    try {
        // Fetch node data from Firestore
        const nodesQ = query(collection(db, "Nodes"), where("node_id", "==", nodeId));
        const snap = await getDocs(nodesQ);

        if (snap.empty) {
            alert("Node not found in Firestore");
            return;
        }

        const docSnap = snap.docs[0];
        const nodeData = docSnap.data();

        // Populate dropdowns for edit modal
        // Populate dropdowns for edit modal, then set value
        await populateInfraDropdown("editRelatedInfra");
        document.getElementById("editRelatedInfra").value = nodeData.related_infra_id ?? "";

        await populateRoomDropdown("editRelatedRoom");
        document.getElementById("editRelatedRoom").value = nodeData.related_room_id ?? "";

        await populateCampusDropdown("editCampusDropdown");
        document.getElementById("editCampusDropdown").value = nodeData.campus_id ?? "";

        // Set values
        document.getElementById("editNodeId").value = nodeData.node_id ?? "";
        document.getElementById("editNodeIdHidden").value = nodeData.node_id ?? "";
        document.getElementById("editNodeName").value = nodeData.name ?? "";
        document.getElementById("editLatitude").value = nodeData.latitude ?? "";
        document.getElementById("editLongitude").value = nodeData.longitude ?? "";

        // Type (handle custom input for "other")
        let typeSelect = document.getElementById("editNodeType");
        if (
            ["room", "barrier", "infrastructure"].includes(nodeData.type)
        ) {
            // Restore select if needed
            if (typeSelect.tagName !== "SELECT") {
                const parent = typeSelect.parentNode;
                const newSelect = document.createElement("select");
                newSelect.id = "editNodeType";
                newSelect.innerHTML = `
                    <option value="">Select type</option>
                    <option value="room">Room</option>
                    <option value="barrier">Barrier</option>
                    <option value="infrastructure">Infrastructure</option>
                `;
                parent.replaceChild(newSelect, typeSelect);
                typeSelect = newSelect;
            }
            typeSelect.value = nodeData.type;
        } else if (nodeData.type) {
            // Custom type
            if (typeSelect.tagName === "SELECT") {
                // Replace select with input
                const input = document.createElement("input");
                input.type = "text";
                input.id = "editNodeType";
                input.value = nodeData.type;
                input.classList.add("custom-input");
                typeSelect.parentNode.replaceChild(input, typeSelect);
            } else {
                typeSelect.value = nodeData.type;
            }
        } else {
            if (typeSelect.tagName === "SELECT") typeSelect.value = "";
            else typeSelect.value = "";
        }

        // Related infra/room
        document.getElementById("editRelatedInfra").value = nodeData.related_infra_id ?? "";
        document.getElementById("editRelatedRoom").value = nodeData.related_room_id ?? "";

        // Indoor/Outdoor
        const indoorCheckbox = document.getElementById("editIndoorCheckbox");
        const outdoorCheckbox = document.getElementById("editOutdoorCheckbox");
        const indoorDetails = document.getElementById("editIndoorDetails");
        if (nodeData.indoor) {
            indoorCheckbox.checked = true;
            outdoorCheckbox.checked = false;
            indoorDetails.style.display = "block";
            document.getElementById("editFloor").value = nodeData.indoor.floor ?? "";
            document.getElementById("editXCoord").value = nodeData.indoor.x ?? "";
            document.getElementById("editYCoord").value = nodeData.indoor.y ?? "";
        } else {
            indoorCheckbox.checked = false;
            outdoorCheckbox.checked = true;
            indoorDetails.style.display = "none";
            document.getElementById("editFloor").value = "";
            document.getElementById("editXCoord").value = "";
            document.getElementById("editYCoord").value = "";
        }

        // Campus
        document.getElementById("editCampusDropdown").value = nodeData.campus_id ?? "";

        // Show modal
        document.getElementById("editNodeModal").style.display = "flex";
        // Store docId for update
        document.getElementById("editNodeForm").dataset.docId = docSnap.id;

    } catch (err) {
        console.error("Error opening edit modal:", err);
    }
});





// ----------- Edit Node Save Handler -----------
document.getElementById("editNodeForm").addEventListener("submit", async (e) => {
    e.preventDefault();

    const form = e.target;
    const docId = form.dataset.docId;
    if (!docId) {
        alert("No document ID found for update");
        return;
    }

    const nodeId = document.getElementById("editNodeIdHidden").value;
    const nodeName = document.getElementById("editNodeName").value;

    // ✅ Parse to float so they save as Firestore numbers
    const latitude = parseFloat(document.getElementById("editLatitude").value);
    const longitude = parseFloat(document.getElementById("editLongitude").value);

    // Type: could be select or input
    let typeEl = document.getElementById("editNodeType");
    let type = typeEl ? typeEl.value : "";
    if (!type && typeEl && typeEl.tagName === "INPUT") {
        type = typeEl.value;
    }

    const relatedInfraId = document.getElementById("editRelatedInfra").value;
    const relatedRoomId = document.getElementById("editRelatedRoom").value;

    // Indoor/Outdoor
    const isIndoor = document.getElementById("editIndoorCheckbox").checked;
    let indoor = null;
    if (isIndoor) {
        indoor = {
            floor: document.getElementById("editFloor").value,
            x: parseFloat(document.getElementById("editXCoord").value) || 0,
            y: parseFloat(document.getElementById("editYCoord").value) || 0
        };
    }

    const campusId = document.getElementById("editCampusDropdown").value;

    try {
        const nodeRef = doc(db, "Nodes", docId);

        await updateDoc(nodeRef, {
            node_id: nodeId,
            name: nodeName,
            latitude: latitude,   // ✅ now number
            longitude: longitude, // ✅ now number
            type: type,
            related_infra_id: relatedInfraId,
            related_room_id: relatedRoomId,
            indoor: indoor,
            is_active: true,
            campus_id: campusId,
            updated_at: new Date()
        });

        alert("Node updated!");
        document.getElementById("editNodeModal").style.display = "none";
        renderNodesTable();

    } catch (err) {
        console.error("Error updating node:", err);
        alert("Error updating node: " + err.message);
    }
});












































// ======================= EDGE SECTION =============================

// ----------- Auto-Increment Edge ID -----------
async function generateNextEdgeId() {
    const q = query(collection(db, "Edges"));
    const snapshot = await getDocs(q);

    let maxNum = 0;
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.edge_id) {
            const num = parseInt(data.edge_id.replace("EDG-", ""));
            if (!isNaN(num) && num > maxNum) maxNum = num;
        }
    });

    const nextId = `EDG-${String(maxNum + 1).padStart(3, "0")}`;
    document.querySelector("#addEdgeModal input[type='text']").value = nextId;
    return nextId;
}

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

        let mapDoc, mapDocId, mapData;

        // If you already store active map globally, you can simplify:
        const mapsSnapshot = await getDocs(collection(db, "MapVersions"));
        mapDoc = mapsSnapshot.docs.find(d => d.data().current_active_map === d.id);

        if (!mapDoc) {
            alert("No active map found.");
            return;
        }

        mapDocId = mapDoc.id;
        mapData = mapDoc.data();
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
            alert(`Edge added to current version ${currentVersion}`);
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

        renderEdgesTable();
        pendingEdgeData = null;

    } catch (err) {
        console.error(err);
        alert("Error saving edge: " + err);
    }
}





// ----------- Load Edges Table from Current Active Map/Campus/Version -----------
async function renderEdgesTable() {
    const tbody = document.querySelector(".edgetbl tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        let edges = [];
        let mapVersions = [];

        if (navigator.onLine) {
            // 🔹 Online: Firestore
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
                const mapEdges = Array.isArray(versionData.edges) ? versionData.edges : [];
                edges.push(
                    ...mapEdges.filter(e => {
                        if (e.is_deleted) return false;
                        if (e.campus_id) return e.campus_id === currentCampus;
                        return mapData.campus_included?.includes(currentCampus);
                    })
                );
            }
        } else {
            // 🔹 Offline: JSON fallback
            const mapRes = await fetch("../assets/firestore/MapVersions.json");
            mapVersions = (await mapRes.json()).filter(m => !m.is_deleted);

            for (const mapData of mapVersions) {
                const currentCampus = mapData.current_active_campus;
                const currentVersion = mapData.current_version;
                if (!currentCampus || !currentVersion) continue;

                const version = mapData.versions.find(v => v.id === currentVersion);
                if (!version) continue;

                const mapEdges = Array.isArray(version.edges) ? version.edges : [];
                edges.push(
                    ...mapEdges.filter(e => {
                        if (e.is_deleted) return false;
                        if (e.campus_id) return e.campus_id === currentCampus;
                        return mapData.campus_included?.includes(currentCampus);
                    })
                );
            }

            console.log("📂 Offline → Edges loaded from JSON");
        }

        // 🔹 Sort edges by created_at
        edges.sort((a, b) => {
            if (!a.created_at || !b.created_at) return 0;
            return (a.created_at.seconds || 0) - (b.created_at.seconds || 0);
        });

        // 🔹 Render edges
        edges.forEach(data => {
            const formatText = (value) => {
                if (!value) return "-";
                return value
                    .toString()
                    .split("_")
                    .map(word => word.charAt(0).toUpperCase() + word.slice(1))
                    .join(" ");
            };

            const formattedPathType = formatText(data.path_type);
            const formattedElevation = formatText(data.elevations);

            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${data.edge_id}</td>
                <td>${data.from_node} → ${data.to_node}</td>
                <td>${data.distance || "-"}</td>
                <td>${formattedPathType}</td>
                <td>${formattedElevation}</td>
                <td class="actions">
                    <i class="fas fa-edit"></i>
                    <i class="fas fa-trash" data-id="${data.edge_id}"></i>
                </td>
            `;
            tbody.appendChild(tr);
        });

        setupEdgeDeleteHandlers(); // ✅ Keep delete modal functionality

    } catch (err) {
        console.error("Error loading edges:", err);
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

// ----------- Edit Edge Modal Open Handler -----------
document.querySelector(".edgetbl").addEventListener("click", async (e) => {
    if (e.target.classList.contains("fa-edit")) {
        const tr = e.target.closest("tr");
        const edgeId = tr.children[0].textContent;

        // Get edge data from Firestore
        const q = query(collection(db, "Edges"), where("edge_id", "==", edgeId));
        const snapshot = await getDocs(q);

        if (!snapshot.empty) {
            const docSnap = snapshot.docs[0];
            const data = docSnap.data();

            document.getElementById("editEdgeId").value = data.edge_id;

            await loadNodesDropdownsForEditEdge(data.from_node, data.to_node);

            handlePreselectOrCustom("editPathType", data.path_type);
            handlePreselectOrCustom("editElevation", data.elevations);

            document.getElementById("editEdgeModal").dataset.docId = docSnap.id;
            document.getElementById("editEdgeModal").style.display = "flex";
        }
    }
});

// ----------- Load Nodes into Edit Edge Dropdowns -----------
async function loadNodesDropdownsForEditEdge(selectedFrom, selectedTo) {
    const startNodeSelect = document.getElementById("editStartNode");
    const endNodeSelect = document.getElementById("editEndNode");

    startNodeSelect.innerHTML = `<option value="">Select start node</option>`;
    endNodeSelect.innerHTML = `<option value="">Select end node</option>`;

    const q = query(collection(db, "Nodes"), orderBy("created_at", "asc"));
    const snapshot = await getDocs(q);

    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.node_id) {
            const option1 = document.createElement("option");
            option1.value = data.node_id;
            option1.textContent = `${data.node_id} - ${data.name}`;
            if (data.node_id === selectedFrom) option1.selected = true;
            startNodeSelect.appendChild(option1);

            const option2 = document.createElement("option");
            option2.value = data.node_id;
            option2.textContent = `${data.node_id} - ${data.name}`;
            if (data.node_id === selectedTo) option2.selected = true;
            endNodeSelect.appendChild(option2);
        }
    });
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

// ----------- Edit Edge Save Handler -----------
document.querySelector("#editEdgeModal form").addEventListener("submit", async (e) => {
    e.preventDefault();

    const docId = document.getElementById("editEdgeModal").dataset.docId;

    // Grab either select OR input value
    const getFieldValue = (id) => {
        const select = document.getElementById(id);
        const input = document.querySelector(`input[name='${id}']`);
        return select ? select.value.trim() : (input ? input.value.trim() : "");
    };

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
        await updateDoc(doc(db, "Edges", docId), updatedData);

        alert("Edge updated!");
        document.getElementById("editEdgeModal").style.display = "none";
        renderEdgesTable();
    } catch (err) {
        alert("Error updating edge: " + err);
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
    renderNodesTable();
    renderEdgesTable();
};

// ----------- Tab & Modal Controls -----------
document.addEventListener("DOMContentLoaded", function () {
    const tabs = document.querySelectorAll(".top-tabs .tab");
    const tables = document.querySelectorAll(".bottom-tbl > div");
    const addButton = document.querySelector(".addnode .add-btn");

    // Modals
    const addNodeModal = document.getElementById("addNodeModal");
    const addEdgeModal = document.getElementById("addEdgeModal");

    const cancelNodeBtn = document.querySelector("#addNodeModal .cancel-btn");
    const cancelEdgeBtn = document.querySelector("#addEdgeModal .cancel-btn");

    // Button text mapping
    const buttonTexts = [
        "Add Node",
        "Add Edge"
    ];

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



// Show/hide indoor details based on checkbox
document.addEventListener("DOMContentLoaded", function() {
    const indoorCheckbox = document.getElementById("indoorCheckbox");
    const outdoorCheckbox = document.getElementById("outdoorCheckbox");
    const indoorDetails = document.getElementById("indoorDetails");

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
            // 🔹 Online: Firestore → use doc.id
            const mapsSnap = await getDocs(collection(db, "MapVersions"));
            mapsData = mapsSnap.docs.map(doc => ({ id: doc.id, ...doc.data() }));
        } else {
            // 🔹 Offline: JSON fallback
            const res = await fetch("../assets/firestore/MapVersions.json");
            mapsData = await res.json();
            console.log("📂 Offline → Maps loaded from JSON");
        }

        // Populate map select options
        mapsData.forEach(map => {
            const option = document.createElement("option");
            option.value = map.id;  // ✅ Always use Firestore doc.id
            option.textContent = `${map.map_name || map.id} (${map.id})`;
            mapSelect.appendChild(option);
        });

        // Select first map as default
        if (mapsData.length > 0) {
            const firstMapId = mapsData[0].id;  // ✅ Firestore doc.id
            mapSelect.value = firstMapId;
            await setActiveMap(firstMapId);
            await populateCampuses(firstMapId);
            await populateVersions(firstMapId);

            // 🔹 Initial load of tables + map
            await renderNodesTable();
            await renderEdgesTable();
            await loadMap(firstMapId);   // ✅ FIX: use firstMapId
        }

        // 🔹 When Map changes
        mapSelect.addEventListener("change", async () => {
            const selectedMapId = mapSelect.value;  // ✅ Firestore doc.id
            if (!selectedMapId) return;

            await setActiveMap(selectedMapId);
            await populateCampuses(selectedMapId);
            await populateVersions(selectedMapId);

            // 🔹 Refresh tables + map
            await renderNodesTable();
            await renderEdgesTable();
            await loadMap(selectedMapId);  // ✅ FIX: use selectedMapId
        });

    } catch (err) {
        console.error("Error loading maps:", err);
    }
}


// ----------- Populate Campuses -----------
async function populateCampuses(mapId) {
    const campusSelect = document.getElementById("campusSelect");
    campusSelect.innerHTML = '<option value="">Select Campus</option>';

    const mapDocRef = doc(db, "MapVersions", String(mapId)); // ✅ enforce doc.id
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

    if (currentCampus && campuses.includes(currentCampus)) {
        campusSelect.value = currentCampus;
    } else if (campuses.length > 0) {
        campusSelect.value = campuses[0];
        await updateDoc(mapDocRef, { current_active_campus: campuses[0] });
    }

    // 🔹 Update Firestore + refresh tables when campus changes
    campusSelect.addEventListener("change", async () => {
        const selectedCampus = campusSelect.value;
        if (!selectedCampus) return;
        try {
            await updateDoc(mapDocRef, { current_active_campus: selectedCampus });
            console.log(`Current active campus updated to ${selectedCampus}`);

            await renderNodesTable();
            await renderEdgesTable();
            await loadMap(mapId); 
        } catch (err) {
            console.error("Error updating current campus:", err);
        }
    });
}

// ----------- Populate Versions -----------
async function populateVersions(mapId) {
    const versionSelect = document.getElementById("versionSelect");
    versionSelect.innerHTML = '<option value="">Select Version</option>';

    const mapDocRef = doc(db, "MapVersions", String(mapId)); // ✅ enforce doc.id
    const mapDocSnap = await getDoc(mapDocRef);
    if (!mapDocSnap.exists()) return;

    const mapData = mapDocSnap.data();
    const currentVersion = mapData.current_version || "";

    const versionsSnap = await getDocs(collection(db, "MapVersions", String(mapId), "versions"));

    versionsSnap.forEach(docSnap => {
        const version = docSnap.id;
        const option = document.createElement("option");
        option.value = version;
        option.textContent = version + (version === currentVersion ? "  🟢" : "");
        versionSelect.appendChild(option);
    });

    if (currentVersion) {
        versionSelect.value = currentVersion;
    } else if (versionsSnap.docs.length > 0) {
        const firstVersion = versionsSnap.docs[0].id;
        versionSelect.value = firstVersion;
        await updateDoc(mapDocRef, { current_version: firstVersion });
    }

    // 🔹 Update Firestore + refresh tables when version changes
    versionSelect.addEventListener("change", async () => {
        const selectedVersion = versionSelect.value;
        if (!selectedVersion) return;

        try {
            await updateDoc(mapDocRef, { current_version: selectedVersion });
            console.log(`Current version updated to ${selectedVersion}`);

            Array.from(versionSelect.options).forEach(opt => {
                opt.textContent = opt.value + (opt.value === selectedVersion ? "  🟢" : "");
            });

            await renderNodesTable();
            await renderEdgesTable();
            await loadMap(mapId); 
        } catch (err) {
            console.error("Error updating current version:", err);
        }
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










async function loadMap(mapId) {
  try {
    // Ensure mapId is a string
    const safeMapId = String(mapId);

    // 🔹 Get current active data
    const mapDocRef = doc(db, "MapVersions", safeMapId);
    const mapDocSnap = await getDoc(mapDocRef);

    if (!mapDocSnap.exists()) {
      console.error("❌ Map not found:", safeMapId);
      return;
    }

    const mapData = mapDocSnap.data();
    const activeCampus = mapData.current_active_campus;
    const activeVersion = String(mapData.current_version || "");

    console.log(`📌 Active → Map: ${safeMapId}, Campus: ${activeCampus}, Version: ${activeVersion}`);

    // 🔹 Fetch nodes & edges for the active version
    const versionDocRef = doc(db, "MapVersions", safeMapId, "versions", activeVersion);
    const versionDocSnap = await getDoc(versionDocRef);

    if (!versionDocSnap.exists()) {
      console.error("❌ Version not found:", activeVersion);
      return;
    }

    const versionData = versionDocSnap.data();
    let nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
    let edges = Array.isArray(versionData.edges) ? versionData.edges : [];

    // 🔹 Filter nodes for the active campus
    nodes = nodes.filter(n => n.campus_id === activeCampus && n.is_deleted !== true);

    console.log("✅ Nodes to render:", nodes);

    // ---- Reset map containers ----
    const mapContainer = document.getElementById("map-overview");
    if (mapContainer._leaflet_id) {
      mapContainer._leaflet_id = null;
      mapContainer.innerHTML = "";
    }

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
      attribution: "© OpenStreetMap"
    }).addTo(map);

    renderDataOnMap(map, { nodes, edges });

    // ---- Fullscreen modal map ----
    const modal = document.getElementById("mapModal");
    const closeBtn = document.querySelector(".close-btn");

    mapContainer.addEventListener("click", () => {
      modal.style.display = "block";
      setTimeout(() => {
        const mapFull = L.map("map-full", {
          center: [6.9130, 122.0630],
          zoom: 18,
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
    });

  } catch (err) {
    console.error("❌ Error loading map:", err);
  }
}




// ---- Sidebar details ----
function showDetails(node) {
  const sidebar = document.querySelector(".map-sidebar");

  // Format created_at nicely
  let createdAtFormatted = "-";
  if (node.created_at) {
    if (typeof node.created_at.toDate === "function") {
      const d = node.created_at.toDate();
      createdAtFormatted = d.toLocaleString("en-US", {
        year: "numeric",
        month: "short",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit"
      });
    } else {
      createdAtFormatted = node.created_at;
    }
  }

  sidebar.innerHTML = `
    <div class="header-row"><h2>📌 Node Details</h2> <button class="edit-btn">Edit</button></div>
    <div class="details-item"><span>Node ID:</span> ${node.node_id || "-"}</div>
    <div class="details-item"><span>Name:</span> ${node.name || "-"}</div>
    <div class="details-item"><span>Type:</span> ${node.type || "-"}</div>
    <div class="details-item"><span>Latitude:</span> ${node.latitude || "-"}</div>
    <div class="details-item"><span>Longitude:</span> ${node.longitude || "-"}</div>
    <div class="details-item"><span>Infrastructure:</span> ${node.infraName || "-"}</div>
    <div class="details-item"><span>Room:</span> ${node.roomName || "-"}</div>
    ${
      node.indoor
        ? `
      <h3>🏢 Indoor Info</h3>
      <div class="details-subitem"><span>Floor:</span> ${node.indoor.floor}</div>
      <div class="details-subitem"><span>X:</span> ${node.indoor.x}</div>
      <div class="details-subitem"><span>Y:</span> ${node.indoor.y}</div>
    `
        : ""
    }
    <div class="details-item"><span>Active:</span> ${node.is_active ? "✅ Yes" : "❌ No"}</div>
    <div class="details-item"><span>Campus:</span> ${node.campusName || "-"}</div>
    <div class="details-item"><span>Created At:</span> ${createdAtFormatted}</div>
  `;
}


function renderDataOnMap(map, data, enableClick = false) {
  const nodes = Array.isArray(data.nodes) ? data.nodes : [];
  const edges = Array.isArray(data.edges) ? data.edges : [];

  // --- Barriers (polygon + corner markers) ---
  const barrierNodes = nodes.filter(d => d.type === "barrier");
  const barrierCoords = barrierNodes.map(b => [b.latitude, b.longitude]);

  if (barrierCoords.length > 0) {
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
          name: "WMSU Camp B",
          type: "Campus Area",
          latitude: e.latlng.lat.toFixed(6),
          longitude: e.latlng.lng.toFixed(6)
        });
      });

      barrierNodes.forEach(node => {
        const cornerMarker = L.circleMarker([node.latitude, node.longitude], {
          radius: 6,
          color: "darkgreen",
          fillColor: "lightgreen",
          fillOpacity: 0.9
        }).addTo(map);

        cornerMarker.on("click", () => showDetails(node));
      });
    }
  }

  // --- Infrastructure (Buildings) ---
  nodes.filter(d => d.type === "infrastructure").forEach(building => {
    const marker = L.circleMarker([building.latitude, building.longitude], {
      radius: 8,
      color: "blue",
      fillColor: "lightblue",
      fillOpacity: 0.7
    }).addTo(map);

    if (enableClick) {
      marker.on("click", () => showDetails(building));
    }
  });

  // --- Rooms ---
  nodes.filter(d => d.type === "room").forEach(room => {
    const marker = L.marker([room.latitude, room.longitude]).addTo(map);

    if (enableClick) {
      marker.on("click", () => showDetails(room));
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

    const line = L.polyline([from.coords, to.coords], {
      color: "orange",
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
  // ======================================================
}
