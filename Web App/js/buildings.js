import { 
    initializeApp 
} from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";

import { 
    getFirestore, collection, addDoc, getDocs, query, orderBy 
} from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";

// Import config from another file
import { firebaseConfig } from "./../firebaseConfig.js";

// Init Firebase
const app = initializeApp(firebaseConfig);
const db = getFirestore(app);

// =================== CATEGORY FUNCTIONS ===================

// Modal open/close (for categories)
function openModal() { document.getElementById('addCategoryModal').style.display = 'block'; }
function closeModal() { document.getElementById('addCategoryModal').style.display = 'none'; }
window.openModal = openModal;
window.closeModal = closeModal;

// Convert file to Base64 (for category icon)
function fileToBase64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = error => reject(error);
        reader.readAsDataURL(file);
    });
}

// Load categories into table
async function loadCategories() { 
    const tbody = document.getElementById("categoriesTableBody");
    if (!tbody) return; // prevent error if table is not present
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

// Handle category form submit
document.getElementById('categoryForm')?.addEventListener('submit', async (e) => {
    e.preventDefault();

    const name = document.getElementById('categoryName').value;
    const iconFile = document.getElementById('categoryIcon').files[0];

    let iconBase64 = '';
    if (iconFile) iconBase64 = await fileToBase64(iconFile);

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
        closeModal();
        loadCategories();
        loadCategoriesIntoDropdown(); // refresh dropdown for buildings
    } catch (err) { 
        alert("Error adding category: " + err); 
    } 
});

// =================== BUILDING FUNCTIONS ===================

function openBuildingModal() { 
    document.getElementById('addBuildingModal').style.display = 'flex'; 
    generateNextBuildingId(); // auto-generate ID every time modal opens 
}
function closeBuildingModal() {
    document.getElementById('addBuildingModal').style.display = 'none';
}
window.openBuildingModal = openBuildingModal;
window.closeBuildingModal = closeBuildingModal;

// =============== AUTO INCREMENT BUILDING ID =============== 
async function generateNextBuildingId() { 
    const q = query(collection(db, "Buildings")); 
    const snapshot = await getDocs(q);

    let maxNum = 0;
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.building_id) { 
            const num = parseInt(data.building_id.replace("BLD-", "")); 
            if (!isNaN(num) && num > maxNum) maxNum = num; 
        }
    });

    const nextId = `BLD-${String(maxNum + 1).padStart(3, "0")}`;
    document.getElementById("buildingId").value = nextId; 
}

// Load categories into dropdown (inside Add Building modal)
async function loadCategoriesIntoDropdown() {
    const categorySelect = document.querySelector("#addBuildingModal select");
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

// Handle add building form submit
document.querySelector("#addBuildingModal form")?.addEventListener("submit", async (e) => {
    e.preventDefault();

    const name = document.querySelector('#addBuildingModal input[placeholder="e.g. College of Nursing"]').value.trim();
    const buildingId = document.getElementById("buildingId").value.trim();
    const categoryId = document.querySelector('#addBuildingModal select').value;
    const latitude = document.querySelector('#addBuildingModal input[placeholder="Latitude"]').value.trim();
    const longitude = document.querySelector('#addBuildingModal input[placeholder="Longitude"]').value.trim();
    const location = document.querySelector('#addBuildingModal input[placeholder="e.g. Near Gate 6 and College of Engineering"]').value.trim();
    const phone = document.querySelector('#addBuildingModal input[type="text"][placeholder=""]').value.trim();

    if (!name || !buildingId || !categoryId || !latitude || !longitude || !location) {
        alert("Please fill in all required fields.");
        return;
    }

    try {
        await addDoc(collection(db, "Buildings"), {
            building_id: buildingId,
            name: name,
            category_id: categoryId,
            location: location,
            latitude: latitude,
            longitude: longitude,
            image_url: "",
            facility: "",
            email: "",
            phone: phone || "",
            is_deleted: false,
            createdAt: new Date()
        });

        alert("Building saved successfully!");
        e.target.reset();
        document.getElementById('addBuildingModal').style.display = 'none';
        loadBuildings(); // refresh table
    } catch (err) {
        console.error("Error adding building:", err);
        alert("Error saving building.");
    }
});

// Load buildings into table
async function loadBuildings() {
    const tbody = document.querySelector(".building-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        const querySnapshot = await getDocs(collection(db, "Buildings"));
        const buildings = querySnapshot.docs.map(doc => doc.data());

        // Sort by createdAt ascending
        buildings.sort((a, b) => a.createdAt.toMillis() - b.createdAt.toMillis());

        for (const data of buildings) {
            // fetch category name
            let categoryName = "N/A";
            if (data.category_id) {
                const cats = await getDocs(collection(db, "Categories"));
                const match = cats.docs.find(c => c.data().category_id === data.category_id);
                if (match) categoryName = match.data().name;
            }

            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${data.building_id}</td>
                <td>${data.name}</td>
                <td>${categoryName}</td>
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
        console.error("Error loading buildings: ", err);
    }
}

// =================== ROOM FUNCTIONS ===================

// Open / Close modal
function openRoomModal() {
    document.getElementById('addRoomModal').style.display = 'flex';
    generateNextRoomId();   // auto-generate ID
    loadBuildingsIntoDropdown(); // populate building dropdown
}
function closeRoomModal() {
    document.getElementById('addRoomModal').style.display = 'none';
}
window.openRoomModal = openRoomModal;
window.closeRoomModal = closeRoomModal;

// =============== AUTO INCREMENT ROOM ID =============== 
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


// =============== LOAD BUILDINGS INTO DROPDOWN =============== 
async function loadBuildingsIntoDropdown() {
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

// =============== HANDLE ADD ROOM SUBMIT =============== 
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
        document.getElementById('addRoomModal').style.display = 'none';
        loadRooms(); // refresh table
    } catch (err) {
        console.error("Error adding room:", err);
        alert("Error saving room.");
    }
});

