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

async function renderCategoriesTable() {
  const tbody = document.getElementById("categoriesTableBody");
  if (!tbody) return;

  // 🌀 Show loader before fetching
  tbody.innerHTML = `
    <tr>
      <td colspan="5" class="table-loader">
        <div class="spinner"></div>
      </td>
    </tr>
  `;

  try {
    let categories = [];
    let infrastructures = [];

    // ===== Load Categories =====
    if (navigator.onLine) {
      const catSnap = await getDocs(collection(db, "Categories"));
      categories = catSnap.docs
        .map(doc => ({ id: doc.id, ...doc.data() }))
        .filter(data => !data.is_deleted);

      // Also fetch infrastructures
      const infraSnap = await getDocs(collection(db, "Infrastructure"));
      infrastructures = infraSnap.docs
        .map(doc => doc.data())
        .filter(data => !data.is_deleted);
    } else {
      // ===== Offline fallback =====
      const [catRes, infraRes] = await Promise.all([
        fetch("../assets/firestore/Categories.json"),
        fetch("../assets/firestore/Infrastructure.json"),
      ]);
      const [catData, infraData] = await Promise.all([
        catRes.json(),
        infraRes.json(),
      ]);
      categories = catData.filter(data => !data.is_deleted);
      infrastructures = infraData.filter(data => !data.is_deleted);
    }

    // ===== Count infrastructures per category =====
    const infraCountMap = {};
    infrastructures.forEach(infra => {
      if (infra.category_id) {
        infraCountMap[infra.category_id] =
          (infraCountMap[infra.category_id] || 0) + 1;
      }
    });

    // ===== Attach building counts to categories =====
    categories.forEach(cat => {
      cat.buildings = infraCountMap[cat.category_id] || 0;
    });

    // ===== Sort by createdAt =====
    categories.sort((a, b) => {
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

    // Save globally for filtering/searching
    categoriesTableData = categories;

    // Render data
    renderCategoriesTableRows(categoriesTableData);
  } catch (err) {
    console.error("❌ Error loading categories:", err);
    tbody.innerHTML = `
      <tr>
        <td colspan="5" style="text-align:center; color:#DC143C;">
          ⚠️ Failed to load categories
        </td>
      </tr>
    `;
  }
}

function renderCategoriesTableRows(data) {
  const tbody = document.getElementById("categoriesTableBody");
  if (!tbody) return;
  tbody.innerHTML = "";

  if (!data.length) {
    tbody.innerHTML = `
      <tr>
        <td colspan="5" style="text-align:center; color:#999;">
          No categories found.
        </td>
      </tr>
    `;
    return;
  }

  data.forEach((data, index) => {
    const tr = document.createElement("tr");
    tr.dataset.id = data.id;
    tr.innerHTML = `
      <td>${index + 1}</td>
      <td>${data.name}</td>
      <td>
        <span class="category-color" style="background:#e5e7eb; color:#111827; padding:4px 8px; border-radius:6px;">
          ${data.legend || "—"}
        </span>
      </td>
      <td>${data.buildings}</td>
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




// // ----------- Add Category Handler with Loading Spinner + Saving Text -----------
// document.getElementById('categoryForm')?.addEventListener('submit', async (e) => {
//     e.preventDefault();

//     const submitBtn = e.target.querySelector(".save-btn");
//     if (!submitBtn) return;

//     const originalBtnHTML = submitBtn.innerHTML;

//     // 🟢 Show spinner + "Saving..." text + disable button
//     submitBtn.innerHTML = `
//         <div class="spinner"></div>
//         <span class="loading-text">Saving...</span>
//     `;
//     submitBtn.disabled = true;

//     const name = document.getElementById('categoryName').value.trim();
//     const color = document.getElementById('categoryColor').value;

//     if (!name || !color) {
//         alert("Please fill in all required fields.");
//         submitBtn.innerHTML = originalBtnHTML;
//         submitBtn.disabled = false;
//         return;
//     }

//     try {
//         // Generate next category ID in format CAT-01, CAT-02, etc.
//         let nextNum = 1;
//         const querySnapshot = await getDocs(collection(db, "Categories"));
//         const existingIds = querySnapshot.docs
//             .map(doc => doc.data().category_id)
//             .filter(id => id && id.startsWith("CAT-"))
//             .map(id => parseInt(id.slice(4), 10))
//             .filter(num => !isNaN(num));
//         if (existingIds.length > 0) {
//             nextNum = Math.max(...existingIds) + 1;
//         }
//         const categoryId = `CAT-${String(nextNum).padStart(2, "0")}`;

//         // Save category
//         await addDoc(collection(db, "Categories"), {
//             category_id: categoryId,
//             name: name,
//             color: color,
//             buildings: 0,
//             is_deleted: false,
//             createdAt: new Date()
//         });

//         // Save Activity Log
//         await addDoc(collection(db, "ActivityLogs"), {
//             timestamp: new Date(),
//             activity: "Added Category",
//             item: `Category ${categoryId}`,
//             description: `Added category "${name}" with color "${color}".`
//         });

//         // ✅ Update StaticDataVersions/GlobalInfo
//         const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
//         await updateDoc(staticDataRef, {
//             categories_updated: true,
//         });

//         document.getElementById('categoryForm').reset();
//         hideCategoryModal();
//         renderCategoriesTable();
//         populateCategoryDropdownForInfra();

//         alert("Category saved!");
//     } catch (err) {
//         console.error("Error adding category:", err);
//         alert("Error adding category: " + err);
//     } finally {
//         // 🔄 Restore original button
//         submitBtn.innerHTML = originalBtnHTML;
//         submitBtn.disabled = false;
//     }
// });










// ----------- Add Category Handler with Loading Spinner + Saving Text -----------
document.getElementById('categoryForm')?.addEventListener('submit', async (e) => {
  e.preventDefault();

  const submitBtn = e.target.querySelector(".save-btn");
  if (!submitBtn) return;

  const originalBtnHTML = submitBtn.innerHTML;

  // 🟢 Show spinner + "Saving..." text + disable button
  submitBtn.innerHTML = `
      <div class="spinner"></div>
      <span class="loading-text">Saving...</span>
  `;
  submitBtn.disabled = true;

  const name = document.getElementById('categoryName').value.trim();
  const legend = document.getElementById('categoryLegend').value; // ← get legend letter

  if (!name || !legend) {
    showModal('error', 'Please fill in all required fields.');
    submitBtn.innerHTML = originalBtnHTML;
    submitBtn.disabled = false;
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

    // Save new category with legend
    await addDoc(collection(db, "Categories"), {
      category_id: categoryId,
      name: name,
      legend: legend, // ← store letter legend
      buildings: 0,
      is_deleted: false,
      createdAt: new Date()
    });

    // Save Activity Log
    await addDoc(collection(db, "ActivityLogs"), {
      timestamp: new Date(),
      activity: "Added Category",
      item: `Category ${categoryId}`,
      description: `Added category "${name}" with legend "${legend}".`
    });

    // ✅ Update StaticDataVersions/GlobalInfo
    const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
    await updateDoc(staticDataRef, {
      categories_updated: true,
    });

    document.getElementById('categoryForm').reset();
    hideCategoryModal();
    renderCategoriesTable();
    populateCategoryDropdownForInfra();

    showModal('success', 'Category has been saved successfully!');
  } catch (err) {
    console.error("Error adding category:", err);
    showModal('error', 'Failed to save category. Please try again.');
  } finally {
    // 🔄 Restore original button
    submitBtn.innerHTML = originalBtnHTML;
    submitBtn.disabled = false;
  }
});


// ----------- Populate Legend Dropdown (A–Z) -----------
async function populateLegendDropdown() {
  const legendSelect = document.getElementById("categoryLegend");
  legendSelect.innerHTML = "<option value=''>Select a letter</option>";

  try {
    const snapshot = await getDocs(collection(db, "Categories"));
    const usedLetters = snapshot.docs
      .map(doc => doc.data().legend)
      .filter(l => !!l);

    const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".split("");
    alphabet.forEach(letter => {
      const option = document.createElement("option");
      option.value = letter;
      option.textContent = letter;

      if (usedLetters.includes(letter)) {
        option.disabled = true; // Disable if already used
        option.textContent = `${letter} (used)`;
      }

      legendSelect.appendChild(option);
    });
  } catch (error) {
    console.error("Error populating legends:", error);
  }
}

// Call the function when the modal or form loads
document.addEventListener("DOMContentLoaded", populateLegendDropdown);



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

// ---------- Upload & Open Edit Modal ----------
const uploadInput = document.getElementById("uploadImage");
const uploadBox = document.querySelector(".upload-box");

const editImageModal = document.getElementById("editImageModal");
const editCanvas = document.getElementById("editCanvas");
const ctx = editCanvas.getContext("2d");

const undoBtn = document.getElementById("undoBtn");
const redoBtn = document.getElementById("redoBtn");
const saveEditedImageBtn = document.getElementById("saveEditedImageBtn");
const cancelEditImageBtn = document.getElementById("cancelEditImageBtn");
const blurRange = document.getElementById("blurRange");
const blurValue = document.getElementById("blurValue");

let originalImage = new Image();
let manualRects = [];
let appliedBlurs = [];
let undoStack = [];
let redoStack = [];
let drawing = false;
let startX = 0, startY = 0;

const MAX_DIMENSION = 1024; // resize image to max 1024px width/height to stay under Firestore limit

// ---------- Open image ----------
function openEditImageModal(file) {
    const url = URL.createObjectURL(file);
    originalImage = new Image();
    originalImage.onload = () => {
        const scale = Math.min(1, MAX_DIMENSION / Math.max(originalImage.naturalWidth, originalImage.naturalHeight));
        editCanvas.width = Math.round(originalImage.naturalWidth * scale);
        editCanvas.height = Math.round(originalImage.naturalHeight * scale);
        ctx.drawImage(originalImage, 0, 0, editCanvas.width, editCanvas.height);
        manualRects = [];
        appliedBlurs = [];
        undoStack = [];
        redoStack = [];
        saveState();
        editImageModal.style.display = "flex";
    };
    originalImage.src = url;
}

uploadInput.addEventListener("change", () => {
    const file = uploadInput.files[0];
    if (!file) return;
    openEditImageModal(file);
});

// ---------- State management ----------
function saveState() {
    undoStack.push(ctx.getImageData(0, 0, editCanvas.width, editCanvas.height));
    redoStack = [];
}
function restoreState(state) { ctx.putImageData(state, 0, 0); }

// ---------- Draw overlay ----------
function drawOverlay() {
    clearCanvas();
    ctx.drawImage(originalImage, 0, 0, editCanvas.width, editCanvas.height);
    appliedBlurs.forEach(r => blurRegion(r.x, r.y, r.w, r.h, r.blur));
    ctx.save();
    ctx.lineWidth = Math.max(2, Math.round(editCanvas.width / 400));
    ctx.strokeStyle = 'rgba(255,0,0,0.8)';
    manualRects.forEach(r => ctx.strokeRect(r.x, r.y, r.w, r.h));
    ctx.restore();
}

// ---------- Apply blur ----------
function blurRegion(x, y, w, h, blurPx) {
    const temp = document.createElement('canvas');
    temp.width = w; temp.height = h;
    const tctx = temp.getContext('2d');
    tctx.drawImage(editCanvas, x, y, w, h, 0, 0, w, h);
    tctx.filter = `blur(${blurPx}px)`;
    tctx.drawImage(temp, 0, 0);
    ctx.drawImage(temp, 0, 0, w, h, x, y, w, h);
}

// ---------- Mouse Events ----------
editCanvas.addEventListener('mousedown', e => {
    drawing = true;
    const rect = editCanvas.getBoundingClientRect();
    startX = Math.round((e.clientX - rect.left) * (editCanvas.width / rect.width));
    startY = Math.round((e.clientY - rect.top) * (editCanvas.height / rect.height));
});
editCanvas.addEventListener('mousemove', e => {
    if (!drawing) return;
    const rect = editCanvas.getBoundingClientRect();
    const currX = Math.round((e.clientX - rect.left) * (editCanvas.width / rect.width));
    const currY = Math.round((e.clientY - rect.top) * (editCanvas.height / rect.height));
    const x = Math.min(startX, currX);
    const y = Math.min(startY, currY);
    const w = Math.abs(currX - startX);
    const h = Math.abs(currY - startY);
    manualRects = [{ x, y, w, h }];
    drawOverlay();
});
window.addEventListener('mouseup', () => {
    if (!drawing) return;
    drawing = false;
    if (manualRects.length) {
        const r = manualRects[0];
        const blurPx = parseInt(blurRange.value, 10);
        appliedBlurs.push({ ...r, blur: blurPx });
        manualRects = [];
        saveState();
        drawOverlay();
    }
});

// ---------- Undo / Redo ----------
undoBtn.addEventListener('click', () => {
    if (undoStack.length > 1) {
        redoStack.push(undoStack.pop());
        restoreState(undoStack[undoStack.length - 1]);
        appliedBlurs.pop();
    }
});
redoBtn.addEventListener('click', () => {
    while (redoStack.length > 0) {
        const redoData = redoStack.pop();
        restoreState(redoData);
    }
    appliedBlurs = [];
});

// ---------- Save ----------
saveEditedImageBtn.addEventListener('click', () => {
    appliedBlurs.forEach(r => blurRegion(r.x, r.y, r.w, r.h, r.blur));
    manualRects = [];
    drawOverlay();

    // Resize before saving to reduce Base64 size
    const tempCanvas = document.createElement('canvas');
    const MAX_SAVE_DIM = 1024;
    let scale = Math.min(1, MAX_SAVE_DIM / Math.max(editCanvas.width, editCanvas.height));
    tempCanvas.width = Math.round(editCanvas.width * scale);
    tempCanvas.height = Math.round(editCanvas.height * scale);
    const tctx = tempCanvas.getContext('2d');
    tctx.drawImage(editCanvas, 0, 0, tempCanvas.width, tempCanvas.height);

    const base64 = tempCanvas.toDataURL('image/jpeg', 0.8); // compress 80%
    let existingImg = uploadBox.querySelector('img');
    if (!existingImg) {
        existingImg = document.createElement('img');
        uploadBox.appendChild(existingImg);
    }
    existingImg.src = base64;
    existingImg.style.width = '100%';
    existingImg.style.height = '100%';
    existingImg.style.objectFit = 'cover';
    existingImg.style.position = 'absolute';
    existingImg.style.top = '0';
    existingImg.style.left = '0';
    uploadBox.querySelector('.upload-label').style.display = 'none';
    editImageModal.style.display = 'none';
});

// ---------- Cancel ----------
cancelEditImageBtn.addEventListener('click', () => { editImageModal.style.display = 'none'; });

// ---------- Blur range display ----------
blurRange.addEventListener('input', () => { blurValue.textContent = blurRange.value + 'px'; });

// ---------- Helpers ----------
function clearCanvas() { ctx.clearRect(0, 0, editCanvas.width, editCanvas.height); }
function dataURLtoFile(dataurl, filename) {
    const arr = dataurl.split(','), mime = arr[0].match(/:(.*?);/)[1];
    const bstr = atob(arr[1]); let n = bstr.length; const u8arr = new Uint8Array(n);
    while (n--) { u8arr[n] = bstr.charCodeAt(n); }
    return new File([u8arr], filename, { type: mime });
}

// ---------- Keyboard shortcuts ----------
document.addEventListener('keydown', (e) => {
    if (e.ctrlKey && e.key === 'z') undoBtn.click();
    if (e.ctrlKey && e.key === 'y') redoBtn.click();
});

document.querySelector("#addInfraModal form")?.addEventListener("submit", async (e) => {
    e.preventDefault();

    const submitBtn = e.target.querySelector(".save-btn");
    if (!submitBtn) return;

    // 🌀 Save original button content
    const originalBtnHTML = submitBtn.innerHTML;

    // 🟢 Show spinner + "Saving..." text + disable button
    submitBtn.innerHTML = `
        <div class="spinner"></div>
        <span class="loading-text">Saving...</span>
    `;
    submitBtn.disabled = true;

    try {
        const name = document.querySelector('#addInfraModal input[placeholder="e.g. Main Library"]').value.trim();
        const infraId = document.getElementById("infraId").value.trim();
        const categorySelect = document.querySelector('#addInfraModal select');
        const categoryId = categorySelect.value;
        const categoryName = categorySelect.selectedOptions[0]?.dataset.name || categoryId;

        const phone = "09123456789";
        const email = "sample@gmail.com";

        const existingImg = uploadBox.querySelector("img");
        let imageUrl = existingImg?.src || "";

        if (!name || !infraId || !categoryId) {
            showModal('error', 'Please fill in all required fields.');
            return;
        }

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

        await addDoc(collection(db, "ActivityLogs"), {
            timestamp: new Date(),
            activity: "Added Infrastructure",
            item: `Infrastructure ${infraId}`,
            description: `Added infrastructure "${name}" under category "${categoryName}".`
        });

        e.target.reset();
        hideInfraModal();
        renderInfraTable();

        const label = uploadBox.querySelector(".upload-label");
        label.style.display = "flex";
        const existingImg2 = uploadBox.querySelector("img");
        if (existingImg2) existingImg2.remove();

        showModal('success', 'Infrastructure has been saved successfully!');

    } catch (err) {
        console.error("Error adding infrastructure:", err);
        showModal('error', 'Failed to save infrastructure. Please try again.');
    } finally {
        // 🔄 Restore original button
        submitBtn.innerHTML = originalBtnHTML;
        submitBtn.disabled = false;
    }
});





















let infraTableData = []; // Store loaded infra for filtering

// Update renderInfraTable to store data for filtering
async function renderInfraTable() {
    const tbody = document.querySelector(".infra-table tbody");
    if (!tbody) return;
    tbody.innerHTML = `
        <tr class="table-loader-row">
            <td colspan="6" class="table-loader">
                <div class="spinner"></div>
            </td>
        </tr>
    `;

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
        tbody.innerHTML = `
            <tr><td colspan="6" style="text-align:center; color:#DC143C;">⚠️ Failed to load data</td></tr>
        `;
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



document.querySelector("#addRoomModal form")?.addEventListener("submit", async (e) => {
    e.preventDefault();

    const submitBtn = e.target.querySelector(".save-btn");
    if (!submitBtn) return;

    const originalBtnHTML = submitBtn.innerHTML;

    // 🟢 Show spinner + "Saving..." text + disable button
    submitBtn.innerHTML = `
        <div class="spinner"></div>
        <span class="loading-text">Saving...</span>
    `;
    submitBtn.disabled = true;

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
        showModal('error', 'Please fill in all required fields.');
        submitBtn.innerHTML = originalBtnHTML;
        submitBtn.disabled = false;
        return;
    }

    try {
        // Save into IndoorInfrastructure collection
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

        // ✅ Clear form fields
        e.target.reset();
        document.querySelector('#addRoomModal input[name="room_id"]').value = "";

        // ✅ Hide modal + refresh table
        hideRoomModal();
        renderRoomsTable();

        showModal('success', 'Room has been saved successfully!');

    } catch (err) {
        console.error("Error adding infrastructure:", err);
        showModal('error', 'Failed to save room. Please try again.');
    } finally {
        // 🔄 Restore button
        submitBtn.innerHTML = originalBtnHTML;
        submitBtn.disabled = false;
    }
});





let roomsTableData = [];

// ----------- Load Indoor Infrastructure Table (ignore deleted) -----------
async function renderRoomsTable() {
  const tbody = document.querySelector(".rooms-table tbody");
  if (!tbody) return;

  // 🌀 STEP 1: Show loader before anything else
  tbody.innerHTML = `
    <tr>
      <td colspan="5" class="table-loader">
        <div class="spinner"></div>
      </td>
    </tr>
  `;

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
      const infraKey =
        room.infra_id?.trim() || room.infrastructure_id?.trim() || "";
      return {
        ...room,
        infraName: infraMap[infraKey] || `⚠️ Missing infra for ${infraKey}`,
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

    // STEP 2: Render table rows after data loads
    renderRoomsTableRows(roomsTableData);
  } catch (err) {
    console.error("❌ Error loading Indoor Infrastructure: ", err);
    tbody.innerHTML = `
      <tr>
        <td colspan="5" style="text-align:center; color:crimson;">
          ❌ Failed to load data
        </td>
      </tr>
    `;
  }
}

function renderRoomsTableRows(data) {
  const tbody = document.querySelector(".rooms-table tbody");
  if (!tbody) return;
  tbody.innerHTML = "";

  if (!data.length) {
    tbody.innerHTML = `
      <tr>
        <td colspan="5" style="text-align:center; color:#999;">
          No rooms found.
        </td>
      </tr>
    `;
    return;
  }

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







// ----------- Edit Room Modal Open Handler with Loading Icon -----------
document.querySelector(".rooms-table").addEventListener("click", async (e) => {
    const button = e.target.closest("button.edit");
    const icon = e.target.closest("i.fa-edit");

    if (!button && !icon) return;

    const row = e.target.closest("tr");
    if (!row) return;

    const roomId = row.querySelector("td")?.textContent?.trim();
    if (!roomId) return;

    // 🌀 Replace icon with spinner
    const originalIconHTML = icon?.outerHTML || button?.innerHTML;
    if (icon) {
        icon.outerHTML = `<div class="spinner"></div>`;
    } else if (button) {
        button.innerHTML = `<div class="spinner"></div>`;
    }

    try {
        // Fetch room data from IndoorInfrastructure
        const roomQ = query(collection(db, "IndoorInfrastructure"), where("room_id", "==", roomId));
        const snap = await getDocs(roomQ);

        if (snap.empty) {
            showModal('error', 'Indoor Infrastructure not found. Please try again.');
            return;
        }

        const docSnap = snap.docs[0];
        const roomData = docSnap.data();

        // Populate dropdowns and set values
        await populateEditRoomInfraDropdown(roomData.infra_id);
        document.getElementById("editRoomInfra").value = roomData.infra_id ?? "";
        document.getElementById("editRoomType").value = roomData.indoor_type ?? "";
        document.getElementById("editRoomId").value = roomData.room_id ?? "";
        document.getElementById("editRoomName").value = roomData.name ?? "";
        document.getElementById("editRoomForm").dataset.docId = docSnap.id;

        // Show modal
        document.getElementById("editRoomModal").style.display = "flex";
    } catch (err) {
        console.error("Error opening edit room modal:", err);
        showModal('error', 'Failed to load room data.');
    } finally {
        // 🔄 Restore original icon
        if (icon) icon.outerHTML = originalIconHTML;
        if (button) button.innerHTML = originalIconHTML;
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








// ----------- Save Edited Room with Loading Button -----------
document.getElementById("editRoomForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    const form = e.target;
    const saveBtn = form.querySelector(".save-btn");

    const docId = form.dataset.docId;
    if (!docId) {
        showModal('error', 'No document ID found for update.');
        return;
    }

    const name = document.getElementById("editRoomName").value.trim();
    const roomId = document.getElementById("editRoomId").value.trim();
    const infraId = document.getElementById("editRoomInfra").value;
    const indoorType = document.getElementById("editRoomType").value;

    if (!name || !roomId || !infraId || !indoorType) {
        showModal('error', 'Please fill in all required fields.');
        return;
    }

    // 🌀 Show loading on the button
    const originalBtnHTML = saveBtn.innerHTML;
    saveBtn.innerHTML = `<div class="spinner"></div> Saving...`;
    saveBtn.disabled = true;
    saveBtn.style.opacity = 0.7;
    saveBtn.style.cursor = "not-allowed";

    try {
        await updateDoc(doc(db, "IndoorInfrastructure", docId), {
            name: name,
            room_id: roomId,
            infra_id: infraId,
            indoor_type: indoorType,
            updatedAt: new Date()
        });

        form.reset();
        document.getElementById("editRoomModal").style.display = "none";
        renderRoomsTable();
        showModal('success', 'Room has been updated successfully!');
    } catch (err) {
        showModal('error', 'Failed to update room. Please try again.');
        console.error(err);
    } finally {
        // 🔄 Restore button
        saveBtn.innerHTML = originalBtnHTML;
        saveBtn.disabled = false;
        saveBtn.style.opacity = 1;
        saveBtn.style.cursor = "pointer";
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













// ----------- Add Map Handler with Loading Spinner + Saving Text -----------
document.querySelector("#addMapModal form")?.addEventListener("submit", async (e) => {
    e.preventDefault();

    const submitBtn = e.target.querySelector(".save-btn");
    if (!submitBtn) return;

    const originalBtnHTML = submitBtn.innerHTML;

    // 🟢 Show spinner + "Saving..." text + disable button
    submitBtn.innerHTML = `
        <div class="spinner"></div>
        <span class="loading-text">Saving...</span>
    `;
    submitBtn.disabled = true;

    const mapName = document.getElementById("mapName").value.trim();
    const campusDropdown = document.getElementById("campusDropdown");
    const campusIncluded = campusDropdown?.getSelectedValues() || [];

    if (!mapName) {
        
        showModal('error', 'Please fill in all required fields.');
        submitBtn.innerHTML = originalBtnHTML;
        submitBtn.disabled = false;
        return;
    }

    try {
        // Generate next document ID
        const mapsSnap = await getDocs(collection(db, "MapVersions"));
        const existingDocNumbers = mapsSnap.docs
            .map(doc => doc.id)
            .filter(id => id.startsWith("MAP-"))
            .map(id => parseInt(id.slice(4), 10))
            .filter(num => !isNaN(num));
        const nextNum = existingDocNumbers.length > 0 ? Math.max(...existingDocNumbers) + 1 : 1;
        const newDocId = `MAP-${nextNum.toString().padStart(2, "0")}`;

        // Create the new document
        const mapRef = doc(db, "MapVersions", newDocId);
        await setDoc(mapRef, {
            map_id: newDocId,
            map_name: mapName,
            campus_included: campusIncluded,
            createdAt: new Date(),
            current_version: "v1.0.0"
        });

        // Initial version
        await setDoc(doc(db, "MapVersions", newDocId, "versions", "v1.0.0"), {
            nodes: [],
            edges: []
        });

        // Add simplified record
        await addDoc(collection(db, "Maps"), {
            map_id: newDocId,
            map_name: mapName,
            campus_included: campusIncluded,
            createdAt: new Date()
        });

        // Activity Log
        await addDoc(collection(db, "ActivityLogs"), {
            timestamp: new Date(),
            activity: "Added Map",
            item: `Map ${newDocId}`,
            description: `Created map "${mapName}" with version v1.0.0 and campuses: ${campusIncluded.join(", ") || "none"}.`
        });

        e.target.reset();

        // Reset dropdown UI
        document.getElementById("selectedCampuses").textContent = "Select campuses...";
        campusDropdown?.querySelectorAll(".option").forEach(opt => opt.classList.remove("selected"));

        hideMapModal();
        renderMapsTable();
        showModal('success', 'Map has been saved successfully!');
    } catch (err) {
        console.error("Error creating map:", err);
        showModal('error', 'Failed to save Map. Please try again.');
    } finally {
        // 🔄 Restore button
        submitBtn.innerHTML = originalBtnHTML;
        submitBtn.disabled = false;
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

  // 🌀 Show table loader before fetching
  tbody.innerHTML = `
    <tr>
      <td colspan="5" class="table-loader">
        <div class="spinner"></div>
      </td>
    </tr>
  `;

  try {
    let maps = [];
    let campuses = [];

    if (navigator.onLine) {
      const mapsSnap = await getDocs(collection(db, "MapVersions"));
      maps = mapsSnap.docs
        .map(doc => ({ id: doc.id, ...doc.data() }))
        .filter(m => !m.is_deleted);

      const campusSnap = await getDocs(collection(db, "Campus"));
      campuses = campusSnap.docs.map(doc => ({ id: doc.id, ...doc.data() }));
    } else {
      const [mapsRes, campusRes] = await Promise.all([
        fetch("../assets/firestore/MapVersions.json"),
        fetch("../assets/firestore/Campus.json"),
      ]);
      maps = (await mapsRes.json()).filter(m => !m.is_deleted);
      campuses = await campusRes.json();
    }

    // ===== Sort maps by createdAt =====
    maps.sort((a, b) => {
      const tA = a.createdAt?.seconds
        ? a.createdAt.seconds * 1000
        : a.createdAt?.toMillis?.() || 0;
      const tB = b.createdAt?.seconds
        ? b.createdAt.seconds * 1000
        : b.createdAt?.toMillis?.() || 0;
      return tA - tB;
    });

    // ===== Build campus lookup =====
    const campusMap = {};
    campuses.forEach(c => (campusMap[c.campus_id || c.id] = c.campus_name));

    // ===== Store for search/filter =====
    mapsTableData = maps.map(data => ({
      ...data,
      campusNames:
        Array.isArray(data.campus_included) && data.campus_included.length > 0
          ? data.campus_included.map(id => campusMap[id] || id).join(", ")
          : "—",
    }));

    renderMapsTableRows(mapsTableData);
    setupMapDeleteHandlers();
  } catch (err) {
    console.error("❌ Error loading maps:", err);
    tbody.innerHTML = `
      <tr>
        <td colspan="5" style="text-align:center; color:#DC143C;">
          ⚠️ Failed to load map data
        </td>
      </tr>
    `;
  }
}

// ----------- Render Rows -----------
function renderMapsTableRows(data) {
  const tbody = document.querySelector(".maps-table tbody");
  if (!tbody) return;
  tbody.innerHTML = "";

  if (!data.length) {
    tbody.innerHTML = `
      <tr>
        <td colspan="5" style="text-align:center; color:#999;">
          No maps found.
        </td>
      </tr>
    `;
    return;
  }

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

// Call on page load
document.addEventListener("DOMContentLoaded", renderMapsTable);




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

// ----------- Add Campus Handler with Loading Spinner + Saving Text -----------
document.querySelector("#addCampusModal form")?.addEventListener("submit", async (e) => {
    e.preventDefault();

    const submitBtn = e.target.querySelector(".save-btn");
    if (!submitBtn) return;

    const originalBtnHTML = submitBtn.innerHTML;

    // 🟢 Show spinner + "Saving..." text + disable button
    submitBtn.innerHTML = `
        <div class="spinner"></div>
        <span class="loading-text">Saving...</span>
    `;
    submitBtn.disabled = true;

    const campusId = document.getElementById("campusId").value.trim();
    const campusName = document.getElementById("campusName").value.trim();
    const mapSelect = document.getElementById("mapSelect");
    const mapId = mapSelect.value;
    const mapName = mapSelect.options[mapSelect.selectedIndex]?.dataset.mapName || "";

    if (!campusId || !campusName || !mapId) {
        showModal('error', 'Please fill in all required fields.');
        submitBtn.innerHTML = originalBtnHTML;
        submitBtn.disabled = false;
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

        // ✅ Update StaticDataVersions/GlobalInfo after saving
        const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
        await updateDoc(staticDataRef, { campus_updated: true });

        e.target.reset();
        hideCampusModal();
        renderCampusTable();
        showModal('success', 'Campus has been saved successfully!');
    } catch (err) {
        console.error("Error saving campus:", err);
        showModal('error', 'Failed to save campus. Please try again.');
    } finally {
        // 🔄 Restore button
        submitBtn.innerHTML = originalBtnHTML;
        submitBtn.disabled = false;
    }
});





let campusTableData = [];

// ----------- Load Campus Table -----------
async function renderCampusTable() {
  const tbody = document.querySelector(".campus-table tbody");
  if (!tbody) return;

  // 🌀 Show table loader before fetching
  tbody.innerHTML = `
    <tr>
      <td colspan="4" class="table-loader">
        <div class="spinner"></div>
      </td>
    </tr>
  `;

  try {
    let campuses = [];
    let maps = [];

    if (navigator.onLine) {
      const campusSnap = await getDocs(collection(db, "Campus"));
      campuses = campusSnap.docs
        .map(doc => ({ id: doc.id, ...doc.data() }))
        .filter(c => !c.is_deleted);

      const mapsSnap = await getDocs(collection(db, "MapVersions"));
      maps = mapsSnap.docs
        .map(doc => ({ id: doc.id, ...doc.data() }))
        .filter(m => !m.is_deleted);
    } else {
      const [campusRes, mapsRes] = await Promise.all([
        fetch("../assets/firestore/Campus.json"),
        fetch("../assets/firestore/MapVersions.json"),
      ]);
      campuses = (await campusRes.json()).filter(c => !c.is_deleted);
      maps = (await mapsRes.json()).filter(m => !m.is_deleted);
    }

    // ===== Sort campuses by createdAt =====
    campuses.sort((a, b) => {
      const tA = a.createdAt?.seconds
        ? a.createdAt.seconds * 1000
        : a.createdAt?.toMillis?.() || 0;
      const tB = b.createdAt?.seconds
        ? b.createdAt.seconds * 1000
        : b.createdAt?.toMillis?.() || 0;
      return tA - tB;
    });

    // ===== Build map lookup =====
    const mapMap = {};
    maps.forEach(m => (mapMap[m.map_id || m.id] = m.map_name));

    // ===== Store for search/filter =====
    campusTableData = campuses.map(data => ({
      ...data,
      mapName: mapMap[data.map_id] || data.map_id || "—",
    }));

    renderCampusTableRows(campusTableData);
    setupCampusDeleteHandlers();
  } catch (err) {
    console.error("❌ Error loading campuses:", err);
    tbody.innerHTML = `
      <tr>
        <td colspan="4" style="text-align:center; color:#DC143C;">
          ⚠️ Failed to load campus data
        </td>
      </tr>
    `;
  }
}

// ----------- Render Rows -----------
function renderCampusTableRows(data) {
  const tbody = document.querySelector(".campus-table tbody");
  if (!tbody) return;
  tbody.innerHTML = "";

  if (!data.length) {
    tbody.innerHTML = `
      <tr>
        <td colspan="4" style="text-align:center; color:#999;">
          No campuses found.
        </td>
      </tr>
    `;
    return;
  }

  data.forEach(data => {
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>${data.campus_id || "—"}</td>
      <td>${data.campus_name || "—"}</td>
      <td>${data.mapName}</td>
      <td class="actions">
        <button class="edit"><i class="fas fa-edit"></i></button>
        <button class="delete" data-id="${data.id}"><i class="fas fa-trash"></i></button>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

// Call on page load
document.addEventListener("DOMContentLoaded", renderCampusTable);







// ======================= EDIT CATEGORY SECTION =========================

// ----------- Open Edit Category Modal on Table Click (with Loading Icon) -----------
document.querySelector("#categoriesTableBody").addEventListener("click", async (e) => {
    const button = e.target.closest("button.edit");
    const icon = e.target.closest("i.fa-edit");

    if (!button && !icon) return;

    const row = e.target.closest("tr");
    if (!row) return;

    const docId = row.dataset.id; // ✅ direct from row
    if (!docId) return;

    // 🌀 Replace icon with spinner
    const originalIconHTML = icon?.outerHTML || button?.innerHTML;
    if (icon) {
        icon.outerHTML = `<div class="spinner"></div>`;
    } else if (button) {
        button.innerHTML = `<div class="spinner"></div>`;
    }

    try {
        const docRef = doc(db, "Categories", docId);
        const docSnap = await getDoc(docRef);

        if (!docSnap.exists()) return;
        const data = docSnap.data();

        // Prefill form fields
        document.getElementById("editCategoryName").value = data.name ?? "";
        document.getElementById("editCategoryColor").value = data.color ?? "#000000";

        // Store docId in form for update
        document.getElementById("editCategoryForm").dataset.docId = docId;

        // Show modal
        document.getElementById("editCategoryModal").style.display = "flex";
    } catch (err) {
        console.error("Error opening edit category modal:", err);
    } finally {
        // 🔄 Restore original icon
        if (icon) icon.outerHTML = originalIconHTML;
        if (button) button.innerHTML = originalIconHTML;
    }
});



// ----------- Save Edited Category with Loading Button -----------
document.getElementById("editCategoryForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    const form = e.target;
    const saveBtn = form.querySelector(".save-btn");

    const docId = form.dataset.docId;
    if (!docId) {
        showModal('error', 'No document ID found for update.');
        return;
    }

    const name = document.getElementById("editCategoryName").value.trim();
    const color = document.getElementById("editCategoryColor").value;

    if (!name || !color) {
        showModal('error', 'Please fill in all required fields.');
        return;
    }

    // 🌀 Show loading spinner and "Saving..." text
    const originalBtnHTML = saveBtn.innerHTML;
    saveBtn.innerHTML = `<div class="spinner"></div><span class="saving-text">Saving...</span>`;
    saveBtn.disabled = true;
    saveBtn.style.opacity = 0.7;
    saveBtn.style.cursor = "not-allowed";

    try {
        await updateDoc(doc(db, "Categories", docId), {
            name: name,
            color: color,
            updatedAt: new Date()
        });


        form.reset();
        document.getElementById("editCategoryModal").style.display = "none";
        renderCategoriesTable();
        populateCategoryDropdownForInfra();

        showModal('success', 'Category has been updated successfully!');
    } catch (err) {
        showModal('error', 'Failed to update category. Please try again.');
        console.error(err);
    } finally {
        // 🔄 Restore button
        saveBtn.innerHTML = originalBtnHTML;
        saveBtn.disabled = false;
        saveBtn.style.opacity = 1;
        saveBtn.style.cursor = "pointer";
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

// ----------- Open Edit Map Modal (with Loading Icon) -----------
document.querySelector(".maps-table tbody").addEventListener("click", async (e) => {
    const button = e.target.closest("button.edit");
    const icon = e.target.closest("i.fa-edit");

    if (!button && !icon) return;

    const row = e.target.closest("tr");
    if (!row) return;

    const mapId = button?.dataset.id || row.dataset.id;
    if (!mapId) return;

    // 🌀 Replace icon with spinner
    const originalIconHTML = icon?.outerHTML || button?.innerHTML;
    if (icon) {
        icon.outerHTML = `<div class="spinner"></div>`;
    } else if (button) {
        button.innerHTML = `<div class="spinner"></div>`;
    }

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
    } finally {
        // 🔄 Restore original icon
        if (icon) icon.outerHTML = originalIconHTML;
        if (button) button.innerHTML = originalIconHTML;
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


// ----------- Save Edited Map with Loading Button -----------
document.getElementById("editMapForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    const form = e.target;
    const saveBtn = form.querySelector(".save-btn");

    const docId = form.dataset.docId;
    if (!docId) return showModal('error', 'No Map ID found for update.');

    const mapName = document.getElementById("editMapName").value.trim();
    const campusDropdown = document.getElementById("editCampusDropdown");
    const campusIncluded = campusDropdown.getSelectedValues() || [];

    if (!mapName) return showModal('error', 'Please enter a map name.');

    // 🌀 Show loading spinner and "Saving..." text
    const originalBtnHTML = saveBtn.innerHTML;
    saveBtn.innerHTML = `<div class="spinner"></div><span class="saving-text">Saving...</span>`;
    saveBtn.disabled = true;
    saveBtn.style.opacity = 0.7;
    saveBtn.style.cursor = "not-allowed";

    try {
        await updateDoc(doc(db, "MapVersions", docId), {
            map_name: mapName,
            campus_included: campusIncluded,
            updatedAt: new Date()
        });

        showModal('success', 'Map has been updated successfully!');
        form.reset();
        document.getElementById("editMapModal").style.display = "none";
        renderMapsTable();
    } catch (err) {
        showModal('error', 'Failed to update map. Please try again.');
        console.error(err);
    } finally {
        // 🔄 Restore button
        saveBtn.innerHTML = originalBtnHTML;
        saveBtn.disabled = false;
        saveBtn.style.opacity = 1;
        saveBtn.style.cursor = "pointer";
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




// ----------- Open Edit Campus Modal (with Loading Icon) -----------
document.querySelector(".campus-table tbody").addEventListener("click", async (e) => {
    const button = e.target.closest("button.edit");
    const icon = e.target.closest("i.fa-edit");

    if (!button && !icon) return;

    const row = e.target.closest("tr");
    if (!row) return;

    const campusId = row.querySelector("td")?.textContent?.trim();
    if (!campusId) return;

    // 🌀 Replace icon with spinner
    const originalIconHTML = icon?.outerHTML || button?.innerHTML;
    if (icon) {
        icon.outerHTML = `<div class="spinner"></div>`;
    } else if (button) {
        button.innerHTML = `<div class="spinner"></div>`;
    }

    try {
        const q = query(collection(db, "Campus"), where("campus_id", "==", campusId));
        const snap = await getDocs(q);
        if (snap.empty) return showModal('error', 'Campus not found.');

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
        showModal('error', 'PError opening edit campus modal.');
    } finally {
        // 🔄 Restore original icon
        if (icon) icon.outerHTML = originalIconHTML;
        if (button) button.innerHTML = originalIconHTML;
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

// ----------- Save Edited Campus with Loading Button -----------
document.getElementById("editCampusForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    const form = e.target;
    const saveBtn = form.querySelector(".save-btn");

    const docId = form.dataset.docId;
    if (!docId) return showModal('error', 'No document ID found for update.');

    const campusName = document.getElementById("editCampusName").value.trim();
    const mapSelect = document.getElementById("editMapSelect");
    const mapId = mapSelect.value;
    const mapName = mapSelect.options[mapSelect.selectedIndex]?.dataset.mapName || "";

    if (!campusName || !mapId) {
        showModal('error', 'Please fill in all required fields.');
        return;
    }

    // 🌀 Show loading spinner and "Saving..." text
    const originalBtnHTML = saveBtn.innerHTML;
    saveBtn.innerHTML = `<div class="spinner"></div><span class="saving-text">Saving...</span>`;
    saveBtn.disabled = true;
    saveBtn.style.opacity = 0.7;
    saveBtn.style.cursor = "not-allowed";

    try {
        await updateDoc(doc(db, "Campus", docId), {
            campus_name: campusName,
            map_id: mapId,
            updatedAt: new Date()
        });

        // Log activity
        await addDoc(collection(db, "ActivityLogs"), {
            timestamp: new Date(),
            activity: "Edited Campus",
            item: `Campus ${docId}`,
            description: `Updated campus "${campusName}" under map "${mapName}".`
        });

        showModal('success', 'Campus has been updated successfully!');
        form.reset();
        document.getElementById("editCampusModal").style.display = "none";
        renderCampusTable();
    } catch (err) {
        showModal('error', 'Failed to update campus. Please try again.');
        console.error(err);
    } finally {
        // 🔄 Restore button
        saveBtn.innerHTML = originalBtnHTML;
        saveBtn.disabled = false;
        saveBtn.style.opacity = 1;
        saveBtn.style.cursor = "pointer";
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















async function convertFileToBase64(file, maxWidth = 800, maxHeight = 800) {
  return new Promise((resolve) => {
    const img = new Image();
    const reader = new FileReader();

    reader.onload = (e) => {
      img.onload = () => {
        const canvas = document.createElement("canvas");
        let width = img.width;
        let height = img.height;

        if (width > maxWidth || height > maxHeight) {
          if (width / height > maxWidth / maxHeight) {
            height = Math.round((height * maxWidth) / width);
            width = maxWidth;
          } else {
            width = Math.round((width * maxHeight) / height);
            height = maxHeight;
          }
        }

        canvas.width = width;
        canvas.height = height;
        const ctx = canvas.getContext("2d");
        ctx.drawImage(img, 0, 0, width, height);
        resolve(canvas.toDataURL("image/jpeg", 0.8));
      };
      img.src = e.target.result;
    };

    reader.readAsDataURL(file);
  });
}



// ======================= EDIT INFRASTRUCTURE SECTION =========================

// ---------------- Edit Infra Image Upload + Blur Editor ----------------

// Get elements
const editUploadInput = document.getElementById("editInfraImage");
const editUploadBox = document.getElementById("editUploadBox");
const editPreview = document.getElementById("editInfraPreview");

// ====== Blur Editor Elements ======
const editInfraBlurModal = document.getElementById("editInfraImageModal");
const editInfraBlurCanvas = document.getElementById("editInfraCanvas");

const editInfraCtx = editInfraBlurCanvas.getContext("2d");

const editInfraUndoBtn = document.getElementById("editInfraUndoBtn");
const editInfraRedoBtn = document.getElementById("editInfraRedoBtn");
const editInfraSaveBtn = document.getElementById("editInfraSaveBtn");
const editInfraCancelBtn = document.getElementById("editInfraCancelBtn");
const editInfraBlurRange = document.getElementById("editInfraBlurRange");
const editInfraBlurValue = document.getElementById("editInfraBlurValue");

let editInfraOriginalImage = new Image();
let editInfraManualRects = [];
let editInfraAppliedBlurs = [];
let editInfraUndoStack = [];
let editInfraRedoStack = [];
let editInfraDrawing = false;
let editInfraStartX = 0,
    editInfraStartY = 0;

const EDIT_INFRA_MAX_DIM = 1024;

// ====== Open Blur Editor ======
function openEditInfraBlurModal(file) {
    const url = URL.createObjectURL(file);
    editInfraOriginalImage = new Image();
    editInfraOriginalImage.onload = () => {
        const scale = Math.min(
            1,
            EDIT_INFRA_MAX_DIM /
                Math.max(
                    editInfraOriginalImage.naturalWidth,
                    editInfraOriginalImage.naturalHeight
                )
        );
        editInfraBlurCanvas.width = Math.round(
            editInfraOriginalImage.naturalWidth * scale
        );
        editInfraBlurCanvas.height = Math.round(
            editInfraOriginalImage.naturalHeight * scale
        );
        editInfraCtx.drawImage(
            editInfraOriginalImage,
            0,
            0,
            editInfraBlurCanvas.width,
            editInfraBlurCanvas.height
        );
        editInfraManualRects = [];
        editInfraAppliedBlurs = [];
        editInfraUndoStack = [];
        editInfraRedoStack = [];
        saveEditInfraState();
        editInfraBlurModal.style.display = "flex";
    };
    editInfraOriginalImage.src = url;
}

// ====== State Management ======
function saveEditInfraState() {
    editInfraUndoStack.push(
        editInfraCtx.getImageData(
            0,
            0,
            editInfraBlurCanvas.width,
            editInfraBlurCanvas.height
        )
    );
    editInfraRedoStack = [];
}
function restoreEditInfraState(state) {
    editInfraCtx.putImageData(state, 0, 0);
}

// ====== Draw Overlay ======
function drawEditInfraOverlay() {
    clearEditInfraCanvas();
    editInfraCtx.drawImage(
        editInfraOriginalImage,
        0,
        0,
        editInfraBlurCanvas.width,
        editInfraBlurCanvas.height
    );
    editInfraAppliedBlurs.forEach((r) =>
        blurEditInfraRegion(r.x, r.y, r.w, r.h, r.blur)
    );
    editInfraCtx.save();
    editInfraCtx.lineWidth = Math.max(
        2,
        Math.round(editInfraBlurCanvas.width / 400)
    );
    editInfraCtx.strokeStyle = "rgba(255,0,0,0.8)";
    editInfraManualRects.forEach((r) =>
        editInfraCtx.strokeRect(r.x, r.y, r.w, r.h)
    );
    editInfraCtx.restore();
}

function blurEditInfraRegion(x, y, w, h, blurPx) {
  if (!editInfraBlurCanvas || !editInfraCtx) return;

  // Clamp rectangle
  x = Math.max(0, x);
  y = Math.max(0, y);
  w = Math.max(1, w);
  h = Math.max(1, h);

  // Temporary canvas just for this rectangle
  const temp = document.createElement("canvas");
  const tctx = temp.getContext("2d");

  temp.width = w;
  temp.height = h;

  // Copy region from main canvas
  tctx.clearRect(0, 0, w, h);
  tctx.drawImage(editInfraBlurCanvas, x, y, w, h, 0, 0, w, h);

  // Apply blur to that isolated region
  tctx.save();
  tctx.filter = `blur(${blurPx}px)`;
  tctx.drawImage(temp, 0, 0);
  tctx.restore();

  // Paste blurred region back to main canvas
  editInfraCtx.drawImage(temp, 0, 0, w, h, x, y, w, h);
}





// ====== Mouse Events ======
editInfraBlurCanvas.addEventListener("mousedown", (e) => {
    editInfraDrawing = true;
    const rect = editInfraBlurCanvas.getBoundingClientRect();
    editInfraStartX = Math.round(
        (e.clientX - rect.left) *
            (editInfraBlurCanvas.width / rect.width)
    );
    editInfraStartY = Math.round(
        (e.clientY - rect.top) *
            (editInfraBlurCanvas.height / rect.height)
    );
});
editInfraBlurCanvas.addEventListener("mousemove", (e) => {
    if (!editInfraDrawing) return;
    const rect = editInfraBlurCanvas.getBoundingClientRect();
    const currX = Math.round(
        (e.clientX - rect.left) *
            (editInfraBlurCanvas.width / rect.width)
    );
    const currY = Math.round(
        (e.clientY - rect.top) *
            (editInfraBlurCanvas.height / rect.height)
    );
    const x = Math.min(editInfraStartX, currX);
    const y = Math.min(editInfraStartY, currY);
    const w = Math.abs(currX - editInfraStartX);
    const h = Math.abs(currY - editInfraStartY);
    editInfraManualRects = [{ x, y, w, h }];
    drawEditInfraOverlay();
});
window.addEventListener("mouseup", () => {
    if (!editInfraDrawing) return;
    editInfraDrawing = false;
    if (editInfraManualRects.length) {
        const r = editInfraManualRects[0];
        const blurPx = parseInt(editInfraBlurRange.value, 10);
        editInfraAppliedBlurs.push({ ...r, blur: blurPx });
        editInfraManualRects = [];
        saveEditInfraState();
        drawEditInfraOverlay();
    }
});

// ====== Undo / Redo ======
editInfraUndoBtn.addEventListener("click", () => {
    if (editInfraUndoStack.length > 1) {
        editInfraRedoStack.push(editInfraUndoStack.pop());
        restoreEditInfraState(
            editInfraUndoStack[editInfraUndoStack.length - 1]
        );
        editInfraAppliedBlurs.pop();
    }
});
editInfraRedoBtn.addEventListener("click", () => {
    while (editInfraRedoStack.length > 0) {
        const redoData = editInfraRedoStack.pop();
        restoreEditInfraState(redoData);
    }
    editInfraAppliedBlurs = [];
});

editInfraSaveBtn.addEventListener("click", () => {
  // apply all blur regions directly
  editInfraAppliedBlurs.forEach((r) =>
    blurEditInfraRegion(r.x, r.y, r.w, r.h, r.blur)
  );
  editInfraManualRects = [];

  // export the final blurred result
  const tempCanvas = document.createElement("canvas");
  const MAX_SAVE_DIM = 1024;
  const scale = Math.min(
    1,
    MAX_SAVE_DIM / Math.max(editInfraBlurCanvas.width, editInfraBlurCanvas.height)
  );
  tempCanvas.width = Math.round(editInfraBlurCanvas.width * scale);
  tempCanvas.height = Math.round(editInfraBlurCanvas.height * scale);
  const tctx = tempCanvas.getContext("2d");
  tctx.drawImage(editInfraBlurCanvas, 0, 0, tempCanvas.width, tempCanvas.height);

  const base64 = tempCanvas.toDataURL("image/jpeg", 0.8);
  editPreview.src = base64;
  editPreview.style.display = "block";
  editInfraBlurModal.style.display = "none";
  const label = editUploadBox.querySelector(".upload-label");
  label.style.display = "none";
  // clear the file input so it doesn’t re-upload the original
  editUploadInput.value = "";
});


// ====== Cancel ======
editInfraCancelBtn.addEventListener("click", () => {
    editInfraBlurModal.style.display = "none";
});

// ====== Blur Range ======
editInfraBlurRange.addEventListener("input", () => {
    editInfraBlurValue.textContent = editInfraBlurRange.value + "px";
});

// ====== Helpers ======
function clearEditInfraCanvas() {
    editInfraCtx.clearRect(
        0,
        0,
        editInfraBlurCanvas.width,
        editInfraBlurCanvas.height
    );
}

// ====== Trigger Blur Modal After Upload ======
editPreview.addEventListener("click", () => {
    editUploadInput.click();
});
editUploadInput.addEventListener("change", () => {
    const file = editUploadInput.files[0];
    if (!file) return;
    openEditInfraBlurModal(file);
});

// =============================================================
// Existing edit infra modal logic (unchanged)
// =============================================================

// ...existing code...
    // Populate modal when editing
    document
      .querySelector(".infra-table")
      .addEventListener("click", async (e) => {
        const button = e.target.closest("button.edit");
        const icon = e.target.closest("i.fa-edit");

        if (!button && !icon) return;

        const row = e.target.closest("tr");
        if (!row) return;

        const infraId = row.querySelector("td")?.textContent?.trim();
        if (!infraId) return;

        // 🌀 Replace icon with spinner
        const originalIconHTML = icon?.outerHTML || button?.innerHTML;
        if (icon) {
          icon.outerHTML = `<div class="spinner"></div>`;
        } else if (button) {
          button.innerHTML = `<div class="spinner"></div>`;
        }

        try {
          const infraQ = query(
            collection(db, "Infrastructure"),
            where("infra_id", "==", infraId)
          );
          const snap = await getDocs(infraQ);

          if (snap.empty) {
            showModal('error', 'Infrastructure not found in Firestore');
            return;
          }

          const docSnap = snap.docs[0];
          const infraData = docSnap.data();

          await populateEditInfraCategoryDropdown(infraData.category_id);
          document.getElementById("editInfraCategory").value =
            infraData.category_id ?? "";

          document.getElementById("editInfraId").value =
            infraData.infra_id ?? "";
          document.getElementById("editInfraName").value =
            infraData.name ?? "";
          document.getElementById("editInfraPhone").value =
            infraData.phone ?? "";
          document.getElementById("editInfraEmail").value =
            infraData.email ?? "";

          // Show existing image inside upload box
          if (infraData.image_url) {
            editPreview.src = infraData.image_url;
            editPreview.style.display = "block";
            editUploadBox.querySelector(".upload-label").style.display = "none";
          } else {
            editPreview.src = "";
            editPreview.style.display = "none";
            editUploadBox.querySelector(".upload-label").style.display = "flex";
          }

          document.getElementById("editInfraForm").dataset.docId = docSnap.id;

          // Reset the edit modal save button to a known good state (prevents leftover spinner)
          const editSaveBtn = document.querySelector("#editInfraForm .save-btn");
          if (editSaveBtn) {
            // If your UI uses an icon inside the button replace the string below with proper HTML
            editSaveBtn.innerHTML = "Save";
            editSaveBtn.disabled = false;
            editSaveBtn.style.opacity = 1;
            editSaveBtn.style.cursor = "pointer";
          }

          document.getElementById("editInfraModal").style.display = "flex";
        } catch (err) {
          console.error("Error opening edit modal:", err);
          showModal('error', 'Error loading infrastructure details. Please try again.');
        } finally {
          // 🔄 Restore original icon
          if (icon) icon.outerHTML = originalIconHTML;
          if (button) button.innerHTML = originalIconHTML;
        }
      });





// Cancel
document
    .getElementById("cancelEditInfraBtn")
    .addEventListener("click", () => {
        document.getElementById("editInfraModal").style.display = "none";
        editPreview.src = "";
        editPreview.style.display = "none";
        editUploadBox.querySelector(".upload-label").style.display = "flex";
        editUploadInput.value = "";
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

// ----------- Save Edited Infrastructure (with Loading Button) -----------
document.getElementById("editInfraForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    const form = e.target;
    const docId = form.dataset.docId;
    if (!docId) {
        showModal('error', 'No document ID found for update.');
        return;
    }

    const saveBtn = form.querySelector(".save-btn");
    const originalBtnHTML = saveBtn.innerHTML;

    // 🌀 Show loading in button
    saveBtn.innerHTML = `<div class="spinner"></div> Saving...`;
    saveBtn.disabled = true;
    saveBtn.style.opacity = 0.7;

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
        showModal('error', 'Please fill in all required fields.');
        saveBtn.innerHTML = originalBtnHTML;
        saveBtn.disabled = false;
        saveBtn.style.opacity = 1;
        return;
    }

    try {
        await updateDoc(doc(db, "Infrastructure", docId), {
            name,
            category_id: categoryId,
            phone,
            email,
            image_url: imageUrl,
            updatedAt: new Date()
        });

        document.getElementById("editInfraModal").style.display = "none";
        renderInfraTable();
        showModal('success', 'Infrastructure has been updated successfully!');
    } catch (err) {
        showModal('error', 'Failed to update infrastructure. Please try again.');
        console.error(err);
    } finally {
        // 🔄 Restore button state
        saveBtn.innerHTML = originalBtnHTML;
        saveBtn.disabled = false;
        saveBtn.style.opacity = 1;
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

// Open delete modal when clicking the delete icon (with loading spinner)
document.querySelector(".infra-table").addEventListener("click", async (e) => {
    const button = e.target.closest("button.delete");
    const icon = e.target.closest("i.fa-trash");

    if (!button && !icon) return;

    const row = e.target.closest("tr");
    if (!row) return;

    const infraId = row.querySelector("td")?.textContent?.trim();
    const infraName = row.children[1]?.textContent?.trim() || "";
    if (!infraId) return;

    // 🌀 Replace icon with spinner
    const originalHTML = icon?.outerHTML || button?.innerHTML;
    if (icon) icon.outerHTML = `<div class="spinner"></div>`;
    if (button && !icon) button.innerHTML = `<div class="spinner"></div>`;

    try {
        const infraQ = query(collection(db, "Infrastructure"), where("infra_id", "==", infraId));
        const snap = await getDocs(infraQ);

        if (snap.empty) {
            showModal('error', 'Infrastructure not found. Please try again.');
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
        showModal('error', 'Failed to prepare delete modal. Please try again.');
    } finally {
        // 🔄 Restore original icon after modal is rendered
        requestAnimationFrame(() => {
            if (icon) icon.outerHTML = originalHTML;
            if (button && !icon) button.innerHTML = originalHTML;
        });
    }
});



// ----------- Confirm Infrastructure Deletion -----------
document.getElementById("confirmDeleteInfraBtn")?.addEventListener("click", async (e) => {
  if (!infraToDelete) return;

  const btn = e.target;
  const originalHTML = btn.innerHTML;

  // 🌀 Show spinner + disable button
  btn.innerHTML = `<div class="spinner"></div> <span class="loading-text">Deleting...</span>`;
  btn.disabled = true;
  btn.style.opacity = 0.7;
  btn.style.cursor = "not-allowed";

  try {
    // 🔥 Delete document from Firestore
    await deleteDoc(doc(db, "Infrastructure", infraToDelete.docId));

    // 🧹 Close modal and refresh table
    document.getElementById("deleteInfraModal").style.display = "none";
    infraToDelete = null;
    renderInfraTable();

    // ✅ Success modal
    showModal('success', 'Infrastructure deleted successfully!');
  } catch (err) {
    console.error("Error deleting infrastructure:", err);
    showModal('error', 'Failed to delete infrastructure. Please try again.');
  } finally {
    // 🔄 Restore original button
    btn.innerHTML = originalHTML;
    btn.disabled = false;
    btn.style.opacity = 1;
    btn.style.cursor = "pointer";
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

// Open delete modal when clicking the delete icon (with loading spinner)
document.querySelector(".rooms-table").addEventListener("click", async (e) => {
    const button = e.target.closest("button.delete");
    const icon = e.target.closest("i.fa-trash");

    if (!button && !icon) return;

    const row = e.target.closest("tr");
    if (!row) return;

    const roomId = row.querySelector("td")?.textContent?.trim();
    const roomName = row.children[1]?.textContent?.trim() || "";
    if (!roomId) return;

    // 🌀 Replace icon/button with spinner
    const originalHTML = icon?.outerHTML || button?.innerHTML;
    if (icon) icon.outerHTML = `<div class="spinner"></div>`;
    if (button && !icon) button.innerHTML = `<div class="spinner"></div>`;

    try {
        const roomQ = query(collection(db, "IndoorInfrastructure"), where("room_id", "==", roomId));
        const snap = await getDocs(roomQ);

        if (snap.empty) {
            showModal('error', 'Room not found. Please try again.');
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
        showModal('error', 'Failed to prepare delete modal. Please try again.');
    } finally {
        // 🔄 Restore original icon/button after modal opens
        requestAnimationFrame(() => {
            if (icon) icon.outerHTML = originalHTML;
            if (button && !icon) button.innerHTML = originalHTML;
        });
    }
});

// Confirm deletion with spinner + deleting text
document.getElementById("confirmDeleteRoomBtn").addEventListener("click", async (e) => {
    if (!roomToDelete) return;

    const btn = e.target;
    const originalHTML = btn.innerHTML;

    // Show loading + text
    btn.innerHTML = `
        <div class="spinner"></div>
        <span class="loading-text">Deleting...</span>
    `;
    btn.disabled = true;

    try {
        await updateDoc(doc(db, "IndoorInfrastructure", roomToDelete.docId), {
            is_deleted: true,
            deletedAt: new Date()
        });

        document.getElementById("deleteRoomModal").style.display = "none";
        roomToDelete = null;
        renderRoomsTable();
        showModal('success', 'Room deleted successfully!');
    } catch (err) {
        showModal('error', 'Failed to delete room. Please try again.');
        console.error(err);
    } finally {
        // Restore button
        btn.innerHTML = originalHTML;
        btn.disabled = false;
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

// Open delete modal when clicking the delete icon (with spinner)
document.querySelector(".categories-table").addEventListener("click", async (e) => {
    const button = e.target.closest("button.delete");
    const icon = e.target.closest("i.fa-trash");

    if (!button && !icon) return;

    const row = e.target.closest("tr");
    if (!row) return;

    const categoryName = row.children[1]?.textContent?.trim() || "";

    // 🌀 Replace icon/button with spinner
    const originalHTML = icon?.outerHTML || button?.innerHTML;
    if (icon) icon.outerHTML = `<div class="spinner"></div>`;
    if (button && !icon) button.innerHTML = `<div class="spinner"></div>`;

    try {
        const catQ = query(collection(db, "Categories"), where("name", "==", categoryName));
        const snap = await getDocs(catQ);

        if (snap.empty) {
            showModal('error', 'Category not found. Please try again.');
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
        showModal('error', 'Failed to prepare delete modal. Please try again.');
    } finally {
        // 🔄 Restore original icon/button after modal opens
        requestAnimationFrame(() => {
            if (icon) icon.outerHTML = originalHTML;
            if (button && !icon) button.innerHTML = originalHTML;
        });
    }
});

// Confirm deletion with spinner + deleting text
document.getElementById("confirmDeleteCategoryBtn").addEventListener("click", async (e) => {
    if (!categoryToDelete) return;

    const btn = e.target;
    const originalHTML = btn.innerHTML;

    // Show loading + text
    btn.innerHTML = `
        <div class="spinner"></div>
        <span class="loading-text">Deleting...</span>
    `;
    btn.disabled = true;

    try {
        await updateDoc(doc(db, "Categories", categoryToDelete.docId), {
            is_deleted: true,
            deletedAt: new Date()
        });

        document.getElementById("deleteCategoryModal").style.display = "none";
        categoryToDelete = null;
        renderCategoriesTable();
        showModal('success', 'Category deleted successfully!');
    } catch (err) {
        showModal('error', 'Failed to delete category. Please try again.');
        console.error(err);
    } finally {
        // Restore button
        btn.innerHTML = originalHTML;
        btn.disabled = false;
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

// ----------- Map Delete Modal Logic with spinner -----------
function setupMapDeleteHandlers() {
    const tbody = document.querySelector(".maps-table tbody");
    if (!tbody) return;

    tbody.querySelectorAll(".delete").forEach(btn => {
        btn.addEventListener("click", async (e) => {
            const tr = btn.closest("tr");
            const mapName = tr.children[1]?.textContent || "";
            const docId = btn.dataset.id;

            // 🌀 Replace button with spinner
            const originalHTML = btn.innerHTML;
            btn.innerHTML = `<div class="spinner"></div>`;
            btn.disabled = true;

            try {
                mapToDelete = { docId, name: mapName };
                document.getElementById("deleteMapPrompt").textContent =
                    `Are you sure you want to delete "${mapName}"?`;
                document.getElementById("deleteMapModal").style.display = "flex";
            } catch (err) {
                console.error("Error preparing delete modal:", err);
                showModal('error', 'Failed to prepare delete modal. Please try again.');
            } finally {
                // Restore button after modal opens
                requestAnimationFrame(() => {
                    btn.innerHTML = originalHTML;
                    btn.disabled = false;
                });
            }
        });
    });
}

// ----------- Confirm Map Deletion with spinner + deleting text -----------
document.getElementById("confirmDeleteMapBtn").addEventListener("click", async (e) => {
    if (!mapToDelete) return;

    const btn = e.target;
    const originalHTML = btn.innerHTML;

    // Show loading + text
    btn.innerHTML = `
        <div class="spinner"></div>
        <span class="loading-text">Deleting...</span>
    `;
    btn.disabled = true;

    try {
        // ✅ Hard delete the document
        await deleteDoc(doc(db, "MapVersions", mapToDelete.docId));

        // Optional: soft delete (commented out)
        // await updateDoc(doc(db, "MapVersions", mapToDelete.docId), {
        //     is_deleted: true,
        //     deletedAt: new Date()
        // });

        document.getElementById("deleteMapModal").style.display = "none";
        mapToDelete = null;
        renderMapsTable();
        showModal('success', 'Map deleted successfully!');
    } catch (err) {
        showModal('error', 'Failed to delete map. Please try again.');
        console.error(err);
    } finally {
        // Restore button
        btn.innerHTML = originalHTML;
        btn.disabled = false;
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

// ----------- Campus Delete Modal Logic with spinner -----------
function setupCampusDeleteHandlers() {
    const tbody = document.querySelector(".campus-table tbody");
    if (!tbody) return;

    tbody.querySelectorAll(".delete").forEach(btn => {
        btn.addEventListener("click", (e) => {
            const row = btn.closest("tr");
            if (!row) return;

            const campusName = row.children[1]?.textContent || "";
            const docId = btn.dataset.id;
            if (!docId) return;

            // 🌀 Replace button with spinner
            const originalHTML = btn.innerHTML;
            btn.innerHTML = `<div class="spinner"></div>`;
            btn.disabled = true;

            try {
                campusToDelete = { docId, name: campusName };
                document.getElementById("deleteCampusPrompt").textContent =
                    `Are you sure you want to delete "${campusName}"?`;
                document.getElementById("deleteCampusModal").style.display = "flex";
            } catch (err) {
                console.error("Error preparing delete modal:", err);
                showModal('error', 'Failed to prepare delete modal. Please try again.');
            } finally {
                // Restore button after modal opens
                requestAnimationFrame(() => {
                    btn.innerHTML = originalHTML;
                    btn.disabled = false;
                });
            }
        });
    });
}

// ----------- Confirm Campus Deletion with spinner + deleting text -----------
document.getElementById("confirmDeleteCampusBtn").addEventListener("click", async (e) => {
    if (!campusToDelete) return;

    const btn = e.target;
    const originalHTML = btn.innerHTML;

    // Show loading + text
    btn.innerHTML = `
        <div class="spinner"></div>
        <span class="loading-text">Deleting...</span>
    `;
    btn.disabled = true;

    try {
        // ✅ Hard delete the document
        await deleteDoc(doc(db, "Campus", campusToDelete.docId));

        // Optional: soft delete (commented out)
        // await updateDoc(doc(db, "Campus", campusToDelete.docId), {
        //     is_deleted: true,
        //     deletedAt: new Date()
        // });

        document.getElementById("deleteCampusModal").style.display = "none";
        campusToDelete = null;
        renderCampusTable();
        showModal('success', 'Campus deleted successfully!');
    } catch (err) {
        showModal('error', 'Failed to delete campus. Please try again.');
        console.error(err);
    } finally {
        // Restore button
        btn.innerHTML = originalHTML;
        btn.disabled = false;
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















function showModal(type, message) {
  const overlay = document.getElementById("jModal");
  const box = overlay.querySelector(".jModal-box");
  const icon = document.getElementById("jModal-icon");
  const title = document.getElementById("jModal-title");
  const msg = document.getElementById("jModal-message");
  const btn = document.getElementById("jModal-btn");

  // Reset
  box.classList.remove("jModal-success", "jModal-error");
  icon.innerHTML = "";

  // Decide content based on type
  let titleText = "";
  let iconSVG = "";

  if (type === "success") {
    box.classList.add("jModal-success");
    btn.style.background = "var(--jModal-success)";
    titleText = "Success";
    iconSVG = `
      <svg xmlns="http://www.w3.org/2000/svg" fill="none" stroke-width="3" viewBox="0 0 24 24">
        <circle cx="12" cy="12" r="10" stroke="#28a745"/>
        <path d="M8 12.5l3 3 5-6" stroke="#28a745" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>`;
  } else {
    box.classList.add("jModal-error");
    btn.style.background = "var(--jModal-error)";
    titleText = "Error";
    iconSVG = `
      <svg xmlns="http://www.w3.org/2000/svg" fill="none" stroke-width="3" viewBox="0 0 24 24">
        <circle cx="12" cy="12" r="10" stroke="#dc3545"/>
        <path d="M15 9l-6 6M9 9l6 6" stroke="#dc3545" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>`;
  }

  // Apply content
  icon.innerHTML = iconSVG;
  title.textContent = titleText;
  msg.textContent = message;

  // Show modal
  overlay.classList.add("jModal-active");

  // Close button
  btn.onclick = () => {
    overlay.classList.remove("jModal-active");
  };
}