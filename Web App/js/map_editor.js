// ======================= FIREBASE SETUP ===========================
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
import {
    getFirestore, collection, addDoc, getDocs, query, orderBy, where, updateDoc, doc
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

// ----------- Load Nodes Table -----------
async function renderNodesTable() {
    const tbody = document.querySelector(".nodetbl tbody");
    tbody.innerHTML = "";

    try {
        // Fetch all needed collections for mapping IDs to names
        const [infraSnap, roomSnap, campusSnap] = await Promise.all([
            getDocs(collection(db, "Infrastructure")),
            getDocs(collection(db, "Rooms")),
            getDocs(collection(db, "Campus"))
        ]);

        // Build lookup maps
        const infraMap = {};
        infraSnap.forEach(doc => {
            const d = doc.data();
            infraMap[d.infra_id] = d.name;
        });

        const roomMap = {};
        roomSnap.forEach(doc => {
            const d = doc.data();
            roomMap[d.room_id] = d.name;
        });

        const campusMap = {};
        campusSnap.forEach(doc => {
            const d = doc.data();
            campusMap[d.campus_id] = d.campus_name;
        });

        // Fetch nodes
        const q = query(collection(db, "Nodes"), orderBy("created_at", "asc"));
        const querySnapshot = await getDocs(q);

        querySnapshot.forEach((docSnap) => {
            const data = docSnap.data();

            // Coordinates
            const coords = (data.latitude && data.longitude) ? `${data.latitude}, ${data.longitude}` : "-";

            // Related infra/room names
            const infraName = data.related_infra_id ? (infraMap[data.related_infra_id] || data.related_infra_id) : "-";
            const roomName = data.related_room_id ? (roomMap[data.related_room_id] || data.related_room_id) : "-";

            // Indoor/Outdoor
            let indoorOutdoor = "Outdoor";
            if (data.indoor) {
                indoorOutdoor = `Indoor (Floor: ${data.indoor.floor || "-"}, X: ${data.indoor.x || "-"}, Y: ${data.indoor.y || "-"})`;
            }

            // Campus name
            const campusName = data.campus_id ? (campusMap[data.campus_id] || data.campus_id) : "-";

            // Type
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
                    <i class="fas fa-trash"></i>
                </td>
            `;
            tbody.appendChild(tr);
        });

    } catch (err) {
        console.error("Error loading nodes: ", err);
    }
}

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

// ----------- Add Node Handler -----------
document.getElementById("nodeForm").addEventListener("submit", async (e) => {
    e.preventDefault();

    const nodeId = document.getElementById("nodeId").value;
    const nodeName = document.getElementById("nodeName").value;
    const latitude = document.getElementById("latitude").value;
    const longitude = document.getElementById("longitude").value;

    // Type: could be select or input
    let typeEl = document.getElementById("nodeType");
    let type = typeEl ? typeEl.value : "";
    if (!type && typeEl && typeEl.tagName === "INPUT") {
        type = typeEl.value;
    }

    const relatedInfraId = document.getElementById("relatedInfra").value;
    const relatedRoomId = document.getElementById("relatedRoom").value;

    // Indoor/Outdoor
    const isIndoor = document.getElementById("indoorCheckbox").checked;
    let indoor = null;
    if (isIndoor) {
        indoor = {
            floor: document.getElementById("floor").value,
            x: document.getElementById("xCoord").value,
            y: document.getElementById("yCoord").value
        };
    }

    const campusId = document.getElementById("campusDropdown").value;

    try {
        await addDoc(collection(db, "Nodes"), {
            node_id: nodeId,
            name: nodeName,
            latitude: latitude,   // ✅ renamed
            longitude: longitude, // ✅ renamed
            type: type,
            related_infra_id: relatedInfraId,
            related_room_id: relatedRoomId,
            indoor: indoor,
            is_active: true,
            campus_id: campusId,
            created_at: new Date()
        });


        // Clear all fields in the add node modal
        document.getElementById("nodeForm").reset();
        // If you have custom fields (like disabled nodeId), reset them manually:
        generateNextNodeId();
        // Hide indoor details if shown
        document.getElementById("indoorDetails").style.display = "none";

        // Close modal
        hideNodeModal();

        // Refresh table
        renderNodesTable();

    } catch (err) {
        alert("Error adding node: " + err);
    }
});



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
    const latitude = document.getElementById("editLatitude").value;
    const longitude = document.getElementById("editLongitude").value;

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
            x: document.getElementById("editXCoord").value,
            y: document.getElementById("editYCoord").value
        };
    }

    const campusId = document.getElementById("editCampusDropdown").value;

    try {
        const nodeRef = doc(db, "Nodes", docId);

        await updateDoc(nodeRef, {
            node_id: nodeId,
            name: nodeName,
            latitude: latitude,   // ✅ renamed
            longitude: longitude, // ✅ renamed
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

// ----------- Load Nodes into Edge Dropdowns -----------
async function loadNodesDropdownsForEdge() {
    const startNodeSelect = document.getElementById("startNode");
    const endNodeSelect = document.getElementById("endNode");

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
            startNodeSelect.appendChild(option1);

            const option2 = document.createElement("option");
            option2.value = data.node_id;
            option2.textContent = `${data.node_id} - ${data.name}`;
            endNodeSelect.appendChild(option2);
        }
    });
}

// ----------- Add Edge Handler -----------
document.querySelector("#addEdgeModal form").addEventListener("submit", async (e) => {
    e.preventDefault();

    const edgeId = document.querySelector("#addEdgeModal input[type='text']").value;
    const startNode = document.getElementById("startNode").value;
    const endNode = document.getElementById("endNode").value;

    // Handle pathType (select OR custom input)
    let pathTypeEl = document.getElementById("pathType") || document.querySelector("input[name='pathType']");
    let pathType = pathTypeEl ? pathTypeEl.value.trim() : "";

    // Handle elevation (select OR custom input)
    let elevationEl = document.getElementById("elevation") || document.querySelector("input[name='elevation']");
    let elevation = elevationEl ? elevationEl.value.trim() : "";

    // Convert custom inputs into snake_case for DB storage
    const toSnakeCase = str => str.toLowerCase().replace(/\s+/g, "_");

    if (pathType && !["via_overpass", "via_underpass", "stairs", "ramp"].includes(pathType)) {
        pathType = toSnakeCase(pathType);
    }
    if (elevation && !["slope_up", "slope_down", "flat"].includes(elevation)) {
        elevation = toSnakeCase(elevation);
    }

    try {
        await addDoc(collection(db, "Edges"), {
            edge_id: edgeId,
            from_node: startNode,
            to_node: endNode,
            distance: null,
            path_type: pathType || null,
            elevations: elevation || null,
            is_active: true,
            is_deleted: false,
            created_at: new Date()
        });

        alert("Edge saved!");
        document.getElementById("addEdgeModal").style.display = "none";
        renderEdgesTable();

    } catch (err) {
        alert("Error adding edge: " + err);
    }
});

// ----------- Load Edges Table -----------
async function renderEdgesTable() {
    const tbody = document.querySelector(".edgetbl tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    const q = query(collection(db, "Edges"), orderBy("created_at", "asc"));
    const snapshot = await getDocs(q);

    snapshot.forEach(doc => {
        const data = doc.data();

        // Formatter helper
        const formatText = (value) => {
            if (!value) return "-";
            return value
                .split("_")
                .map(word => word.charAt(0).toUpperCase() + word.slice(1))
                .join(" ");
        };

        const formattedPathType = formatText(data.path_type);
        const formattedElevation = formatText(data.elevations);

        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>${data.edge_id}</td>
            <td>${data.from_node} > ${data.to_node}</td>
            <td>${data.distance || "-"}</td>
            <td>${formattedPathType}</td>
            <td>${formattedElevation}</td>
            <td class="actions">
                <i class="fas fa-edit"></i>
                <i class="fas fa-trash"></i>
            </td>
        `;
        tbody.appendChild(tr);
    });
}

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