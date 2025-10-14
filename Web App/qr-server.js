// ======= Node.js backend for generating QR codes =======
import express from "express";
import QRCode from "qrcode";
import fs from "fs";
import path from "path";

const app = express();
app.use(express.json());

// Serve static files so frontend can access /assets/qr/
app.use("/assets", express.static(path.join(__dirname, "assets")));

app.post("/generate-qr", async (req, res) => {
  try {
    const { node_id } = req.body;
    if (!node_id) return res.json({ success: false, message: "Missing node_id" });

    // Ensure assets/qr folder exists
    const qrFolder = path.join(__dirname, "assets", "qr");
    if (!fs.existsSync(qrFolder)) fs.mkdirSync(qrFolder, { recursive: true });

    // Full path for the PNG file
    const qrPath = path.join(qrFolder, `${node_id}.png`);

    // Generate high-quality QR code
    await QRCode.toFile(qrPath, node_id, {
      width: 500,   // high resolution
      margin: 2
    });

    // Return the path for frontend and Firestore
    res.json({ success: true, path: `/assets/qr/${node_id}.png` });
  } catch (err) {
    console.error(err);
    res.json({ success: false, message: err.message });
  }
});

// Start the server
const PORT = 3000;
app.listen(PORT, () => console.log(`QR server running on http://localhost:${PORT}`));
