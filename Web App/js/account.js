// ===============================
// 🔧 Firebase Initialization
// ===============================
import { firebaseConfig } from "../firebaseConfig.js";
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.0/firebase-app.js";
import { getFirestore, collection, addDoc, serverTimestamp } from "https://www.gstatic.com/firebasejs/10.12.0/firebase-firestore.js";
import CryptoJS from "https://cdn.jsdelivr.net/npm/crypto-js@4.2.0/+esm";

const app = initializeApp(firebaseConfig);
const db = getFirestore(app);

// ===============================
// 🎯 Modal and Button Elements
// ===============================
const addUserBtn = document.querySelector(".add-user-btn");
const addUserModal = document.getElementById("addUserModal");
const closeBtn = addUserModal?.querySelector(".close-btn");
const cancelBtn = addUserModal?.querySelector(".cancel-btn");
const addUserForm = document.getElementById("addUserForm");

// ===============================
// 🪟 Modal Controls
// ===============================
addUserBtn?.addEventListener("click", () => {
  addUserModal.style.display = "flex";
});

closeBtn?.addEventListener("click", () => {
  addUserModal.style.display = "none";
});

cancelBtn?.addEventListener("click", () => {
  addUserModal.style.display = "none";
});

window.addEventListener("click", (e) => {
  if (e.target === addUserModal) addUserModal.style.display = "none";
});

// ===============================
// 💾 Add User Form Submit Handler
// ===============================
addUserForm?.addEventListener("submit", async (e) => {
  e.preventDefault();

  const firstName = document.getElementById("firstName").value.trim();
  const middleInitial = document.getElementById("middleInitial").value.trim();
  const lastName = document.getElementById("lastName").value.trim();
  const contactNumber = document.getElementById("contactNumber").value.trim();
  const email = document.getElementById("modalEmail").value.trim(); // ✅ fixed ID
  const password = document.getElementById("modalPassword").value; // ✅ fixed ID

  if (!firstName || !lastName || !contactNumber || !email || !password) {
    alert("Please fill out all required fields.");
    return;
  }

  try {
    const secretKey = "CrimsonMapSecretKey123!";
    const encryptedPassword = CryptoJS.AES.encrypt(password, secretKey).toString();

    await addDoc(collection(db, "Users"), {
      firstName,
      middleInitial,
      lastName,
      contactNumber,
      email,
      password: encryptedPassword,
      created_at: serverTimestamp(),
    });

    alert("✅ User added successfully!");
    addUserForm.reset();
    addUserModal.style.display = "none";
  } catch (error) {
    console.error("Error adding user:", error);
    alert("❌ Failed to add user. Please try again.");
  }
});

// ===============================
// 📧 Display Logged-In User Info
// ===============================
document.addEventListener("DOMContentLoaded", () => {
  const emailInput = document.querySelector(".account-section #accountEmail");
  const currentUser = JSON.parse(sessionStorage.getItem("currentUser"));

  if (currentUser && currentUser.email && emailInput) {
    emailInput.value = currentUser.email;
  } else if (!currentUser) {
    window.location.href = "/html/login.html";
  }
});
