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

// ======================= LOAD FROM FIRESTORE ===========================
async function loadFromFirestore() {
    const q = query(collection(db, "ActivityLogs"), orderBy("timestamp", "asc"));
    const querySnapshot = await getDocs(q);

    const results = [];
    querySnapshot.forEach(docSnap => {
        results.push({ id: docSnap.id, ...docSnap.data() });
    });

    // Sort descending by timestamp (latest last)
    results.sort((a, b) => getTimestampValue(a.timestamp) - getTimestampValue(b.timestamp));

    return results;
}

// ======================= LOAD FROM JSON (Fallback) ===========================
async function loadFromJson() {
    const res = await fetch("../assets/firestore/ActivityLogs.json");
    const data = await res.json();

    // Sort descending by timestamp (latest last)
    data.sort((a, b) => getTimestampValue(a.timestamp) - getTimestampValue(b.timestamp));

    return data;
}

// ======================= GET TIMESTAMP VALUE (ms) ===========================
function getTimestampValue(ts) {
    if (!ts) return 0;

    // Firestore Timestamp object
    if (typeof ts.toDate === "function") return ts.toDate().getTime();

    // JSON export (seconds + nanoseconds)
    if (ts.seconds !== undefined) return ts.seconds * 1000 + Math.floor(ts.nanoseconds / 1e6);

    // Older JSON export (_seconds + _nanoseconds)
    if (ts._seconds !== undefined) return ts._seconds * 1000 + Math.floor((ts._nanoseconds || 0) / 1e6);

    return 0;
}

// ======================= FORMAT TIMESTAMP ===========================
function formatTimestamp(ts) {
    if (!ts) return "-";
    const ms = getTimestampValue(ts);
    if (!ms) return "-";
    return formatDate(new Date(ms));
}

function formatDate(d) {
    const dateStr = d.toLocaleDateString("en-CA"); // YYYY-MM-DD
    const timeStr = d.toLocaleTimeString("en-US", {
        hour: "numeric",
        minute: "2-digit",
        hour12: true
    });
    return `${dateStr}<br>${timeStr}`;
}

// ======================= RENDER ACTIVITY LOGS ===========================
async function renderActivityLogsTable() {
    const tbody = document.querySelector(".activity-table tbody");
    if (!tbody) return;
    tbody.innerHTML = ""; // Clear table

    try {
        let logs;
        if (navigator.onLine) {
            console.log("🌐 Online → Firestore");
            logs = await loadFromFirestore();
        } else {
            console.log("📂 Offline → JSON fallback");
            logs = await loadFromJson();
        }

        let counter = 1;
        logs.forEach(data => {
            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${counter++}</td>
                <td>${formatTimestamp(data.timestamp)}</td>
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

// ======================= AUTO SWITCH (ONLINE/OFFLINE) ===========================
window.addEventListener("online", renderActivityLogsTable);
window.addEventListener("offline", renderActivityLogsTable);

// Run when page loads
document.addEventListener("DOMContentLoaded", renderActivityLogsTable);
