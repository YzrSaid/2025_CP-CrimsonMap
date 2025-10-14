// ======================= FIREBASE SETUP ===========================
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
import { getFirestore, setDoc, collection, addDoc, getDocs, query, orderBy, where, updateDoc, doc, deleteField, getDoc, deleteDoc } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";
import { firebaseConfig } from "../firebaseConfig.js";


// Initialize Firebase and Firestore
const app = initializeApp(firebaseConfig);
const db = getFirestore(app);




// ======================= CATEGORY SECTION =========================

// ----------- Modal Controls -----------
function showCategoryModal() {
    document.getElementById('addCategoryModal').style.display = 'flex';
}
function hideCategoryModal() {
    document.getElementById('addCategoryModal').style.display = 'none';
}
window.showCategoryModal = showCategoryModal;
window.hideCategoryModal = hideCategoryModal;




let categoriesTableData = [];

// ----------- Load Categories Table (exclude deleted categories) -----------
async function renderCategoriesTable() {
    const tbody = document.getElementById("categoriesTableBody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        let categories = [];

        if (navigator.onLine) {
            const querySnapshot = await getDocs(collection(db, "Categories"));
            categories = querySnapshot.docs
                .map(doc => ({ id: doc.id, ...doc.data() }))
                .filter(data => !data.is_deleted);
        } else {
            const res = await fetch("../assets/firestore/Categories.json");
            const dataJson = await res.json();
            categories = dataJson.filter(data => !data.is_deleted);
        }

        // Sort by createdAt
        categories.sort((a, b) => {
            const timeA = a.createdAt?.seconds ? a.createdAt.seconds * 1000 : a.createdAt?.toMillis ? a.createdAt.toMillis() : 0;
            const timeB = b.createdAt?.seconds ? b.createdAt.seconds * 1000 : b.createdAt?.toMillis ? b.createdAt.toMillis() : 0;
            return timeA - timeB;
        });

        // Store for filtering/searching
        categoriesTableData = categories;

        renderCategoriesTableRows(categoriesTableData);

    } catch (err) {
        console.error("Error loading categories: ", err);
    }
}

function renderCategoriesTableRows(data) {
    const tbody = document.getElementById("categoriesTableBody");
    if (!tbody) return;
    tbody.innerHTML = "";
    data.forEach((data, index) => {
        const tr = document.createElement("tr");
        tr.dataset.id = data.id;
        tr.innerHTML = `
            <td>${index + 1}</td>
            <td>${data.name}</td>
            <td>
                <span class="category-color" style="background:${data.color || '#b41c1c'}"></span>
                <span style="margin-left:8px;">${data.color || '#b41c1c'}</span>
            </td>
            <td>${data.buildings || 0}</td>
            <td class="actions">
                <button class="edit"><i class="fas fa-edit"></i></button>
                <button class="delete"><i class="fas fa-trash"></i></button>
            </td>
        `;
        tbody.appendChild(tr);
    });
}



// Call on page load
document.addEventListener("DOMContentLoaded", renderCategoriesTable);



// ----------- Add Category Handler -----------
document.getElementById('categoryForm')?.addEventListener('submit', async (e) => {
    e.preventDefault();

    const name = document.getElementById('categoryName').value.trim();
    const color = document.getElementById('categoryColor').value;

    if (!name || !color) {
        alert("Please fill in all required fields.");
        return;
    }

    try {
        // Generate next category ID in format CAT-01, CAT-02, etc.
        let nextNum = 1;
        const querySnapshot = await getDocs(collection(db, "Categories"));
        const existingIds = querySnapshot.docs
            .map(doc => doc.data().category_id)
            .filter(id => id && id.startsWith("CAT-"))
            .map(id => parseInt(id.slice(4), 10))
            .filter(num => !isNaN(num));
        if (existingIds.length > 0) {
            nextNum = Math.max(...existingIds) + 1;
        }
        const categoryId = `CAT-${String(nextNum).padStart(2, "0")}`;

        // Save category
        await addDoc(collection(db, "Categories"), {
            category_id: categoryId,
            name: name,
            color: color,
            buildings: 0,
            is_deleted: false,
            createdAt: new Date()
        });

        // Save Activity Log
        await addDoc(collection(db, "ActivityLogs"), {
            timestamp: new Date(),
            activity: "Added Category",
            item: `Category ${categoryId}`,
            description: `Added category "${name}" with color "${color}".`
        });

        alert("Category saved!");

        // ✅ Update StaticDataVersions/GlobalInfo after saving or updating a node
        const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
        await updateDoc(staticDataRef, {
            categories_updated: true,
        });
        document.getElementById('categoryForm').reset();
        hideCategoryModal();
        renderCategoriesTable();
        populateCategoryDropdownForInfra();
    } catch (err) {
        alert("Error adding category: " + err);
    }
});

// ----------- Populate Category Dropdown for Infrastructure -----------
async function populateCategoryDropdownForInfra() {
    const categorySelect = document.querySelector("#addInfraModal select");
    if (!categorySelect) return;

    categorySelect.innerHTML = `<option value="">Select a category</option>`;
    const q = query(collection(db, "Categories"), orderBy("createdAt", "asc"));
    const snapshot = await getDocs(q);

    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.category_id && data.name) {
            const option = document.createElement("option");
            option.value = data.category_id;
            option.textContent = data.name;
            option.dataset.name = data.name; // ✅ save category name here
            categorySelect.appendChild(option);
        }
    });
}



// ======================= INFRASTRUCTURE SECTION =========================

// ----------- Modal Controls -----------
function showInfraModal() {
    document.getElementById('addInfraModal').style.display = 'flex';
    generateNextInfraId();
}
function hideInfraModal() {
    document.getElementById('addInfraModal').style.display = 'none';
}
window.showInfraModal = showInfraModal;
window.hideInfraModal = hideInfraModal;

// ----------- Auto-Increment Infra ID -----------
async function generateNextInfraId() {
    const q = query(collection(db, "Infrastructure"));
    const snapshot = await getDocs(q);

    let maxNum = 0;
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.infra_id) {
            const num = parseInt(data.infra_id.replace("INFRA-", ""));
            if (!isNaN(num) && num > maxNum) maxNum = num;
        }
    });

    const nextId = `INFRA-${String(maxNum + 1).padStart(2, "0")}`;
    document.getElementById("infraId").value = nextId;
}

// ----------- Add Infrastructure Handler -----------
document.querySelector("#addInfraModal form")?.addEventListener("submit", async (e) => {
    e.preventDefault();

    const name = document.querySelector('#addInfraModal input[placeholder="e.g. Main Library"]').value.trim();
    const infraId = document.getElementById("infraId").value.trim();

    const categorySelect = document.querySelector('#addInfraModal select');
    const categoryId = categorySelect.value;
    const categoryName = categorySelect.selectedOptions[0]?.dataset.name || categoryId; // ✅ category name

        const phone = "09123456789";
    const email = "sample@gmail.com";

    let imageUrl = "";
    const imageFile = document.getElementById('uploadImage').files[0];
    if (imageFile) {
        imageUrl = await convertFileToBase64(imageFile);
    }

    if (!name || !infraId || !categoryId) {
        alert("Please fill in all required fields.");
        return;
    }

    

    try {
        // Save infrastructure
        await addDoc(collection(db, "Infrastructure"), {
            infra_id: infraId,
            name: name,
            category_id: categoryId,
            image_url: imageUrl,
            email: email,
            phone: phone,
            is_deleted: false,
            createdAt: new Date()
        });

        // ✅ Save activity log with category NAME (not id)
        await addDoc(collection(db, "ActivityLogs"), {
            timestamp: new Date(),
            activity: "Added Infrastructure",
            item: `Infrastructure ${infraId}`,
            description: `Added infrastructure "${name}" under category "${categoryName}".`
        });

        alert("Infrastructure saved successfully!");
        e.target.reset();
        hideInfraModal();
        renderInfraTable();

    } catch (err) {
        console.error("Error adding infrastructure:", err);
        alert("Error saving infrastructure.");
    }
});






let infraTableData = []; // Store loaded infra for filtering

