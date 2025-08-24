import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js"; 
import { 
    getFirestore, collection, addDoc, getDocs, query, orderBy, where, updateDoc, doc   // ⬅️ added `where`
} from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";

// ✅ Firebase config
import { firebaseConfig } from "./../firebaseConfig.js";

const app = initializeApp(firebaseConfig);
const db = getFirestore(app);

// =============== MODAL CONTROL ===============
function openNodeModal() {
    document.getElementById('addNodeModal').style.display = 'block';
    generateNextNodeId();
    loadBuildingsIntoDropdown();
}

function closeNodeModal() {
    document.getElementById('addNodeModal').style.display = 'none';

    // Reset all form fields EXCEPT nodeId
    document.getElementById("nodeName").value = "";
    document.getElementById("latitude").value = "";
    document.getElementById("longitude").value = "";
    document.getElementById("linkedBuilding").value = "";
    document.getElementById("qrAnchor").checked = false;
}
window.openNodeModal = openNodeModal;
window.closeNodeModal = closeNodeModal;

// =============== AUTO INCREMENT NODE ID ===============
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


// =============== LOAD BUILDINGS INTO NODE MODAL DROPDOWN ===============
async function loadBuildingsIntoDropdown() {
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

// ✅ NEW: load buildings into *edit modal* dropdown
async function loadBuildingsIntoDropdownById(selectId) {
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


// =============== LOAD NODES INTO TABLE ===============
async function loadNodes() { 
    const tbody = document.querySelector(".nodetbl tbody");
    tbody.innerHTML = "";

    try {
        const q = query(collection(db, "Nodes"), orderBy("created_at", "asc"));
        const querySnapshot = await getDocs(q);

        querySnapshot.forEach((docSnap) => {
            const data = docSnap.data();

            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${data.node_id}</td>
                <td>${data.name}</td>
                <td>${data.coordinates ? 
                    `${data.coordinates.latitude}, ${data.coordinates.longitude}` 
                    : "-"}</td>
                <td>${data.linked_building_name || "-"}</td>
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


// =============== HANDLE NODE FORM SUBMIT ===============
document.getElementById("nodeForm").addEventListener("submit", async (e) => {
    e.preventDefault();

    const nodeId = document.getElementById("nodeId").value;
    const nodeName = document.getElementById("nodeName").value;
    const latitude = document.getElementById("latitude").value;
    const longitude = document.getElementById("longitude").value;
    const linkedBuildingSelect = document.getElementById("linkedBuilding");
    const linkedBuildingId = linkedBuildingSelect.value;
    const linkedBuildingName = linkedBuildingSelect.options[linkedBuildingSelect.selectedIndex]?.text || "";
    const qrAnchor = document.getElementById("qrAnchor").checked;

    try {
        await addDoc(collection(db, "Nodes"), {
            node_id: nodeId,
            name: nodeName,
            coordinates: { latitude, longitude },
            linked_building: linkedBuildingId,
            linked_building_name: linkedBuildingName,
            qr_anchor: qrAnchor,
            is_active: true,
            is_deleted: false,
            created_at: new Date()
        });

        alert("Node saved!");
        closeNodeModal();
        loadNodes();

    } catch (err) { 
        alert("Error adding node: " + err); 
    }
});

document.getElementById("editNodeForm").addEventListener("submit", async (e) => {
  e.preventDefault();

  const form = e.target;
  const docId = form.dataset.docId;   // 👈 we stored this earlier when opening modal
  if (!docId) {
    alert("No document ID found for update");
    return;
  }

  const nodeId = document.getElementById("editNodeIdHidden").value; // hidden, original ID
  const nodeName = document.getElementById("editNodeName").value;
  const latitude = document.getElementById("editLatitude").value;
  const longitude = document.getElementById("editLongitude").value;
  const linkedBuildingSelect = document.getElementById("editLinkedBuilding");
  const linkedBuildingId = linkedBuildingSelect.value;
  const linkedBuildingName = linkedBuildingSelect.options[linkedBuildingSelect.selectedIndex]?.text || "";
  const qrAnchor = document.getElementById("editQrAnchor").checked;

  try {
    const nodeRef = doc(db, "Nodes", docId);

    await updateDoc(nodeRef, {
      node_id: nodeId,
      name: nodeName,
      coordinates: { latitude, longitude },
      linked_building: linkedBuildingId,
      linked_building_name: linkedBuildingName,
      qr_anchor: qrAnchor,
      updated_at: new Date()
    });

    alert("Node updated!");
    document.getElementById("editNodeModal").style.display = "none";
    loadNodes(); // refresh table

  } catch (err) {
    console.error("Error updating node:", err);
    alert("Error updating node: " + err.message);
  }
});


// =============== EDIT NODE HANDLER ===============
// ✅ listens for clicks on the edit icon in the nodes table
document.querySelector(".nodetbl").addEventListener("click", async (e) => {
    if (!e.target.classList.contains("fa-edit")) return;

    const row = e.target.closest("tr");
    if (!row) return;

    const nodeId = row.querySelector("td")?.textContent?.trim();
    if (!nodeId) return;

    try {
        const nodesQ = query(collection(db, "Nodes"), where("node_id", "==", nodeId));
        const snap = await getDocs(nodesQ);

        if (snap.empty) {
            alert("Node not found in Firestore");
            return;
        }

        const docSnap = snap.docs[0];
        const nodeData = docSnap.data();

        await loadBuildingsIntoDropdownById("editLinkedBuilding");

        document.getElementById("editNodeId").value = nodeData.node_id ?? "";
        document.getElementById("editNodeIdHidden").value = nodeData.node_id ?? "";
        document.getElementById("editNodeName").value = nodeData.name ?? "";
        document.getElementById("editLatitude").value = nodeData.coordinates?.latitude ?? "";
        document.getElementById("editLongitude").value = nodeData.coordinates?.longitude ?? "";
        document.getElementById("editQrAnchor").checked = !!nodeData.qr_anchor;

        if (nodeData.linked_building) {
            const sel = document.getElementById("editLinkedBuilding");
            sel.value = nodeData.linked_building;

            if (![...sel.options].some(o => o.value === sel.value)) {
                const opt = document.createElement("option");
                opt.value = nodeData.linked_building;
                opt.textContent = nodeData.linked_building_name || nodeData.linked_building;
                sel.appendChild(opt);
                sel.value = nodeData.linked_building;
            }
        }

        const editForm = document.getElementById("editNodeForm");
        if (editForm) editForm.dataset.docId = docSnap.id;

        document.getElementById("editNodeModal").style.display = "flex";  // 👈 show modal

    } catch (err) {
        console.error("Error opening edit modal:", err);
    }
});








// =============== AUTO INCREMENT EDGE ID ===============
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

// =============== LOAD NODES INTO DROPDOWNS ===============
async function loadNodesIntoDropdowns() {
    const startNodeSelect = document.getElementById("startNode");
    const endNodeSelect   = document.getElementById("endNode");

    startNodeSelect.innerHTML = `<option value="">Select start node</option>`;
    endNodeSelect.innerHTML   = `<option value="">Select end node</option>`;

    // ✅ Order nodes by created_at ascending
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


// =============== HANDLE EDGE FORM SUBMIT ===============
document.querySelector("#addEdgeModal form").addEventListener("submit", async (e) => {
    e.preventDefault();

    const edgeId    = document.querySelector("#addEdgeModal input[type='text']").value;
    const startNode = document.getElementById("startNode").value;
    const endNode   = document.getElementById("endNode").value;

    // Handle pathType (select OR custom input)
    let pathTypeEl = document.getElementById("pathType") || document.querySelector("input[name='pathType']");
    let pathType = pathTypeEl ? pathTypeEl.value.trim() : "";

    // Handle elevation (select OR custom input)
    let elevationEl = document.getElementById("elevation") || document.querySelector("input[name='elevation']");
    let elevation = elevationEl ? elevationEl.value.trim() : "";

    // Convert custom inputs into snake_case for DB storage
    const toSnakeCase = str =>
        str.toLowerCase().replace(/\s+/g, "_");

    if (pathType && !["via_overpass","via_underpass","stairs","ramp"].includes(pathType)) {
        pathType = toSnakeCase(pathType);
    }
    if (elevation && !["slope_up","slope_down","flat"].includes(elevation)) {
        elevation = toSnakeCase(elevation);
    }

    try {
        await addDoc(collection(db, "Edges"), {
            edge_id: edgeId,
            from_node: startNode,
            to_node: endNode,
            distance: null, // to be added later
            path_type: pathType || null,
            elevations: elevation || null,
            is_active: true,
            is_deleted: false,
            created_at: new Date()
        });

        alert("Edge saved!");
        document.getElementById("addEdgeModal").style.display = "none";
        loadEdges();

    } catch (err) {
        alert("Error adding edge: " + err);
    }
});


// =============== LOAD EDGES INTO TABLE ===============
async function loadEdges() {
    const tbody = document.querySelector(".edgetbl tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    const q = query(collection(db, "Edges"), orderBy("created_at", "asc"));
    const snapshot = await getDocs(q);

    snapshot.forEach(doc => {
        const data = doc.data();

        // --- Formatter helper ---
        const formatText = (value, prefix = "") => {
            if (!value) return "-";
            // replace underscores with spaces, capitalize each word
            const formatted = value
                .split("_")
                .map(word => word.charAt(0).toUpperCase() + word.slice(1))
                .join(" ");
            return prefix ? `${prefix} ${formatted}` : formatted;
        };

        // Format path_type and elevations
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


// =============== MAKE FUNCTIONS GLOBAL FOR MODALS ===============
window.openEdgeModal = async function () {
    document.getElementById("addEdgeModal").style.display = "block";
    await generateNextEdgeId();
    await loadNodesIntoDropdowns();
};

window.closeEdgeModal = function () {
    document.getElementById("addEdgeModal").style.display = "none";
};

// =============== LOAD DATA ON PAGE LOAD ===============
window.onload = () => {
    loadNodes();
    loadEdges();
};







// ================= EDIT EDGE MODAL =================

// ================== TEMPLATE STORAGE ==================
// store original select HTML so we can restore it later
const selectTemplates = {};
document.addEventListener("DOMContentLoaded", () => {
  ["editPathType", "editElevation"].forEach(id => {
    const el = document.getElementById(id);
    if (el) selectTemplates[id] = el.outerHTML;
  });
});

// recreate select if it was replaced with input
function ensureSelectExists(selectId) {
  let select = document.getElementById(selectId);
  if (select) return select;

  // if an input exists instead, restore the select
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

// handle select vs custom input
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

// ================== EDIT EDGE MODAL ==================
document.querySelector(".edgetbl").addEventListener("click", async (e) => {
  if (e.target.classList.contains("fa-edit")) {
    const tr = e.target.closest("tr");
    const edgeId = tr.children[0].textContent; // first td = edge_id

    // Get edge data from Firestore
    const q = query(collection(db, "Edges"), where("edge_id", "==", edgeId));
    const snapshot = await getDocs(q);

    if (!snapshot.empty) {
      const docSnap = snapshot.docs[0];
      const data = docSnap.data();

      // Fill modal fields
      document.getElementById("editEdgeId").value = data.edge_id;

      // Load nodes into dropdowns and pre-select
      await loadNodesIntoDropdownsForEdit(data.from_node, data.to_node);

      // Handle path type & elevation properly
      handlePreselectOrCustom("editPathType", data.path_type);
      handlePreselectOrCustom("editElevation", data.elevations);

      // Store doc ID for saving
      document.getElementById("editEdgeModal").dataset.docId = docSnap.id;

      // Show modal
      document.getElementById("editEdgeModal").style.display = "flex";
    }
  }
});


// Helper to load nodes into edit dropdowns and select current values
async function loadNodesIntoDropdownsForEdit(selectedFrom, selectedTo) {
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

// Handle "Other" replacement (Add + Edit modals)
function handleOther(selectId) {
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

// Apply for Add modal
handleOther("pathType");
handleOther("elevation");

// Apply for Edit modal
handleOther("editPathType");
handleOther("editElevation");

// Handle save changes (update Firestore)
document.querySelector("#editEdgeModal form").addEventListener("submit", async (e) => {
    e.preventDefault();

    const docId = document.getElementById("editEdgeModal").dataset.docId;

    // Grab either select OR input value
    const getFieldValue = (id) => {
        const select = document.getElementById(id);
        const input = document.querySelector(`input[name='${id}']`);
        return select ? select.value.trim() : (input ? input.value.trim() : "");
    };

    // Convert custom input into snake_case
    const toSnakeCase = str => str.toLowerCase().replace(/\s+/g, "_");

    let pathType = getFieldValue("editPathType");
    let elevation = getFieldValue("editElevation");

    if (pathType && !["via_overpass","via_underpass","stairs","ramp"].includes(pathType)) {
        pathType = toSnakeCase(pathType);
    }
    if (elevation && !["slope_up","slope_down","flat"].includes(elevation)) {
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
        loadEdges(); // refresh table
    } catch (err) {
        alert("Error updating edge: " + err);
    }
});

// Cancel button
document.getElementById("cancelEditEdgeBtn").addEventListener("click", () => {
    document.getElementById("editEdgeModal").style.display = "none";
});

// Close if clicked outside
document.getElementById("editEdgeModal").addEventListener("click", (e) => {
    if (e.target.id === "editEdgeModal") {
        document.getElementById("editEdgeModal").style.display = "none";
    }
});























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
        "Add Node",   // Tab 1
        "Add Edge"    // Tab 2
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

    // ✅ Open modal
    addButton.addEventListener("click", () => {
        if (addButton.textContent === "Add Node") {
            window.openNodeModal(); // <-- use the function from your module script
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





