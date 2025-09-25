# Crimson Map

**Crimson Map** is a capstone project developed for **Western Mindanao State University (WMSU)**. The application delivers an Augmented Reality (AR)–based indoor/outdoor navigation system designed to guide new students and campus visitors across WMSU’s Main Campus. 

It includes an **Admin Web App** that allows easy configuration of building positions, routes, and connections.

---

## 👥 Capstone Project Group Members

- **Mohammad Aldrin Said** — Lead Developer & UI/UX
- **Daryl Gregorio** — Documentation and Paper Writing  
- **John Basil Andre Mula** — Co-Developer

---

## 🚀 Key Features

### 📱 Mobile AR App
- 🔍 **AR navigation** with visual building overlays
- 📍 **Shortest route calculation using A\*** pathfinding algorithm
- ✅ Step-by-step **onboarding screens** with dot indicators
- 🔁 **Skip** and **Get Started** button flow
- 🌐 **Hybrid location system**:
  - GPS (online)
  - QR/manual fallback (offline)  

### 🧑‍💻 Admin Web App
- 🛠 Web-based interface for editing:
  - Building nodes
  - Connections (edges)
  - Labels and positions
- 💾 Export feature for saving map as JSON
- 🔄 Admin edits reflect in mobile app without recompiling

---

## 🧪 Technologies Used

- **Unity 2022.3 LTS**
- **AR Foundation + ARCore**
- **C#** for Unity scripting
- **Firebase** (optional for user/cloud data)
- **A\* Algorithm** for route optimization
- **HTML/CSS/JavaScript** (Admin Web App)
- **JSON** for data handling between systems