// Update renderInfraTable to store data for filtering
async function renderInfraTable() {
    const tbody = document.querySelector(".infra-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        let infras = [];
        let categories = [];

        if (navigator.onLine) {
            const infraSnap = await getDocs(collection(db, "Infrastructure"));
            infras = infraSnap.docs.map(doc => ({ id: doc.id, ...doc.data() })).filter(d => !d.is_deleted);

            const catSnap = await getDocs(collection(db, "Categories"));
            categories = catSnap.docs.map(doc => ({ id: doc.id, ...doc.data() })).filter(d => !d.is_deleted);
        } else {
            const [infraRes, catRes] = await Promise.all([
                fetch("../assets/firestore/Infrastructure.json"),
                fetch("../assets/firestore/Categories.json")
            ]);
            infras = (await infraRes.json()).filter(d => !d.is_deleted);
            categories = (await catRes.json()).filter(d => !d.is_deleted);
        }

        // Sort infrastructure by createdAt
        infras.sort((a, b) => {
            const timeA = a.createdAt?.seconds ? a.createdAt.seconds * 1000 : a.createdAt?.toMillis ? a.createdAt.toMillis() : 0;
            const timeB = b.createdAt?.seconds ? b.createdAt.seconds * 1000 : b.createdAt?.toMillis ? b.createdAt.toMillis() : 0;
            return timeA - timeB;
        });

        // Create category map
        const catMap = {};
        categories.forEach(c => {
            catMap[c.category_id || c.id] = c.name;
        });

        // Store for filtering
        infraTableData = infras.map(data => ({
            ...data,
            categoryName: catMap[data.category_id] || "N/A"
        }));

        renderInfraTableRows(infraTableData);
    } catch (err) {
        console.error("Error loading infrastructure: ", err);
    }
}

// Helper to render rows based on filtered data
function renderInfraTableRows(data) {
    const tbody = document.querySelector(".infra-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";
    data.forEach(data => {
        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>${data.infra_id}</td>
            <td>${data.name}</td>
            <td>${data.categoryName}</td>
            <td>${data.phone || ""}</td>
            <td>${data.email || ""}</td>
            <td class="actions">
                <button class="edit"><i class="fas fa-edit"></i></button>
                <button class="delete"><i class="fas fa-trash"></i></button>
            </td>
        `;
        tbody.appendChild(tr);
    });
}


document.getElementById("sortCategory").addEventListener("change", function() {
    const sortVal = this.value.trim();
    let filtered = infraTableData;
    if (sortVal) {
        filtered = filtered.filter(d => String(d.category_id).trim() === sortVal);
    }
    // If search is active, filter by search too
    const searchVal = document.getElementById("searchInput").value.trim().toLowerCase();
    if (searchVal) {
        filtered = filtered.filter(d =>
            d.name.toLowerCase().includes(searchVal) ||
            d.infra_id.toLowerCase().includes(searchVal) ||
            d.categoryName.toLowerCase().includes(searchVal) ||
            (d.phone || "").toLowerCase().includes(searchVal) ||
            (d.email || "").toLowerCase().includes(searchVal)
        );
    }
    renderInfraTableRows(filtered);
});















// Call on page load
document.addEventListener("DOMContentLoaded", renderInfraTable);



// ======================= ROOM SECTION =========================

// ----------- Modal Controls -----------
function showRoomModal() {
    document.getElementById('addRoomModal').style.display = 'flex';
    generateNextRoomId();
    populateInfraDropdownForRooms();
}
function hideRoomModal() {
    document.getElementById('addRoomModal').style.display = 'none';
}
window.showRoomModal = showRoomModal;
window.hideRoomModal = hideRoomModal;

// ----------- Auto-Increment Indoor ID (always IND) -----------
async function generateNextRoomId() {
  const q = query(collection(db, "IndoorInfrastructure"));
  const snapshot = await getDocs(q);

  let maxNum = 0;
  snapshot.forEach(doc => {
    const data = doc.data();
    if (data.room_id) {
      // Always strip RM- or IND- just in case old data exists
      const num = parseInt(data.room_id.replace(/^RM-|^IND-/, ""));
      if (!isNaN(num) && num > maxNum) maxNum = num;
    }
  });

  // Always use IND as prefix
  const nextId = `IND-${String(maxNum + 1).padStart(3, "0")}`;
  document.getElementById("roomId").value = nextId;
}



// ----------- Populate Infrastructure Dropdown for Rooms -----------
async function populateInfraDropdownForRooms() {
    const select = document.querySelector("#addRoomModal select");
    if (!select) return;

    select.innerHTML = `<option value="">Select an infrastructure</option>`;

    try {
        // Step 1: Fetch categories so we can translate IDs → names
        const categoriesSnapshot = await getDocs(collection(db, "Categories"));
        const categoryMap = {};
        categoriesSnapshot.forEach(doc => {
            const cat = doc.data();
            if (cat.category_id && cat.name) {
                categoryMap[cat.category_id] = cat.name;
            }
        });

        // Step 2: Fetch infrastructures
        const q = query(collection(db, "Infrastructure"), orderBy("createdAt", "asc"));
        const snapshot = await getDocs(q);

        snapshot.forEach(doc => {
            const data = doc.data();

            // Look up category name using category_id
            const categoryName = categoryMap[data.category_id] || null;

            // ✅ Only include if under Academics or Administration Offices
            if (
                data.infra_id &&
                data.name &&
                (categoryName === "Academics" || categoryName === "Administration Offices")
            ) {
                const option = document.createElement("option");
                option.value = data.infra_id;
                option.textContent = data.name;
                option.dataset.name = data.name; // ✅ save infra name
                option.dataset.category = categoryName; // ✅ save category too
                select.appendChild(option);
            }
        });
    } catch (err) {
        console.error("Error populating infrastructures: ", err);
    }
}



// ----------- Add Indoor Infrastructure Handler -----------
document.querySelector("#addRoomModal form")?.addEventListener("submit", async (e) => {
    e.preventDefault();

    // Indoor Infrastructure Name
    const name = document.querySelector('#addRoomModal input[placeholder="e.g. Lecture Room 1"]')?.value.trim();

    // Indoor Infrastructure ID
    const roomId = document.querySelector('#addRoomModal input[name="room_id"]')?.value.trim();

    // Infrastructure dropdown
    const infraSelect = document.querySelectorAll('#addRoomModal select')[0];
    const infraId = infraSelect?.value || "";
    const infraName = infraSelect?.selectedOptions[0]?.dataset.name || infraId;

    // Indoor Type dropdown
    const indoorType = document.querySelectorAll('#addRoomModal select')[1]?.value || "";

    // Validation
    if (!name || !roomId || !infraId || !indoorType) {
        alert("Please fill in all required fields.");
        return;
    }

    try {
        // ✅ Save into IndoorInfrastructure collection
        await addDoc(collection(db, "IndoorInfrastructure"), {
            room_id: roomId,
            name: name,
            infra_id: infraId,
            indoor_type: indoorType,
            is_deleted: false,
            createdAt: new Date()
        });

        // Save Activity Log
        await addDoc(collection(db, "ActivityLogs"), {
            timestamp: new Date(),
            activity: "Added Indoor Infrastructure",
            item: `Indoor ${roomId}`,
            description: `Added "${name}" under infrastructure "${infraName}" (Type: ${indoorType}).`
        });

        alert("Indoor Infrastructure saved successfully!");

        // ✅ Clear form fields
        e.target.reset();
        document.querySelector('#addRoomModal input[name="room_id"]').value = "";

        // ✅ Hide modal + refresh table
        hideRoomModal();
        renderRoomsTable();
    } catch (err) {
        console.error("Error adding infrastructure:", err);
        alert("Error saving infrastructure.");
    }
});



let roomsTableData = [];

// ----------- Load Indoor Infrastructure Table (ignore deleted) -----------
async function renderRoomsTable() {
    const tbody = document.querySelector(".rooms-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        // Load Indoor Infrastructure
        const indoorSnap = await getDocs(collection(db, "IndoorInfrastructure"));
        const rooms = indoorSnap.docs
            .map(doc => ({ id: doc.id, ...doc.data() }))
            .filter(r => !r.is_deleted);

        // Load Infrastructure
        const infraSnap = await getDocs(collection(db, "Infrastructure"));
        const infrastructures = infraSnap.docs
            .map(doc => ({ id: doc.id, ...doc.data() }))
            .filter(i => !i.is_deleted);

        // Build infra map
        const infraMap = {};
        infrastructures.forEach(infra => {
            const key = infra.infra_id?.trim() || infra.id;
            if (!infraMap[key]) {
                infraMap[key] = `${infra.name} (${infra.infra_id || infra.id})`;
            }
        });

        // Store for filtering/searching
        roomsTableData = rooms.map(room => {
            const infraKey = room.infra_id?.trim() || room.infrastructure_id?.trim() || "";
            return {
                ...room,
                infraName: infraMap[infraKey] || `⚠️ Missing infra for ${infraKey}`
            };
        });

        // Sort by createdAt
        roomsTableData.sort((a, b) => {
            const timeA = a.createdAt?.seconds
                ? a.createdAt.seconds * 1000
                : a.createdAt?.toMillis
                ? a.createdAt.toMillis()
                : 0;
            const timeB = b.createdAt?.seconds
                ? b.createdAt.seconds * 1000
                : b.createdAt?.toMillis
                ? b.createdAt.toMillis()
                : 0;
            return timeA - timeB;
        });

        renderRoomsTableRows(roomsTableData);

    } catch (err) {
        console.error("❌ Error loading Indoor Infrastructure: ", err);
    }
}

function renderRoomsTableRows(data) {
    const tbody = document.querySelector(".rooms-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";
    data.forEach(room => {
        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>${room.room_id}</td>
            <td>${room.name}</td>
            <td>${room.infraName}</td>
            <td>${room.indoor_type || ""}</td>
            <td class="actions">
                <button class="edit"><i class="fas fa-edit"></i></button>
                <button class="delete"><i class="fas fa-trash"></i></button>
            </td>
        `;
        tbody.appendChild(tr);
    });
}

// Call on page load
document.addEventListener("DOMContentLoaded", renderRoomsTable);







// ----------- Edit Room Modal Open Handler -----------
document.querySelector(".rooms-table").addEventListener("click", async (e) => {
    if (
        !(e.target.classList.contains("fa-edit") ||
          (e.target.closest("button") && e.target.closest("button").classList.contains("edit")))
    ) return;

    const row = e.target.closest("tr");
    if (!row) return;

    // Get room_id from the first cell
    const roomId = row.querySelector("td")?.textContent?.trim();
    if (!roomId) return;

    try {
        // Fetch room data from IndoorInfrastructure
        const roomQ = query(collection(db, "IndoorInfrastructure"), where("room_id", "==", roomId));
        const snap = await getDocs(roomQ);

        if (snap.empty) {
            alert("Indoor Infrastructure not found in Firestore");
            return;
        }

        const docSnap = snap.docs[0];
        const roomData = docSnap.data();

        // Populate infrastructure dropdown and set value
        await populateEditRoomInfraDropdown(roomData.infra_id);
        document.getElementById("editRoomInfra").value = roomData.infra_id ?? "";

        // Populate indoor type dropdown and set value
        document.getElementById("editRoomType").value = roomData.indoor_type ?? "";

        // Prefill fields
        document.getElementById("editRoomId").value = roomData.room_id ?? "";
        document.getElementById("editRoomName").value = roomData.name ?? "";

        // Store docId for update
        document.getElementById("editRoomForm").dataset.docId = docSnap.id;

        // Show modal
        document.getElementById("editRoomModal").style.display = "flex";
    } catch (err) {
        console.error("Error opening edit room modal:", err);
    }
});

// ----------- Populate Infrastructure Dropdown for Edit Room Modal -----------
async function populateEditRoomInfraDropdown(selectedId) {
    const select = document.getElementById("editRoomInfra");
    if (!select) return;
    select.innerHTML = `<option value="">Select an infrastructure</option>`;
    const q = query(collection(db, "Infrastructure"), orderBy("createdAt", "asc"));
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
    // Set selected value after options are loaded
    if (selectedId) select.value = selectedId;
}








// ----------- Save Edited Room -----------
document.getElementById("editRoomForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    const docId = e.target.dataset.docId;
    if (!docId) {
        alert("No document ID found for update.");
        return;
    }

    const name = document.getElementById("editRoomName").value.trim();
    const roomId = document.getElementById("editRoomId").value.trim();
    const infraId = document.getElementById("editRoomInfra").value;
    const indoorType = document.getElementById("editRoomType").value;

    if (!name || !roomId || !infraId || !indoorType) {
        alert("Please fill in all required fields.");
        return;
    }

    try {
        await updateDoc(doc(db, "IndoorInfrastructure", docId), {
            name: name,
            room_id: roomId,
            infra_id: infraId,
            indoor_type: indoorType,
            updatedAt: new Date()
        });

        alert("Indoor Infrastructure updated!");
        document.getElementById("editRoomModal").style.display = "none";
        renderRoomsTable();
    } catch (err) {
        alert("Error updating indoor infrastructure: " + err);
    }
});


