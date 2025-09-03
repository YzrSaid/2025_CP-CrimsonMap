// ======================= FIREBASE SETUP ===========================
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
import { getFirestore, collection, addDoc, getDocs, query, orderBy } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";
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
        const infras = querySnapshot.docs.map(doc => doc.data());
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


// ======================= UI & TAB CONTROLS =========================

// ----------- Initial Data Load -----------
window.onload = () => {
    renderCategoriesTable();
    populateCategoryDropdownForInfra();
    renderInfraTable();
    renderRoomsTable();
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

// ----------- Add Button Handler -----------
addButton.addEventListener('click', () => {
    if (addButton.textContent === 'Add Infrastructure') {
        showInfraModal();
    } else if (addButton.textContent === 'Add Room') {
        showRoomModal();
    } else if (addButton.textContent === 'Add Category') {
        showCategoryModal();
    }
});

// ----------- Modal Cancel Button Handlers -----------
const cancelInfraBtn = document.querySelector('#addInfraModal .cancel-btn');
const cancelRoomBtn = document.querySelector('#addRoomModal .cancel-btn');
const cancelCategoryBtn = document.querySelector('#addCategoryModal .cancel-btn');

cancelInfraBtn.addEventListener('click', hideInfraModal);
cancelRoomBtn.addEventListener('click', hideRoomModal);
cancelCategoryBtn.addEventListener('click', hideCategoryModal);

// ----------- Close Modal When Clicking Outside -----------
[document.getElementById('addInfraModal'), document.getElementById('addRoomModal')].forEach(modal => {
    modal.addEventListener('click', (e) => {
        if (e.target === modal) modal.style.display = 'none';
    });
});