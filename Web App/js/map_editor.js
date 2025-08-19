import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js"; 
import { 
    getFirestore, collection, addDoc, getDocs, query, orderBy 
} from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";

// ✅ Firebase config (better: put in firebaseConfig.js and gitignore it!) 
import { firebaseConfig } from "./../firebaseConfig.js";

const app = initializeApp(firebaseConfig);
const db = getFirestore(app);

// =============== MODAL CONTROL ===============
function openNodeModal() {
    document.getElementById('addNodeModal').style.display = 'block';
    generateNextNodeId(); // auto-generate ID every time modal opens
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
                <td>${data.linked_building || "-"}</td>
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
    const linkedBuilding = document.getElementById("linkedBuilding").value;
    const qrAnchor = document.getElementById("qrAnchor").checked;

    try {
        await addDoc(collection(db, "Nodes"), {
            node_id: nodeId,
            name: nodeName,
            coordinates: { latitude, longitude },
            linked_building: linkedBuilding,
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





document.addEventListener("DOMContentLoaded", function () {
    function handleOther(selectId) {
        const select = document.getElementById(selectId);

        select.addEventListener("change", function () {
            if (this.value === "other") {
                // Create input element
                const input = document.createElement("input");
                input.type = "text";
                input.name = selectId;
                input.placeholder = "Enter your own value";
                input.classList.add("custom-input");

                // Replace select with input
                this.parentNode.replaceChild(input, this);

                // Allow switching back (if user clears input)
                input.addEventListener("blur", function () {
                    if (input.value.trim() === "") {
                        input.parentNode.replaceChild(select, input);
                        select.value = "";
                    }
                });
            }
        });
    }

    handleOther("pathType");
    handleOther("elevation");
});