document.getElementById("cancelEditRoomBtn").addEventListener("click", () => {
    document.getElementById("editRoomModal").style.display = "none";
});


















// ======================= MAPS SECTION =========================

// ----------- Modal Controls -----------
async function showMapModal() {
    document.getElementById('addMapModal').style.display = 'flex';

    // ✅ Generate next ID and put it in the input
    const nextId = await generateNextMapId();
    document.getElementById("mapId").value = nextId;

    populateCampusIncludedSelect();
}

function hideMapModal() {
    document.getElementById('addMapModal').style.display = 'none';
}

window.showMapModal = showMapModal;
window.hideMapModal = hideMapModal;

// ----------- Auto-Increment Map ID (safe + numeric) -----------
async function generateNextMapId() {
    const snapshot = await getDocs(collection(db, "MapVersions"));

    let maxNum = 0;
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.map_id) {
            const num = parseInt(data.map_id.replace("MAP-", ""), 10);
            if (!isNaN(num) && num > maxNum) {
                maxNum = num;
            }
        }
    });

    // ✅ If no maps exist, return MAP-01, otherwise increment
    return `MAP-${String(maxNum + 1).padStart(2, "0")}`;
}



// ----------- Populate Campus Included Select -----------
async function populateCampusIncludedSelect() {
    const select = document.getElementById("campusIncludedSelect");
    if (!select) return;
    select.innerHTML = "";
    const q = query(collection(db, "Campus"), orderBy("createdAt", "asc"));
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















// ----------- Add Map Handler (Sequential Doc ID) -----------
document.querySelector("#addMapModal form")?.addEventListener("submit", async (e) => {
    e.preventDefault();

    const mapName = document.getElementById("mapName").value.trim();
    const campusDropdown = document.getElementById("campusDropdown");
    const campusIncluded = campusDropdown?.getSelectedValues() || [];

    if (!mapName) {
        alert("Please enter a map name.");
        return;
    }

    try {
        // ✅ Generate the next document ID in format MAP-01
        const mapsSnap = await getDocs(collection(db, "MapVersions"));
        const existingDocNumbers = mapsSnap.docs
            .map(doc => doc.id)
            .filter(id => id.startsWith("MAP-"))
            .map(id => parseInt(id.slice(4), 10))
            .filter(num => !isNaN(num));
        const nextNum = existingDocNumbers.length > 0 ? Math.max(...existingDocNumbers) + 1 : 1;
        const newDocId = `MAP-${nextNum.toString().padStart(2, "0")}`; // MAP-01, MAP-02, etc.

        // ✅ Create the new document with that ID
        const mapRef = doc(db, "MapVersions", newDocId);
        await setDoc(mapRef, {
            map_id: newDocId,
            map_name: mapName,
            campus_included: campusIncluded,
            createdAt: new Date(),
            current_version: "v1.0.0"
        });

        // ✅ Create the initial version document
        await setDoc(doc(db, "MapVersions", newDocId, "versions", "v1.0.0"), {
            nodes: [],
            edges: []
        });

        // ✅ Log Activity
        await addDoc(collection(db, "ActivityLogs"), {
            timestamp: new Date(),
            activity: "Added Map",
            item: `Map ${newDocId}`,
            description: `Created map "${mapName}" with version v1.0.0 and campuses: ${campusIncluded.join(", ") || "none"}.`
        });

        alert(`Map created successfully with document ID: ${newDocId} and version v1.0.0!`);
        e.target.reset();

        // Reset dropdown UI
        document.getElementById("selectedCampuses").textContent = "Select campuses...";
        campusDropdown?.querySelectorAll(".option").forEach(opt => opt.classList.remove("selected"));

        hideMapModal();
        renderMapsTable();
    } catch (err) {
        alert("Error creating map: " + err.message);
    }
});





async function populateCampusDropdown() {
  const container = document.getElementById("campusDropdown");
  const optionsList = document.getElementById("campusOptions");
  const selectedDisplay = document.getElementById("selectedCampuses");

  optionsList.innerHTML = "";
  let selectedValues = [];

  // Fetch campuses
  const q = query(collection(db, "Campus"), orderBy("createdAt", "asc"));
  const snapshot = await getDocs(q);

  snapshot.forEach(docSnap => {
    const data = docSnap.data();
    if (data.campus_id && data.campus_name) {
      const option = document.createElement("div");
      option.classList.add("option");
      option.dataset.value = data.campus_id;
      option.innerHTML = `
        <span>${data.campus_name}</span>
        <span class="checkmark">✔</span>
      `;

      option.addEventListener("click", () => {
        option.classList.toggle("selected");

        const value = option.dataset.value;
        if (option.classList.contains("selected")) {
          selectedValues.push(value);
        } else {
          selectedValues = selectedValues.filter(v => v !== value);
        }

        // Update display
        selectedDisplay.textContent =
          selectedValues.length > 0
            ? selectedValues.join(", ")
            : "Select campuses...";
      });

      optionsList.appendChild(option);
    }
  });

  // Toggle open/close
  container.addEventListener("click", (e) => {
    if (!e.target.closest(".options-list")) {
      container.classList.toggle("open");
    }
  });

  // Close if click outside
  document.addEventListener("click", (e) => {
    if (!container.contains(e.target)) {
      container.classList.remove("open");
    }
  });

  // Expose getter
  container.getSelectedValues = () => selectedValues;
}

populateCampusDropdown();


let mapsTableData = [];

// ----------- Load Maps Table (with current_version) -----------
async function renderMapsTable() {
    const tbody = document.querySelector(".maps-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        let maps = [];
        let campuses = [];

        if (navigator.onLine) {
            const mapsSnap = await getDocs(collection(db, "MapVersions"));
            maps = mapsSnap.docs.map(doc => ({ id: doc.id, ...doc.data() })).filter(m => !m.is_deleted);

            const campusSnap = await getDocs(collection(db, "Campus"));
            campuses = campusSnap.docs.map(doc => ({ id: doc.id, ...doc.data() }));
        } else {
            const [mapsRes, campusRes] = await Promise.all([
                fetch("../assets/firestore/MapVersions.json"),
                fetch("../assets/firestore/Campus.json")
            ]);
            maps = (await mapsRes.json()).filter(m => !m.is_deleted);
            campuses = await campusRes.json();
        }

        // Sort maps by createdAt
        maps.sort((a, b) => {
            const tA = a.createdAt?.seconds ? a.createdAt.seconds * 1000 : a.createdAt?.toMillis?.() || 0;
            const tB = b.createdAt?.seconds ? b.createdAt.seconds * 1000 : b.createdAt?.toMillis?.() || 0;
            return tA - tB;
        });

        // Build campus lookup
        const campusMap = {};
        campuses.forEach(c => campusMap[c.campus_id || c.id] = c.campus_name);

        // Store for search/filter
        mapsTableData = maps.map(data => ({
            ...data,
            campusNames: Array.isArray(data.campus_included) && data.campus_included.length > 0
                ? data.campus_included.map(id => campusMap[id] || id).join(", ")
                : "—"
        }));

        mapsTableData.forEach(data => {
            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${data.map_id || "—"}</td>
                <td>${data.map_name || "—"}</td>
                <td>${data.current_version || (data.versions && data.versions[0]?.id) || "—"}</td>
                <td>${data.campusNames}</td>
                <td class="actions">
                    <button class="edit" data-id="${data.id}"><i class="fas fa-edit"></i></button>
                    <button class="delete" data-id="${data.id}"><i class="fas fa-trash"></i></button>
                </td>
            `;
            tbody.appendChild(tr);
        });

        setupMapDeleteHandlers();
    } catch (err) {
        console.error("Error loading maps: ", err);
    }
}


function renderMapsTableRows(data) {
    const tbody = document.querySelector(".maps-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";
    data.forEach(data => {
        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>${data.map_id || "—"}</td>
            <td>${data.map_name || "—"}</td>
            <td>${data.current_version || (data.versions && data.versions[0]?.id) || "—"}</td>
            <td>${data.campusNames}</td>
            <td class="actions">
                <button class="edit" data-id="${data.id}"><i class="fas fa-edit"></i></button>
                <button class="delete" data-id="${data.id}"><i class="fas fa-trash"></i></button>
            </td>
        `;
        tbody.appendChild(tr);
    });
}



// ======================= CAMPUS SECTION =========================

// ----------- Modal Controls -----------
function showCampusModal() {
    document.getElementById('addCampusModal').style.display = 'flex';
    generateNextCampusId();
    populateMapSelect();
}
function hideCampusModal() {
    document.getElementById('addCampusModal').style.display = 'none';
}
window.showCampusModal = showCampusModal;
window.hideCampusModal = hideCampusModal;

// ----------- Auto-Increment Campus ID -----------
async function generateNextCampusId() {
    const q = query(collection(db, "Campus"));
    const snapshot = await getDocs(q);

    let maxNum = 0;
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.campus_id) {
            const num = parseInt(data.campus_id.replace("CAMP-", ""));
            if (!isNaN(num) && num > maxNum) maxNum = num;
        }
    });

    const nextId = `CAMP-${String(maxNum + 1).padStart(2, "0")}`;
    document.getElementById("campusId").value = nextId;
}

