// ======================= FIREBASE SETUP ===========================
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
import { getFirestore, collection, addDoc, getDocs, query, orderBy, where, updateDoc, doc } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";
import { firebaseConfig } from "./../firebaseConfig.js";

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

// ----------- File to Base64 Utility -----------
function convertFileToBase64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = error => reject(error);
        reader.readAsDataURL(file);
    });
}

// ----------- Load Categories Table -----------
async function renderCategoriesTable() {
    const tbody = document.getElementById("categoriesTableBody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        const querySnapshot = await getDocs(collection(db, "Categories"));
        const categories = querySnapshot.docs.map(doc => doc.data());
        categories.sort((a, b) => a.createdAt.toMillis() - b.createdAt.toMillis());

        categories.forEach((data, index) => {
            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${index + 1}</td>
                <td>${data.name}</td>
                <td>${data.icon ? `<img src="${data.icon}" alt="${data.name}" style="width:24px;height:24px;">` : ""}</td>
                <td>${data.buildings || 0}</td>
                <td class="actions">
                    <button class="edit"><i class="fas fa-edit"></i></button>
                    <button class="delete"><i class="fas fa-trash"></i></button>
                </td>
            `;
            tbody.appendChild(tr);
        });
    } catch (err) {
        console.error("Error loading categories: ", err);
    }
}

// ----------- Add Category Handler -----------
document.getElementById('categoryForm')?.addEventListener('submit', async (e) => {
    e.preventDefault();

    const name = document.getElementById('categoryName').value;
    const iconFile = document.getElementById('categoryIcon').files[0];
    let iconBase64 = '';
    if (iconFile) iconBase64 = await convertFileToBase64(iconFile);

    try {
        await addDoc(collection(db, "Categories"), {
            category_id: Date.now().toString(),
            name: name,
            icon: iconBase64,
            buildings: 0,
            is_deleted: false,
            createdAt: new Date()
        });

        alert("Category saved!");
        document.getElementById('categoryForm').reset();
        hideCategoryModal();
        renderCategoriesTable();
        populateCategoryDropdownForBuildings();
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
            const num = parseInt(data.infra_id.replace("INFA-", ""));
            if (!isNaN(num) && num > maxNum) maxNum = num;
        }
    });

    const nextId = `INFA-${String(maxNum + 1).padStart(2, "0")}`;
    document.getElementById("infraId").value = nextId;
}

// ----------- Add Infrastructure Handler -----------
document.querySelector("#addInfraModal form")?.addEventListener("submit", async (e) => {
    e.preventDefault();

    const name = document.querySelector('#addInfraModal input[placeholder="e.g. Main Library"]').value.trim();
    const infraId = document.getElementById("infraId").value.trim();
    const categoryId = document.querySelector('#addInfraModal select').value;
    const phone = document.querySelector('#addInfraModal input[type="text"][placeholder="e.g. 09123456789"]').value.trim();
    const email = document.querySelector('#addInfraModal input[type="email"]').value.trim();
    // Image upload (optional)
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

        alert("Infrastructure saved successfully!");
        e.target.reset();
        hideInfraModal();
        renderInfraTable();
    } catch (err) {
        console.error("Error adding infrastructure:", err);
        alert("Error saving infrastructure.");
    }
});

// ----------- Load Infrastructure Table -----------
async function renderInfraTable() {
    const tbody = document.querySelector(".infra-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        const querySnapshot = await getDocs(collection(db, "Infrastructure"));
        const infras = querySnapshot.docs.map(doc => doc.data()).filter(data => !data.is_deleted);
        infras.sort((a, b) => a.createdAt.toMillis() - b.createdAt.toMillis());

        // Get all categories once
        const catSnap = await getDocs(collection(db, "Categories"));
        const catMap = {};
        catSnap.forEach(c => {
            const data = c.data();
            catMap[data.category_id] = data.name;
        });

        for (const data of infras) {
            const categoryName = catMap[data.category_id] || "N/A";
            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${data.infra_id}</td>
                <td>${data.name}</td>
                <td>${categoryName}</td>
                <td>${data.phone || ""}</td>
                <td>${data.email || ""}</td>
                <td class="actions">
                    <button class="edit"><i class="fas fa-edit"></i></button>
                    <button class="delete"><i class="fas fa-trash"></i></button>
                </td>
            `;
            tbody.appendChild(tr);
        }
    } catch (err) {
        console.error("Error loading infrastructure: ", err);
    }
}


// ======================= ROOM SECTION =========================

// ----------- Modal Controls -----------
function showRoomModal() {
    document.getElementById('addRoomModal').style.display = 'flex';
    generateNextRoomId();
    populateBuildingDropdownForRooms();
}
function hideRoomModal() {
    document.getElementById('addRoomModal').style.display = 'none';
}
window.showRoomModal = showRoomModal;
window.hideRoomModal = hideRoomModal;

// ----------- Auto-Increment Room ID -----------
async function generateNextRoomId() {
    const q = query(collection(db, "Rooms"));
    const snapshot = await getDocs(q);

    let maxNum = 0;
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.room_id) {
            const num = parseInt(data.room_id.replace("RM-", ""));
            if (!isNaN(num) && num > maxNum) maxNum = num;
        }
    });

    const nextId = `RM-${String(maxNum + 1).padStart(3, "0")}`;
    document.getElementById("roomId").value = nextId;
}