// =============== LOAD ROOMS INTO TABLE =============== 
async function loadRooms() {
    const tbody = document.querySelector(".rooms-table tbody");
    if (!tbody) return;
    tbody.innerHTML = "";

    try {
        const querySnapshot = await getDocs(collection(db, "Rooms"));
        const rooms = querySnapshot.docs.map(doc => doc.data());

        // Sort by createdAt ascending
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

// =================== INITIAL LOAD =================== 
window.onload = () => {
    loadCategories(); // for building modal
    loadCategoriesIntoDropdown(); 
    loadBuildings();
    loadRooms();
};


const tabs = document.querySelectorAll('.tab');
const tables = {
    buildtbl: document.querySelector('.buildtbl'),
    roomstbl: document.querySelector('.roomstbl'),
    categoriestbl: document.querySelector('.categoriestbl')
};

const addButton = document.querySelector('.addbtn button');

// Define button text for each tab
const buttonTexts = {
    buildtbl: 'Add Building',
    roomstbl: 'Add Room',
    categoriestbl: 'Add Category'
};

// Tab switching logic
tabs.forEach(tab => {
    tab.addEventListener('click', () => {
        tabs.forEach(t => t.classList.remove('active'));
        tab.classList.add('active');

        Object.values(tables).forEach(tbl => tbl.style.display = 'none');

        const target = tab.getAttribute('data-target');
        if (tables[target]) {
            tables[target].style.display = '';
        }

        if (buttonTexts[target]) {
            addButton.textContent = buttonTexts[target];
        }
    });
});

// Get modals
const addBuildingModal = document.getElementById('addBuildingModal');
const cancelBuildingBtn = document.querySelector('#addBuildingModal .cancel-btn');

const addRoomModal = document.getElementById('addRoomModal');
const cancelRoomBtn = document.querySelector('#addRoomModal .cancel-btn');

const addCategoryModal = document.getElementById('addCategoryModal');
const cancelCategoryBtn = document.querySelector('#addCategoryModal .cancel-btn');

// One click handler for the add button
addButton.addEventListener('click', () => {
    if (addButton.textContent === 'Add Building') {
        addBuildingModal.style.display = 'flex';
        window.openBuildingModal();
    } 
    else if (addButton.textContent === 'Add Room') {
        addRoomModal.style.display = 'flex';
        window.openRoomModal();
    }
    else if (addButton.textContent === 'Add Category') {
        addCategoryModal.style.display = 'flex';
    }
});

// Close modals when Cancel is clicked
cancelBuildingBtn.addEventListener('click', () => {
    addBuildingModal.style.display = 'none';
});
cancelRoomBtn.addEventListener('click', () => {
    addRoomModal.style.display = 'none';
});

cancelCategoryBtn.addEventListener('click', () => {
    addCategoryModal.style.display = 'none';
});

// Close when clicking outside the modal
[addBuildingModal, addRoomModal].forEach(modal => {
    modal.addEventListener('click', (e) => {
        if (e.target === modal) {
            modal.style.display = 'none';
        }
    });
});

window.openBuildingModal = openBuildingModal;
window.closeBuildingModal = closeBuildingModal;