// ----------- Populate Map Select -----------
async function populateMapSelect() {
    const select = document.getElementById("mapSelect");
    if (!select) return;
    select.innerHTML = `<option value="">Select a map</option>`;
    const q = query(collection(db, "Maps"), orderBy("createdAt", "asc"));
    const snapshot = await getDocs(q);
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.map_id && data.map_name) {
            const option = document.createElement("option");
            option.value = data.map_id;
            option.textContent = data.map_name;
            option.dataset.mapName = data.map_name; // ✅ keep map name for logging
            select.appendChild(option);
        }
    });
}

// ----------- Add Campus Handler -----------
document.querySelector("#addCampusModal form")?.addEventListener("submit", async (e) => {
    e.preventDefault();
    const campusId = document.getElementById("campusId").value.trim();
    const campusName = document.getElementById("campusName").value.trim();
    const mapSelect = document.getElementById("mapSelect");
    const mapId = mapSelect.value;
    const mapName = mapSelect.options[mapSelect.selectedIndex]?.dataset.mapName || "";

    if (!campusId || !campusName || !mapId) {
        alert("Please fill in all required fields.");
        return;
    }

    try {
        // ✅ Save Campus
        await addDoc(collection(db, "Campus"), {
            campus_id: campusId,
            campus_name: campusName,
            map_id: mapId,
            createdAt: new Date()
        });

        // ✅ Save Activity Log
        await addDoc(collection(db, "ActivityLogs"), {
            timestamp: new Date(),
            activity: "Added Campus",
            item: `Campus ${campusId}`,
            description: `Added campus "${campusName}" under map "${mapName}".`
        });

        alert("Campus saved!");
        // ✅ Update StaticDataVersions/GlobalInfo after saving or updating a node
        const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
        await updateDoc(staticDataRef, {
            campus_updated: true,
        });
        e.target.reset();
        hideCampusModal();
        renderCampusTable();
    } catch (err) {
        alert("Error saving campus: " + err);
    }
});



let campusTableData = [];

