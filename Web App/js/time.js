// dashboard_time.js

function updateDateTime() {
  const timeElement = document.querySelector(".time");

  if (!timeElement) return;

  // Get Philippine time (UTC+8)
  const options = { timeZone: "Asia/Manila" };
  const now = new Date().toLocaleString("en-US", options);
  const dateObj = new Date(now);

  // Format time: HH:MM:SS AM/PM
  let hours = dateObj.getHours();
  const minutes = dateObj.getMinutes();
  const seconds = dateObj.getSeconds();
  const ampm = hours >= 12 ? "PM" : "AM";
  hours = hours % 12 || 12; // convert 24hr → 12hr
  const formattedTime = `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")} ${ampm}`;

  // Format date: DD-MM-YY
  const day = String(dateObj.getDate()).padStart(2, "0");
  const month = String(dateObj.getMonth() + 1).padStart(2, "0");
  const year = String(dateObj.getFullYear()).slice(-2);
  const formattedDate = `${day}-${month}-${year}`;

  // Display
  timeElement.textContent = `${formattedTime} | ${formattedDate}`;
}

// Initial call + live update every second
updateDateTime();
setInterval(updateDateTime, 1000);
