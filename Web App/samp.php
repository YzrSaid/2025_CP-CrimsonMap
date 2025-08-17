<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Firestore Test</title>
<script type="module">
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
import { getFirestore, collection, addDoc, getDocs } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";

// Firebase config
const firebaseConfig = {
  apiKey: "AIzaSyC_o1vK92tOz-v6yLlF-QCff6J5ojB2_Qo",
  authDomain: "crimsonmap-d05e5.firebaseapp.com",
  projectId: "crimsonmap-d05e5",
  storageBucket: "crimsonmap-d05e5.appspot.com",
  messagingSenderId: "396668100672",
  appId: "1:396668100672:web:26d56e1b5dad4903fabfa5",
  measurementId: "G-ENJWWP4GNY"
};

// Init
const app = initializeApp(firebaseConfig);
const db = getFirestore(app);

// Save data
async function saveData(e) {
  e.preventDefault();
  const name = document.getElementById("name").value;
  const number = document.getElementById("number").value;
  const imageFile = document.getElementById("image").files[0];

  // Send image to PHP
  const formData = new FormData();
  formData.append("image", imageFile);

  const res = await fetch("upload.php", { method: "POST", body: formData });
  const data = await res.json();

  if (!data.success) {
    alert("Image upload failed: " + data.error);
    return;
  }

  try {
    await addDoc(collection(db, "people"), {
      name: name,
      number: Number(number),
      image: data.filename, // ✅ save only filename
      createdAt: new Date()
    });
    alert("Saved!");
    loadData(); // reload after save
  } catch (err) {
    alert("Error: " + err);
  }
}

// Load data
async function loadData() {
  const peopleList = document.getElementById("peopleList");
  peopleList.innerHTML = "";

  try {
    const querySnapshot = await getDocs(collection(db, "people"));
    querySnapshot.forEach((doc) => {
      const data = doc.data();
      const li = document.createElement("li");
      li.innerHTML = `
        ${data.name} — ${data.number} <br>
        <img src="uploads/${data.image}" width="80" />
      `;
      peopleList.appendChild(li);
    });
  } catch (err) {
    console.error("Error loading documents: ", err);
  }
}

// expose
window.saveData = saveData;
window.onload = loadData;
</script>

</head>
<body>
  <h1>Send + Display Firestore Data</h1>

<form onsubmit="saveData(event)" enctype="multipart/form-data">
  <label>
    Name:
    <input type="text" id="name" required>
  </label>
  <br><br>
  <label>
    Number:
    <input type="number" id="number" required>
  </label>
  <br><br>
  <label>
    Image:
    <input type="file" id="image" accept="image/*" required>
  </label>
  <br><br>
  <button type="submit">Save</button>
</form>


  <h2>People in Firestore:</h2>
  <ul id="peopleList"></ul>
</body>
</html>