// ----------- Populate Building Dropdown for Rooms -----------
async function populateBuildingDropdownForRooms() {
    const select = document.querySelector("#addRoomModal select");
    if (!select) return;

    select.innerHTML = `<option value="">Select a building</option>`;
    const q = query(collection(db, "Buildings"), orderBy("createdAt", "asc"));
    const snapshot = await getDocs(q);

    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.building_id && data.name) {
            const option = document.createElement("option");
            option.value = data.building_id;
            option.textContent = data.name;
            select.appendChild(option);
        }
    });
}

// ----------- Add Room Handler -----------
document.querySelector("#addRoomModal form")?.addEventListener("submit", async (e) => {
    e.preventDefault();

    const name = document.querySelector('#addRoomModal input[placeholder="e.g. Lecture Room 1"]').value.trim();
    const roomId = document.querySelector('#addRoomModal input[name="room_id"]').value.trim();
    const buildingId = document.querySelector('#addRoomModal select').value;
    const latitude = document.querySelector('#addRoomModal input[placeholder="Latitude"]').value.trim();
    const longitude = document.querySelector('#addRoomModal input[placeholder="Longitude"]').value.trim();
    const location = document.querySelector('#addRoomModal input[placeholder="e.g. Near Gate 6 and College of Engineering"]').value.trim();
    const phone = document.querySelector('#addRoomModal input[type="text"][placeholder=""]').value.trim();

    if (!name || !roomId || !buildingId || !latitude || !longitude || !location) {
        alert("Please fill in all required fields.");
        return;
    }

    try {
        await addDoc(collection(db, "Rooms"), {
            room_id: roomId,
            name: name,
            building_id: buildingId,
            location: location,
            latitude: latitude,
            longitude: longitude,
            phone: phone || "",
            is_deleted: false,
            createdAt: new Date()
        });

        alert("Room saved successfully!");
        e.target.reset();
        hideRoomModal();
        renderRoomsTable();
    } catch (err) {
        console.error("Error adding room:", err);
        alert("Error saving room.");
    }
});