// ----------- Load Campus Table -----------
async function renderCampusTable() {
    const tbody = document.querySelector(".campus-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        let campuses = [];
        let maps = [];

        if (navigator.onLine) {
            const campusSnap = await getDocs(collection(db, "Campus"));
            campuses = campusSnap.docs.map(doc => ({ id: doc.id, ...doc.data() })).filter(c => !c.is_deleted);

            const mapsSnap = await getDocs(collection(db, "MapVersions"));
            maps = mapsSnap.docs.map(doc => ({ id: doc.id, ...doc.data() })).filter(m => !m.is_deleted);
        } else {
            const [campusRes, mapsRes] = await Promise.all([
                fetch("../assets/firestore/Campus.json"),
                fetch("../assets/firestore/MapVersions.json")
            ]);
            campuses = (await campusRes.json()).filter(c => !c.is_deleted);
            maps = (await mapsRes.json()).filter(m => !m.is_deleted);
        }

        // Sort campuses by createdAt
        campuses.sort((a, b) => {
            const tA = a.createdAt?.seconds ? a.createdAt.seconds * 1000 : a.createdAt?.toMillis?.() || 0;
            const tB = b.createdAt?.seconds ? b.createdAt.seconds * 1000 : b.createdAt?.toMillis?.() || 0;
            return tA - tB;
        });

        // Build map lookup
        const mapMap = {};
        maps.forEach(m => mapMap[m.map_id || m.id] = m.map_name);

        // Store for search/filter
        campusTableData = campuses.map(data => ({
            ...data,
            mapName: mapMap[data.map_id] || data.map_id || "—"
        }));

        renderCampusTableRows(campusTableData);

        setupCampusDeleteHandlers();
    } catch (err) {
        console.error("Error loading campuses: ", err);
    }
}


