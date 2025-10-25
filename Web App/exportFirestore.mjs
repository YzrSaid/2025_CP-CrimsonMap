import { initializeApp } from "firebase/app";
import { getFirestore, collection, getDocs, doc, getDoc } from "firebase/firestore";
import { writeFileSync, mkdirSync } from "fs";
import path from "path";
import { fileURLToPath } from "url";

// Fix __dirname for ES modules
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// ======================= FIREBASE CONFIG ===========================
import { firebaseConfig } from "./firebaseConfig.js";

// Initialize Firebase
const app = initializeApp(firebaseConfig);
const db = getFirestore(app);

// ======================= COLLECTIONS TO EXPORT ===========================
const collections = [
  "ActivityLogs",
  "AppVersion",
  "Campus",
  "Categories",
  "Edges",
  "Infrastructure",
  "MapVersions", // this one has nested data
  "Maps",
  "Nodes",
  "Rooms",
  "StaticDataVersions",
  "Users"
];

// ======================= FETCH TOP-LEVEL COLLECTION ===========================
async function fetchCollection(colName) {
  const docsArray = [];
  const querySnapshot = await getDocs(collection(db, colName));

  for (const docSnap of querySnapshot.docs) {
    const data = { id: docSnap.id, ...docSnap.data() };

    // Special case: MapVersions has subcollection "versions"
    if (colName === "MapVersions") {
      const versionsSnap = await getDocs(collection(db, `${colName}/${docSnap.id}/versions`));
      data.versions = [];

      for (const verDoc of versionsSnap.docs) {
        const verData = { id: verDoc.id, ...verDoc.data() };
        // At this point, verData may already have arrays like nodes and edges
        data.versions.push(verData);
      }
    }

    docsArray.push(data);
  }

  return docsArray;
}

// ======================= EXPORT ALL ===========================
async function exportFirestore() {
  const exportDir = path.join(__dirname, "assets", "firestore");
  mkdirSync(exportDir, { recursive: true });

  for (const colName of collections) {
    try {
      const docsArray = await fetchCollection(colName);
      const filePath = path.join(exportDir, `${colName}.json`);
      writeFileSync(filePath, JSON.stringify(docsArray, null, 2));
      console.log(`✅ Exported ${colName} → ${filePath}`);
    } catch (error) {
      console.error(`❌ Error exporting ${colName}:`, error);
    }
  }
}

exportFirestore();