// ----------- Load Rooms Table -----------
async function renderRoomsTable() {
    const tbody = document.querySelector(".rooms-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        const querySnapshot = await getDocs(collection(db, "Rooms"));
        const rooms = querySnapshot.docs.map(doc => doc.data());
        rooms.sort((a, b) => a.createdAt.toMillis() - b.createdAt.toMillis());

        // Get all buildings once to avoid many queries
        const buildingSnap = await getDocs(collection(db, "Buildings"));
        const buildingsMap = {};
        buildingSnap.forEach(b => {
            const data = b.data();
            buildingsMap[data.building_id] = data.name;
        });

        for (const data of rooms) {
            const buildingName = buildingsMap[data.building_id] || "N/A";
            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${data.room_id}</td>
                <td>${data.name}</td>
                <td>${buildingName}</td>
                <td>${data.location}</td>
                <td>${data.latitude}, ${data.longitude}</td>
                <td class="actions">
                    <button class="edit"><i class="fas fa-edit"></i></button>
                    <button class="delete"><i class="fas fa-trash"></i></button>
                </td>
            `;
            tbody.appendChild(tr);
        }
    } catch (err) {
        console.error("Error loading rooms: ", err);
    }
}


// ======================= MAPS SECTION =========================

// ----------- Modal Controls -----------
function showMapModal() {
    document.getElementById('addMapModal').style.display = 'flex';
    generateNextMapId();
    populateCampusIncludedSelect();
}
function hideMapModal() {
    document.getElementById('addMapModal').style.display = 'none';
}
window.showMapModal = showMapModal;
window.hideMapModal = hideMapModal;

// ----------- Auto-Increment Map ID -----------
async function generateNextMapId() {
    const q = query(collection(db, "Maps"));
    const snapshot = await getDocs(q);

    let maxNum = 0;
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.map_id) {
            const num = parseInt(data.map_id.replace("MAP-", ""));
            if (!isNaN(num) && num > maxNum) maxNum = num;
        }
    });

    const nextId = `MAP-${String(maxNum + 1).padStart(2, "0")}`;
    document.getElementById("mapId").value = nextId;
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

// ----------- Add Map Handler -----------
document.querySelector("#addMapModal form")?.addEventListener("submit", async (e) => {
    e.preventDefault();
    const mapId = document.getElementById("mapId").value.trim();
    const mapName = document.getElementById("mapName").value.trim();

    // ✅ Get selected campuses from custom dropdown
    const campusDropdown = document.getElementById("campusDropdown");
    const campusIncluded = campusDropdown.getSelectedValues();

    if (!mapId || !mapName || campusIncluded.length === 0) {
        alert("Please fill in all required fields.");
        return;
    }

    try {
        await addDoc(collection(db, "Maps"), {
            map_id: mapId,
            map_name: mapName,
            campus_included: campusIncluded, // ✅ array now
            createdAt: new Date()
        });
        alert("Map saved!");
        e.target.reset();

        // reset dropdown display
        document.getElementById("selectedCampuses").textContent = "Select campuses...";
        campusDropdown.querySelectorAll(".option").forEach(opt => opt.classList.remove("selected"));

        hideMapModal();
        renderMapsTable();
    } catch (err) {
        alert("Error saving map: " + err);
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


// ----------- Load Maps Table -----------
async function renderMapsTable() {
    const tbody = document.querySelector(".maps-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        const querySnapshot = await getDocs(collection(db, "Maps"));
        const maps = querySnapshot.docs.map(doc => doc.data());
        maps.sort((a, b) => a.createdAt.toMillis() - b.createdAt.toMillis());

        // Get all campuses for display
        const campusSnap = await getDocs(collection(db, "Campus"));
        const campusMap = {};
        campusSnap.forEach(c => {
            const data = c.data();
            campusMap[data.campus_id] = data.campus_name;
        });

        for (const data of maps) {
            let campusNames = "";
            if (Array.isArray(data.campus_included)) {
                campusNames = data.campus_included
                    .map(id => campusMap[id] || id)
                    .join(", ");
            } else {
                campusNames = campusMap[data.campus_included] || data.campus_included;
            }

            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${data.map_id}</td>
                <td>${data.map_name}</td>
                <td>${campusNames}</td>
                <td class="actions">
                    <button class="edit"><i class="fas fa-edit"></i></button>
                    <button class="delete"><i class="fas fa-trash"></i></button>
                </td>
            `;
            tbody.appendChild(tr);
        }
    } catch (err) {
        console.error("Error loading maps: ", err);
    }
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
            select.appendChild(option);
        }
    });
}