function renderCampusTableRows(data) {
    const tbody = document.querySelector(".campus-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";
    data.forEach(data => {
        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>${data.campus_id}</td>
            <td>${data.campus_name}</td>
            <td>${data.mapName}</td>
            <td class="actions">
                <button class="edit"><i class="fas fa-edit"></i></button>
                <button class="delete" data-id="${data.id}"><i class="fas fa-trash"></i></button>
            </td>
        `;
        tbody.appendChild(tr);
    });
}






// ======================= EDIT CATEGORY SECTION =========================

// ----------- Open Edit Category Modal on Table Click -----------
document.querySelector("#categoriesTableBody").addEventListener("click", async (e) => {
    if (!(e.target.classList.contains("fa-edit") || (e.target.closest("button")?.classList.contains("edit")))) 
        return;

    const row = e.target.closest("tr");
    if (!row) return;

    const docId = row.dataset.id; // ✅ direct from row
    if (!docId) return;

    try {
        const docRef = doc(db, "Categories", docId);
        const docSnap = await getDoc(docRef);

        if (!docSnap.exists()) return;
        const data = docSnap.data();

        // Prefill form fields
        document.getElementById("editCategoryName").value = data.name ?? "";
        document.getElementById("editCategoryColor").value = data.color ?? "#000000"; // <-- fix: prefill color

        // Store docId in form for update
        document.getElementById("editCategoryForm").dataset.docId = docId;

        // ✅ Update StaticDataVersions/GlobalInfo after saving or updating a node
        const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
        await updateDoc(staticDataRef, {
            categories_updated: true,
        });

        // Show modal
        document.getElementById("editCategoryModal").style.display = "flex";
    } catch (err) {
        console.error("Error opening edit category modal:", err);
    }
});


// ----------- Save Edited Category -----------
document.getElementById("editCategoryForm").addEventListener("submit", async (e) => {
    e.preventDefault();

    const docId = e.target.dataset.docId;
    if (!docId) {
        alert("No document ID found for update.");
        return;
    }

    const name = document.getElementById("editCategoryName").value.trim();
    const color = document.getElementById("editCategoryColor").value;

    if (!name || !color) {
        alert("Please fill in all required fields.");
        return;
    }

    try {
        await updateDoc(doc(db, "Categories", docId), {
            name: name,
            color: color,
            updatedAt: new Date()
        });

        alert("Category updated!");
        document.getElementById("editCategoryModal").style.display = "none";
        renderCategoriesTable();
        populateCategoryDropdownForInfra();
    } catch (err) {
        alert("Error updating category: " + err);
    }
});

// ----------- Cancel Button for Edit Modal -----------
document.getElementById("cancelEditCategoryBtn").addEventListener("click", () => {
    document.getElementById("editCategoryModal").style.display = "none";
});

// ----------- Close Modal When Clicking Outside -----------
document.getElementById("editCategoryModal").addEventListener("click", (e) => {
    if (e.target === document.getElementById("editCategoryModal")) {
        document.getElementById("editCategoryModal").style.display = "none";
    }
});
















// ======================= EDIT MAP SECTION =========================

// ----------- Open Edit Map Modal -----------
document.querySelector(".maps-table tbody").addEventListener("click", async (e) => {
    if (!e.target.closest("button.edit")) return;

    const mapId = e.target.closest("button.edit").dataset.id;
    if (!mapId) return;

    try {
        const docRef = doc(db, "MapVersions", mapId);
        const mapSnap = await getDoc(docRef);
        if (!mapSnap.exists()) return;

        const mapData = mapSnap.data();

        // Prefill Map Name
        document.getElementById("editMapName").value = mapData.map_name || "";

        // Populate Campus Dropdown and preselect included campuses
        await populateEditCampusDropdown(mapData.campus_included || []);

        // Store docId for saving
        document.getElementById("editMapForm").dataset.docId = mapId;

        // Show modal
        document.getElementById("editMapModal").style.display = "flex";
    } catch (err) {
        console.error("Error opening edit map modal:", err);
    }
});

// ----------- Populate Campus Dropdown for Edit Modal (same as your Add Map dropdown) -----------
async function populateEditCampusDropdown(selectedCampuses = []) {
    const container = document.getElementById("editCampusDropdown");
    const optionsList = document.getElementById("editCampusOptions");
    const selectedDisplay = document.getElementById("editSelectedCampuses");

    optionsList.innerHTML = "";
    let selectedValues = [...selectedCampuses]; // preselected

    const q = query(collection(db, "Campus"), orderBy("createdAt", "asc"));
    const snapshot = await getDocs(q);

    snapshot.forEach(docSnap => {
        const data = docSnap.data();
        if (data.campus_id && data.campus_name) {
            const option = document.createElement("div");
            option.classList.add("option");
            option.dataset.value = data.campus_id;
            option.innerHTML = `
                <span>${data.campus_name}</span>
                <span class="checkmark">✔</span>
            `;

            if (selectedValues.includes(data.campus_id)) option.classList.add("selected");

            option.addEventListener("click", (e) => {
                e.stopPropagation();
                option.classList.toggle("selected");
                const value = option.dataset.value;
                if (option.classList.contains("selected")) {
                    if (!selectedValues.includes(value)) selectedValues.push(value);
                } else {
                    selectedValues = selectedValues.filter(v => v !== value);
                }
                selectedDisplay.textContent =
                    selectedValues.length > 0 ? selectedValues.join(", ") : "Select campuses...";
            });

            optionsList.appendChild(option);
        }
    });

    // Toggle dropdown open/close
    container.addEventListener("click", (e) => {
        if (!e.target.closest(".options-list")) {
            container.classList.toggle("open");
        }
    });

    // Close if click outside
    document.addEventListener("click", (e) => {
        if (!container.contains(e.target)) container.classList.remove("open");
    });

    container.getSelectedValues = () => selectedValues;
    selectedDisplay.textContent =
        selectedValues.length > 0 ? selectedValues.join(", ") : "Select campuses...";
}


// ----------- Save Edited Map -----------
document.getElementById("editMapForm").addEventListener("submit", async (e) => {
    e.preventDefault();

    const docId = e.target.dataset.docId;
    if (!docId) return alert("No Map ID found for update.");

    const mapName = document.getElementById("editMapName").value.trim();
    const campusDropdown = document.getElementById("editCampusDropdown");
    const campusIncluded = campusDropdown.getSelectedValues() || [];

    if (!mapName) return alert("Please enter a map name.");

    try {
        await updateDoc(doc(db, "MapVersions", docId), {
            map_name: mapName,
            campus_included: campusIncluded,
            updatedAt: new Date()
        });

        alert("Map updated successfully!");
        document.getElementById("editMapModal").style.display = "none";
        renderMapsTable();
    } catch (err) {
        alert("Error updating map: " + err.message);
    }
});

// ----------- Cancel Button -----------
document.getElementById("cancelEditMapBtn").addEventListener("click", () => {
    document.getElementById("editMapModal").style.display = "none";
});

// ----------- Close Modal on Outside Click -----------
document.getElementById("editMapModal").addEventListener("click", (e) => {
    if (e.target === document.getElementById("editMapModal")) {
        document.getElementById("editMapModal").style.display = "none";
    }
});




// ----------- Open Edit Campus Modal -----------
document.querySelector(".campus-table tbody").addEventListener("click", async (e) => {
    if (!e.target.closest("button.edit")) return;

    const row = e.target.closest("tr");
    if (!row) return;

    const campusId = row.querySelector("td")?.textContent?.trim();
    if (!campusId) return;

    try {
        const q = query(collection(db, "Campus"), where("campus_id", "==", campusId));
        const snap = await getDocs(q);
        if (snap.empty) return alert("Campus not found");

        const docSnap = snap.docs[0];
        const data = docSnap.data();

        // Prefill fields
        document.getElementById("editCampusId").value = data.campus_id || "";
        document.getElementById("editCampusName").value = data.campus_name || "";

        // Populate map dropdown and preselect current map
        await populateEditMapSelect(data.map_id);

        // Store docId for updating
        document.getElementById("editCampusForm").dataset.docId = docSnap.id;
        // ✅ Update StaticDataVersions/GlobalInfo after saving or updating a node
        const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
        await updateDoc(staticDataRef, {
            campus_updated: true,
        });

        // Show modal
        document.getElementById("editCampusModal").style.display = "flex";
    } catch (err) {
        console.error("Error opening edit campus modal:", err);
    }
});

// ----------- Populate Map Dropdown for Edit Modal -----------
async function populateEditMapSelect(selectedMapId = "") {
    const select = document.getElementById("editMapSelect");
    if (!select) return;
    select.innerHTML = `<option value="">Select a map</option>`;

    const q = query(collection(db, "Maps"), orderBy("createdAt", "asc"));
    const snapshot = await getDocs(q);

    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.map_id && data.map_name) {
            const option = document.createElement("option");
            option.value = data.map_id;
            option.textContent = data.map_name;
            option.dataset.mapName = data.map_name;
            if (data.map_id === selectedMapId) option.selected = true;
            select.appendChild(option);
        }
    });
}

// ----------- Save Edited Campus -----------
document.getElementById("editCampusForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    const docId = e.target.dataset.docId;
    if (!docId) return alert("No document ID found for update.");

    const campusName = document.getElementById("editCampusName").value.trim();
    const mapSelect = document.getElementById("editMapSelect");
    const mapId = mapSelect.value;
    const mapName = mapSelect.options[mapSelect.selectedIndex]?.dataset.mapName || "";

    if (!campusName || !mapId) return alert("Please fill in all required fields.");

    try {
        await updateDoc(doc(db, "Campus", docId), {
            campus_name: campusName,
            map_id: mapId,
            updatedAt: new Date()
        });

        // Optional: log activity
        await addDoc(collection(db, "ActivityLogs"), {
            timestamp: new Date(),
            activity: "Edited Campus",
            item: `Campus ${docId}`,
            description: `Updated campus "${campusName}" under map "${mapName}".`
        });

        alert("Campus updated successfully!");
        document.getElementById("editCampusModal").style.display = "none";
        renderCampusTable();
    } catch (err) {
        alert("Error updating campus: " + err.message);
    }
});

// ----------- Cancel Button -----------
document.getElementById("cancelEditCampusBtn").addEventListener("click", () => {
    document.getElementById("editCampusModal").style.display = "none";
});

// ----------- Close Modal on Outside Click -----------
document.getElementById("editCampusModal").addEventListener("click", (e) => {
    if (e.target === document.getElementById("editCampusModal")) {
        document.getElementById("editCampusModal").style.display = "none";
    }
});




















// ======================= UI & TAB CONTROLS =========================

// ----------- Initial Data Load -----------
window.onload = () => {
    populateCategoryDropdownForInfra();


    renderMapsTable();
    renderCampusTable();
};

// ----------- Tab Switching Logic -----------
const tabs = document.querySelectorAll('.tab');
const tables = {
    infratbl: document.querySelector('.infratbl'),
    roomstbl: document.querySelector('.roomstbl'),
    categoriestbl: document.querySelector('.categoriestbl')
};
const addButton = document.querySelector('.addbtn button');
const breadcrumbDetail = document.querySelector('.span-details'); // breadcrumb span

const buttonTexts = {
    infratbl: 'Add Infrastructure',
    roomstbl: 'Add Indoor Infrastructure',
    categoriestbl: 'Add Category'
};

// Add to your tables and buttonTexts objects:
tables.maptbl = document.querySelector('.maptbl');
tables.campustbl = document.querySelector('.campustbl');

buttonTexts.maptbl = 'Add Map';
buttonTexts.campustbl = 'Add Campus';

// New object for breadcrumb text
const breadcrumbTexts = {
    infratbl: 'Infrastructure',
    roomstbl: 'Indoor Infrastructure',
    categoriestbl: 'Categories',
    maptbl: 'Maps',
    campustbl: 'Campus'
};

tabs.forEach(tab => {
    tab.addEventListener('click', () => {
        tabs.forEach(t => t.classList.remove('active'));
        tab.classList.add('active');

        Object.values(tables).forEach(tbl => tbl.style.display = 'none');

        const target = tab.getAttribute('data-target');
        if (tables[target]) tables[target].style.display = '';
        if (buttonTexts[target]) addButton.textContent = buttonTexts[target];

        // ✅ Update breadcrumb text with a space before
        if (breadcrumbTexts[target]) breadcrumbDetail.textContent = ' ' + breadcrumbTexts[target];
    });
});

// ----------- Add Button Handler -----------
addButton.addEventListener('click', () => {
    if (addButton.textContent === 'Add Infrastructure') {
        showInfraModal();
    } else if (addButton.textContent === 'Add Indoor Infrastructure') {
        showRoomModal();
    } else if (addButton.textContent === 'Add Category') {
        showCategoryModal();
    } else if (addButton.textContent === 'Add Map') {
        showMapModal();
    } else if (addButton.textContent === 'Add Campus') {
        showCampusModal();
    }
});


// ----------- Modal Cancel Button Handlers -----------
const cancelInfraBtn = document.querySelector('#addInfraModal .cancel-btn');
const cancelRoomBtn = document.querySelector('#addRoomModal .cancel-btn');
const cancelCategoryBtn = document.querySelector('#addCategoryModal .cancel-btn');
const cancelMapBtn = document.querySelector('#addMapModal .cancel-btn');
const cancelCampusBtn = document.querySelector('#addCampusModal .cancel-btn');

cancelInfraBtn.addEventListener('click', hideInfraModal);
cancelRoomBtn.addEventListener('click', hideRoomModal);
cancelCategoryBtn.addEventListener('click', hideCategoryModal);
cancelMapBtn.addEventListener('click', hideMapModal);
cancelCampusBtn.addEventListener('click', hideCampusModal);

// ----------- Close Modal When Clicking Outside -----------
[document.getElementById('addInfraModal'), document.getElementById('addRoomModal')].forEach(modal => {
    modal.addEventListener('click', (e) => {
        if (e.target === modal) modal.style.display = 'none';
    });
});

// Close Modal When Clicking Outside:
[document.getElementById('addMapModal'), document.getElementById('addCampusModal')].forEach(modal => {
    modal.addEventListener('click', (e) => {
        if (e.target === modal) modal.style.display = 'none';
    });
});





// ======================= EDIT INFRASTRUCTURE SECTION =========================

// ----------- Edit Infrastructure Modal Open Handler -----------
document.querySelector(".infra-table").addEventListener("click", async (e) => {
    // Only respond to edit icon/button clicks
    if (
        !(e.target.classList.contains("fa-edit") ||
          (e.target.closest("button") && e.target.closest("button").classList.contains("edit")))
    ) return;

    const row = e.target.closest("tr");
    if (!row) return;

    // Get infra_id from the first cell
    const infraId = row.querySelector("td")?.textContent?.trim();
    if (!infraId) return;

    try {
        // Fetch infrastructure data from Firestore
        const infraQ = query(collection(db, "Infrastructure"), where("infra_id", "==", infraId));
        const snap = await getDocs(infraQ);

        if (snap.empty) {
            alert("Infrastructure not found in Firestore");
            return;
        }

        const docSnap = snap.docs[0];
        const infraData = docSnap.data();

        // Populate category dropdown and set value
        await populateEditInfraCategoryDropdown(infraData.category_id);
        document.getElementById("editInfraCategory").value = infraData.category_id ?? "";

        // Prefill fields
        document.getElementById("editInfraId").value = infraData.infra_id ?? "";
        document.getElementById("editInfraName").value = infraData.name ?? "";
        document.getElementById("editInfraPhone").value = infraData.phone ?? "";
        document.getElementById("editInfraEmail").value = infraData.email ?? "";

        // Image preview
        const preview = document.getElementById("editInfraPreview");
        if (infraData.image_url) {
            preview.src = infraData.image_url;
            preview.style.display = "block";
        } else {
            preview.src = "";
            preview.style.display = "none";
        }

        // Store docId for update
        document.getElementById("editInfraForm").dataset.docId = docSnap.id;

        // Show modal
        document.getElementById("editInfraModal").style.display = "flex";

    } catch (err) {
        console.error("Error opening edit modal:", err);
    }
});

// ----------- Populate Category Dropdown for Edit Modal -----------
async function populateEditInfraCategoryDropdown(selectedId) {
    const select = document.getElementById("editInfraCategory");
    if (!select) return;
    select.innerHTML = `<option value="">Select a category</option>`;
    const q = query(collection(db, "Categories"), orderBy("createdAt", "asc"));
    const snapshot = await getDocs(q);
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.category_id && data.name) {
            const option = document.createElement("option");
            option.value = data.category_id;
            option.textContent = data.name;
            select.appendChild(option);
        }
    });
    // Set selected value after options are loaded
    if (selectedId) select.value = selectedId;
}

// ----------- Save Edited Infrastructure -----------
document.getElementById("editInfraForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    const docId = e.target.dataset.docId;
    if (!docId) {
        alert("No document ID found for update.");
        return;
    }

    const name = document.getElementById("editInfraName").value.trim();
    const categoryId = document.getElementById("editInfraCategory").value;
    const phone = document.getElementById("editInfraPhone").value.trim();
    const email = document.getElementById("editInfraEmail").value.trim();

    // Handle image update (optional)
    let imageUrl = document.getElementById("editInfraPreview").src || "";
    const imageFile = document.getElementById("editInfraImage").files[0];
    if (imageFile) {
        imageUrl = await convertFileToBase64(imageFile);
    }

    if (!name || !categoryId) {
        alert("Please fill in all required fields.");
        return;
    }

    try {
        await updateDoc(doc(db, "Infrastructure", docId), {
            name: name,
            category_id: categoryId,
            phone: phone,
            email: email,
            image_url: imageUrl,
            updatedAt: new Date()
        });

        alert("Infrastructure updated!");
        document.getElementById("editInfraModal").style.display = "none";
        renderInfraTable();
    } catch (err) {
        alert("Error updating infrastructure: " + err);
    }
});

// ----------- Cancel Button for Edit Modal -----------
document.getElementById("cancelEditInfraBtn").addEventListener("click", () => {
    document.getElementById("editInfraModal").style.display = "none";
});

// ----------- Close Modal When Clicking Outside -----------
document.getElementById("editInfraModal").addEventListener("click", (e) => {
    if (e.target === document.getElementById("editInfraModal")) {
        document.getElementById("editInfraModal").style.display = "none";
    }
});















// ----------- Delete Infrastructure Modal Logic -----------

let infraToDelete = null; // Will hold {docId, name} for deletion

// Open delete modal when clicking the delete icon
document.querySelector(".infra-table").addEventListener("click", async (e) => {
    if (
        !(e.target.classList.contains("fa-trash") ||
          (e.target.closest("button") && e.target.closest("button").classList.contains("delete")))
    ) return;

    const row = e.target.closest("tr");
    if (!row) return;

    const infraId = row.querySelector("td")?.textContent?.trim();
    const infraName = row.children[1]?.textContent?.trim() || "";

    try {
        const infraQ = query(collection(db, "Infrastructure"), where("infra_id", "==", infraId));
        const snap = await getDocs(infraQ);

        if (snap.empty) {
            alert("Infrastructure not found in Firestore");
            return;
        }

        const docSnap = snap.docs[0];
        infraToDelete = { docId: docSnap.id, name: infraName };

        // Set prompt text
        document.getElementById("deleteInfraPrompt").textContent =
            `Are you sure you want to permanently delete "${infraName}"?`;

        // Show modal
        document.getElementById("deleteInfraModal").style.display = "flex";
    } catch (err) {
        console.error("Error preparing delete modal:", err);
    }
});

// Confirm deletion
document.getElementById("confirmDeleteInfraBtn").addEventListener("click", async () => {
    if (!infraToDelete) return;
    try {
        await deleteDoc(doc(db, "Infrastructure", infraToDelete.docId)); // <-- deleteDoc instead of updateDoc
        document.getElementById("deleteInfraModal").style.display = "none";
        infraToDelete = null;
        renderInfraTable(); // refresh table after deletion
    } catch (err) {
        alert("Error deleting infrastructure: " + err);
    }
});

// Cancel deletion
document.getElementById("cancelDeleteInfraBtn").addEventListener("click", () => {
    document.getElementById("deleteInfraModal").style.display = "none";
    infraToDelete = null;
});

// Close modal when clicking outside
document.getElementById("deleteInfraModal").addEventListener("click", (e) => {
    if (e.target === document.getElementById("deleteInfraModal")) {
        document.getElementById("deleteInfraModal").style.display = "none";
        infraToDelete = null;
    }
});




// ======================= DELETE ROOM SECTION =========================

let roomToDelete = null; // Will store {docId, name}

// Open delete modal when clicking the delete icon
document.querySelector(".rooms-table").addEventListener("click", async (e) => {
    if (!(e.target.classList.contains("fa-trash") ||
          (e.target.closest("button") && e.target.closest("button").classList.contains("delete")))) return;

    const row = e.target.closest("tr");
    if (!row) return;

    const roomId = row.querySelector("td")?.textContent?.trim();
    const roomName = row.children[1]?.textContent?.trim() || "";

    // Find Firestore docId for this room
    try {
        const roomQ = query(collection(db, "IndoorInfrastructure"), where("room_id", "==", roomId));
        const snap = await getDocs(roomQ);

        if (snap.empty) {
            alert("Room not found in Firestore");
            return;
        }

        const docSnap = snap.docs[0];
        roomToDelete = { docId: docSnap.id, name: roomName };

        // Set prompt text
        document.getElementById("deleteRoomPrompt").textContent =
            `Are you sure you want to delete "${roomName}"?`;

        // Show modal
        document.getElementById("deleteRoomModal").style.display = "flex";
    } catch (err) {
        console.error("Error preparing delete modal:", err);
    }
});

// Confirm deletion
document.getElementById("confirmDeleteRoomBtn").addEventListener("click", async () => {
    if (!roomToDelete) return;

    try {
        await updateDoc(doc(db, "IndoorInfrastructure", roomToDelete.docId), {
            is_deleted: true,
            deletedAt: new Date()
        });

        document.getElementById("deleteRoomModal").style.display = "none";
        roomToDelete = null;
        renderRoomsTable();
    } catch (err) {
        alert("Error deleting room: " + err);
    }
});

// Cancel deletion
document.getElementById("cancelDeleteRoomBtn").addEventListener("click", () => {
    document.getElementById("deleteRoomModal").style.display = "none";
    roomToDelete = null;
});

// Close modal when clicking outside
document.getElementById("deleteRoomModal").addEventListener("click", (e) => {
    if (e.target === document.getElementById("deleteRoomModal")) {
        document.getElementById("deleteRoomModal").style.display = "none";
        roomToDelete = null;
    }
});





// ======================= DELETE CATEGORY SECTION =========================

// ----------- Delete Category Modal Logic -----------
let categoryToDelete = null; // Will store {docId, name}

// Open delete modal when clicking the delete icon
document.querySelector(".categories-table").addEventListener("click", async (e) => {
    if (!(e.target.classList.contains("fa-trash") ||
          (e.target.closest("button") && e.target.closest("button").classList.contains("delete")))) return;

    const row = e.target.closest("tr");
    if (!row) return;

    const categoryName = row.children[1]?.textContent?.trim() || "";

    // Find Firestore docId for this category
    try {
        const catQ = query(collection(db, "Categories"), where("name", "==", categoryName));
        const snap = await getDocs(catQ);

        if (snap.empty) {
            alert("Category not found in Firestore");
            return;
        }

        const docSnap = snap.docs[0];
        categoryToDelete = { docId: docSnap.id, name: categoryName };

        // Set prompt text
        document.getElementById("deleteCategoryPrompt").textContent =
            `Are you sure you want to delete category "${categoryName}"?`;

        // Show modal
        document.getElementById("deleteCategoryModal").style.display = "flex";
    } catch (err) {
        console.error("Error preparing delete modal:", err);
    }
});



// Confirm deletion
document.getElementById("confirmDeleteCategoryBtn").addEventListener("click", async () => {
    if (!categoryToDelete) return;

    try {
        await updateDoc(doc(db, "Categories", categoryToDelete.docId), {
            is_deleted: true,
            deletedAt: new Date()
        });

        document.getElementById("deleteCategoryModal").style.display = "none";
        categoryToDelete = null;
        renderCategoriesTable();
    } catch (err) {
        alert("Error deleting category: " + err);
    }
});

// Cancel deletion
document.getElementById("cancelDeleteCategoryBtn").addEventListener("click", () => {
    document.getElementById("deleteCategoryModal").style.display = "none";
    categoryToDelete = null;
});

// Close modal when clicking outside
document.getElementById("deleteCategoryModal").addEventListener("click", (e) => {
    if (e.target === document.getElementById("deleteCategoryModal")) {
        document.getElementById("deleteCategoryModal").style.display = "none";
        categoryToDelete = null;
    }
});





let mapToDelete = null;

// ----------- Map Delete Modal Logic -----------
function setupMapDeleteHandlers() {
    const tbody = document.querySelector(".maps-table tbody");
    if (!tbody) return;

    tbody.querySelectorAll(".delete").forEach(btn => {
        btn.addEventListener("click", async () => {
            const tr = btn.closest("tr");
            const mapName = tr.children[1]?.textContent || "";
            const docId = btn.dataset.id;

            mapToDelete = { docId, name: mapName };
            document.getElementById("deleteMapPrompt").textContent =
                `Are you sure you want to delete "${mapName}"?`;
            document.getElementById("deleteMapModal").style.display = "flex";
        });
    });
}

// ----------- Confirm Map Deletion -----------
document.getElementById("confirmDeleteMapBtn").addEventListener("click", async () => {
    if (!mapToDelete) return;
    try {
        await updateDoc(doc(db, "MapVersions", mapToDelete.docId), {
            is_deleted: true,
            deletedAt: new Date()
        });
        document.getElementById("deleteMapModal").style.display = "none";
        mapToDelete = null;
        renderMapsTable();
    } catch (err) {
        alert("Error deleting map: " + err);
    }
});

// ----------- Cancel Map Deletion -----------
document.getElementById("cancelDeleteMapBtn").addEventListener("click", () => {
    document.getElementById("deleteMapModal").style.display = "none";
    mapToDelete = null;
});

// ----------- Close modal if click outside -----------
document.getElementById("deleteMapModal").addEventListener("click", (e) => {
    if (e.target === document.getElementById("deleteMapModal")) {
        document.getElementById("deleteMapModal").style.display = "none";
        mapToDelete = null;
    }
});




let campusToDelete = null;

// ----------- Campus Delete Modal Logic -----------
function setupCampusDeleteHandlers() {
    const tbody = document.querySelector(".campus-table tbody");
    if (!tbody) return;

    tbody.querySelectorAll(".delete").forEach(btn => {
        btn.addEventListener("click", () => {
            const tr = btn.closest("tr");
            const campusName = tr.children[1]?.textContent || "";
            const docId = btn.dataset.id;

            campusToDelete = { docId, name: campusName };
            document.getElementById("deleteCampusPrompt").textContent =
                `Are you sure you want to delete "${campusName}"?`;
            document.getElementById("deleteCampusModal").style.display = "flex";
        });
    });
}

// ----------- Confirm Campus Deletion -----------
document.getElementById("confirmDeleteCampusBtn").addEventListener("click", async () => {
    if (!campusToDelete) return;
    try {
        await updateDoc(doc(db, "Campus", campusToDelete.docId), {
            is_deleted: true,
            deletedAt: new Date()
        });
        document.getElementById("deleteCampusModal").style.display = "none";
        campusToDelete = null;
        renderCampusTable();
    } catch (err) {
        alert("Error deleting campus: " + err);
    }
});

// ----------- Cancel Campus Deletion -----------
document.getElementById("cancelDeleteCampusBtn").addEventListener("click", () => {
    document.getElementById("deleteCampusModal").style.display = "none";
    campusToDelete = null;
});

// ----------- Close modal if click outside -----------
document.getElementById("deleteCampusModal").addEventListener("click", (e) => {
    if (e.target === document.getElementById("deleteCampusModal")) {
        document.getElementById("deleteCampusModal").style.display = "none";
        campusToDelete = null;
    }
});



async function populateSortDropdown() {
    const sortSelect = document.getElementById("sortCategory");
    if (!sortSelect) return;

    sortSelect.innerHTML = `<option value="">Sort</option>`;
    const q = query(collection(db, "Categories"), orderBy("createdAt", "asc"));
    const snapshot = await getDocs(q);
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.category_id && data.name && !data.is_deleted) {
            const option = document.createElement("option");
            option.value = data.category_id;
            option.textContent = data.name;
            sortSelect.appendChild(option);
        }
    });
}
document.addEventListener("DOMContentLoaded", populateSortDropdown);


document.getElementById("searchInput").addEventListener("input", function() {
    const val = this.value.trim().toLowerCase();

    // Infrastructure tab
    if (tables.infratbl.style.display !== "none") {
        let filtered = infraTableData.filter(d =>
            d.name.toLowerCase().includes(val) ||
            d.infra_id.toLowerCase().includes(val) ||
            d.categoryName.toLowerCase().includes(val) ||
            (d.phone || "").toLowerCase().includes(val) ||
            (d.email || "").toLowerCase().includes(val)
        );
        const sortVal = document.getElementById("sortCategory").value;
        if (sortVal) {
            filtered = filtered.filter(d => String(d.category_id).trim() === sortVal);
        }
        renderInfraTableRows(filtered);

    // Room tab
    } else if (tables.roomstbl.style.display !== "none") {
        let filtered = roomsTableData.filter(r =>
            r.name.toLowerCase().includes(val) ||
            r.room_id.toLowerCase().includes(val) ||
            (r.infraName || "").toLowerCase().includes(val) ||
            (r.indoor_type || "").toLowerCase().includes(val)
        );
        const sortVal = document.getElementById("sortInfra")?.value;
        if (sortVal) {
            filtered = filtered.filter(r => String(r.infra_id).trim() === sortVal);
        }
        renderRoomsTableRows(filtered);
    } else if (tables.categoriestbl.style.display !== "none") {
        let filtered = categoriesTableData.filter(c =>
            c.name.toLowerCase().includes(val) ||
            (c.color || "").toLowerCase().includes(val)
        );
        renderCategoriesTableRows(filtered);
    } else if (tables.maptbl.style.display !== "none") {
        let filtered = mapsTableData.filter(m =>
            (m.map_id || "").toLowerCase().includes(val) ||
            (m.map_name || "").toLowerCase().includes(val) ||
            (m.current_version || "").toLowerCase().includes(val) ||
            (m.campusNames || "").toLowerCase().includes(val)
        );
        renderMapsTableRows(filtered);
    } else if (tables.campustbl.style.display !== "none") {
        let filtered = campusTableData.filter(c =>
            (c.campus_id || "").toLowerCase().includes(val) ||
            (c.campus_name || "").toLowerCase().includes(val) ||
            (c.mapName || "").toLowerCase().includes(val)
        );
        renderCampusTableRows(filtered);
    }
});