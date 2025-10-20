import { firebaseConfig } from "/firebaseConfig.js";
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.0/firebase-app.js";
import { getFirestore, collection, getDocs } from "https://www.gstatic.com/firebasejs/10.12.0/firebase-firestore.js";
import CryptoJS from "https://cdn.jsdelivr.net/npm/crypto-js@4.2.0/+esm";

// Initialize Firebase
const app = initializeApp(firebaseConfig);
const db = getFirestore(app);

// 🔑 Handle login form submit
const loginForm = document.getElementById("loginForm");

loginForm.addEventListener("submit", async (e) => {
  e.preventDefault();

  const email = document.getElementById("email").value.trim();
  const password = document.getElementById("password").value;

  if (!email || !password) {
    alert("Please enter your email and password.");
    return;
  }

  try {
    const secretKey = "CrimsonMapSecretKey123!"; // must match the one in account.js
    const usersSnapshot = await getDocs(collection(db, "Users"));
    let validUser = null;

    usersSnapshot.forEach((doc) => {
      const user = doc.data();

      if (user.email === email) {
        // Decrypt stored password
        const decryptedBytes = CryptoJS.AES.decrypt(user.password, secretKey);
        const decryptedPassword = decryptedBytes.toString(CryptoJS.enc.Utf8);

        if (decryptedPassword === password) {
          validUser = { id: doc.id, ...user };
        }
      }
    });

    if (validUser) {

      // Save session (optional)
      sessionStorage.setItem("currentUser", JSON.stringify(validUser));
      window.location.href = "/html/dashboard_main.html";
    } else {
      alert("❌ Invalid email or password. Please try again.");
    }
  } catch (error) {
    console.error("Login error:", error);
    alert("Something went wrong. Please try again later.");
  }
});
