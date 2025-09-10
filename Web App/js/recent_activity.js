// ======================= FIREBASE SETUP ===========================
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
import {
    getFirestore,
    collection,
    getDocs,
    query,
    orderBy
} from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";
import { firebaseConfig } from "./../firebaseConfig.js";

// Initialize Firebase and Firestore
const app = initializeApp(firebaseConfig);
const db = getFirestore(app);

// ----------- Load Activity Logs Table -----------
async function renderActivityLogsTable() {
    const tbody = document.querySelector(".activity-table tbody");
    if (!tbody) return;
    tbody.innerHTML = ""; // Clear table

    try {
        const q = query(collection(db, "ActivityLogs"), orderBy("timestamp", "asc"));
        const querySnapshot = await getDocs(q);

        let counter = 1;
        querySnapshot.forEach(docSnap => {
            const data = docSnap.data();

            // Format timestamp
            let formattedDate = "-";
            if (data.timestamp && typeof data.timestamp.toDate === "function") {
                const d = data.timestamp.toDate();
                const dateStr = d.toLocaleDateString("en-CA"); // YYYY-MM-DD
                const timeStr = d.toLocaleTimeString("en-US", {
                    hour: "numeric",
                    minute: "2-digit",
                    hour12: true
                });
                formattedDate = `${dateStr}<br>${timeStr}`;
            }

            // Create row
            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${counter++}</td>
                <td>${formattedDate}</td>
                <td>${data.activity || "-"}</td>
                <td>${data.item || "-"}</td>
                <td>${data.description || "-"}</td>
            `;
            tbody.appendChild(tr);
        });
    } catch (err) {
        console.error("Error loading activity logs: ", err);
    }
}

// Run when page loads
document.addEventListener("DOMContentLoaded", renderActivityLogsTable);