// ----------- Add Campus Handler -----------
document.querySelector("#addCampusModal form")?.addEventListener("submit", async (e) => {
    e.preventDefault();
    const campusId = document.getElementById("campusId").value.trim();
    const campusName = document.getElementById("campusName").value.trim();
    const mapId = document.getElementById("mapSelect").value;

    if (!campusId || !campusName || !mapId) {
        alert("Please fill in all required fields.");
        return;
    }

    try {
        await addDoc(collection(db, "Campus"), {
            campus_id: campusId,
            campus_name: campusName,
            map_id: mapId,
            createdAt: new Date()
        });
        alert("Campus saved!");
        e.target.reset();
        hideCampusModal();
        renderCampusTable();
    } catch (err) {
        alert("Error saving campus: " + err);
    }
});

// ----------- Load Campus Table -----------
async function renderCampusTable() {
    const tbody = document.querySelector(".campus-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        const querySnapshot = await getDocs(collection(db, "Campus"));
        const campuses = querySnapshot.docs.map(doc => doc.data());
        campuses.sort((a, b) => a.createdAt.toMillis() - b.createdAt.toMillis());

        // Get all maps for display
        const mapSnap = await getDocs(collection(db, "Maps"));
        const mapMap = {};
        mapSnap.forEach(m => {
            const data = m.data();
            mapMap[data.map_id] = data.map_name;
        });

        for (const data of campuses) {
            const mapName = mapMap[data.map_id] || data.map_id;
            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${data.campus_id}</td>
                <td>${data.campus_name}</td>
                <td>${mapName}</td>
                <td class="actions">
                    <button class="edit"><i class="fas fa-edit"></i></button>
                    <button class="delete"><i class="fas fa-trash"></i></button>
                </td>
            `;
            tbody.appendChild(tr);
        }
    } catch (err) {
        console.error("Error loading campuses: ", err);
    }
}









// ======================= UI & TAB CONTROLS =========================

// ----------- Initial Data Load -----------
window.onload = () => {
    renderCategoriesTable();
    populateCategoryDropdownForInfra();
    renderInfraTable();
    renderRoomsTable();
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
const buttonTexts = {
    infratbl: 'Add Infrastructure',
    roomstbl: 'Add Room',
    categoriestbl: 'Add Category'
};
// Add to your tables and buttonTexts objects:
tables.maptbl = document.querySelector('.maptbl');
tables.campustbl = document.querySelector('.campustbl');
buttonTexts.maptbl = 'Add Map';
buttonTexts.campustbl = 'Add Campus';

tabs.forEach(tab => {
    tab.addEventListener('click', () => {
        tabs.forEach(t => t.classList.remove('active'));
        tab.classList.add('active');
        Object.values(tables).forEach(tbl => tbl.style.display = 'none');
        const target = tab.getAttribute('data-target');
        if (tables[target]) tables[target].style.display = '';
        if (buttonTexts[target]) addButton.textContent = buttonTexts[target];
    });
});

// Extend Add Button Handler:
addButton.addEventListener('click', () => {
    if (addButton.textContent === 'Add Infrastructure') {
        showInfraModal();
    } else if (addButton.textContent === 'Add Room') {
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

    // Find the Firestore docId for this infra
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
            `Are you sure you want to delete "${infraName}"?`;

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
        await updateDoc(doc(db, "Infrastructure", infraToDelete.docId), {
            is_deleted: true,
            deletedAt: new Date()
        });
        document.getElementById("deleteInfraModal").style.display = "none";
        infraToDelete = null;
        renderInfraTable();
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